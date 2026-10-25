[CmdletBinding()]
param(
	[switch]$SkipTests,
	[switch]$BuildOnly
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$dataDir = Join-Path $env:APPDATA 'MacroDeck'
$uiExe = Join-Path $env:LOCALAPPDATA 'Macro Deck\MacroDeck.exe'
$hostDir = Join-Path $env:LOCALAPPDATA 'Macro Deck\host'
$installedExe = Join-Path $hostDir 'MacroDeckHostCustom.exe'
$configDir = Join-Path $env:APPDATA 'app.macro-deck.macrodeck'
$portPath = Join-Path $configDir 'loopback-port'
$secretPath = Join-Path $configDir 'loopback-secret'
$stateDir = Join-Path $env:LOCALAPPDATA 'MacroDeckCustomBuild'
$publishDir = Join-Path $stateDir 'publish'
$backupDir = Join-Path $stateDir 'backup'
$logPath = Join-Path $stateDir 'update.log'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

New-Item -ItemType Directory -Force -Path $stateDir, $publishDir, $backupDir | Out-Null

function Write-Step([string]$message) {
	Write-Host ''
	Write-Host "== $message" -ForegroundColor Cyan
}

function Assert-Exit([string]$name) {
	if ($LASTEXITCODE -ne 0) {
		throw "$name failed with exit code $LASTEXITCODE."
	}
}

function Get-TrimmedFile([string]$path) {
	return (Get-Content $path -Raw).Trim()
}

function Start-CustomHost {
	param([string]$Port, [string]$Secret)

	$env:MACRODECK_DATA_DIR = $dataDir
	$env:MACRO_DECK_PORT = '8193'
	$env:MACRO_DECK_HTTPS_PORT = '8194'
	$env:MACRODECK_HOST_PORT = $Port
	$env:MACRODECK_LOOPBACK_SECRET = $Secret
	$env:ASPNETCORE_WEBROOT = Join-Path $hostDir 'wwwroot'
	Start-Process -FilePath $installedExe -WorkingDirectory $hostDir
}

function Wait-ForHost {
	param([string]$Port, [string]$Secret)

	$deadline = (Get-Date).AddSeconds(60)
	while ((Get-Date) -lt $deadline) {
		try {
			$response = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/api/auth/status" -Headers @{ 'X-MacroDeck-Loopback-Secret' = $Secret } -UseBasicParsing -TimeoutSec 2
			if ($response.StatusCode -eq 200) {
				return $true
			}
		} catch {
		}

		Start-Sleep -Seconds 1
	}

	return $false
}

function Stop-MacroDeck {
	Get-Process MacroDeck -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

	if (Test-Path $portPath) {
		$port = Get-TrimmedFile $portPath
		if (Test-Path $secretPath) {
			$secret = Get-TrimmedFile $secretPath
			try {
				Invoke-WebRequest -Method Post -Uri "http://127.0.0.1:$port/api/host/shutdown" -Headers @{ 'X-MacroDeck-Loopback-Secret' = $secret } -UseBasicParsing -TimeoutSec 10 | Out-Null
			} catch {
			}
		}
	}

	Start-Sleep -Seconds 3
	Get-Process MacroDeckHostCustom, MacroDeckHost -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
	Start-Sleep -Seconds 2
}

Start-Transcript -Path $logPath -Append | Out-Null
try {
	Write-Step 'Checking prerequisites'
	if (-not (Test-Path (Join-Path $repo 'MacroDeck.slnx'))) {
		throw "Run this script from the Macro Deck repository."
	}

	if (-not (Test-Path $uiExe)) {
		throw "Macro Deck is not installed at $uiExe."
	}

	if (-not (Test-Path $installedExe)) {
		throw "The custom host is not installed at $installedExe."
	}

	if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
		throw 'The .NET SDK is not on PATH.'
	}

	Push-Location $repo
	try {
		Write-Step 'Building Release with warnings as errors'
		& dotnet build 'MacroDeck.slnx' -c Release -warnaserror
		Assert-Exit 'Build'

		if (-not $SkipTests) {
			Write-Step 'Running feed tests'
			& dotnet test 'host/tests/MacroDeckHost.Tests.UnitTests/MacroDeckHost.Tests.UnitTests.csproj' -c Release --no-build --filter 'FullyQualifiedName~Feeds'
			Assert-Exit 'Tests'
		}

		Write-Step 'Publishing the Production host'
		& dotnet publish 'host/src/MacroDeckHost/MacroDeckHost.csproj' -c Release -r win-x64 -p:BuildChannel=Production -o $publishDir
		Assert-Exit 'Publish'
	} finally {
		Pop-Location
	}

	$publishedExe = Join-Path $publishDir 'MacroDeckHost.exe'
	if (-not (Test-Path $publishedExe)) {
		throw "The published host was not created at $publishedExe."
	}

	if ($BuildOnly) {
		Write-Host "Build only. Published host: $publishedExe"
		return
	}

	Write-Step 'Stopping Macro Deck'
	Stop-MacroDeck

	if (-not (Test-Path $portPath) -or -not (Test-Path $secretPath)) {
		throw 'The loopback port or secret file is missing.'
	}

	$port = Get-TrimmedFile $portPath
	$secret = Get-TrimmedFile $secretPath

	Write-Step 'Backing up data and the current host'
	$dataBackup = Join-Path $backupDir "data-$stamp"
	$exeBackup = Join-Path $backupDir "MacroDeckHostCustom-$stamp.exe"
	New-Item -ItemType Directory -Force -Path $dataBackup | Out-Null
	$exclude = @('plugins', 'store', 'backups', 'logs', 'platform-tools') | ForEach-Object { Join-Path $dataDir $_ }
	$robocopyArgs = @($dataDir, $dataBackup, '/E', '/XD') + $exclude + @('/NFL', '/NDL', '/NJH', '/NJS', '/NP')
	& robocopy @robocopyArgs | Out-Null
	if ($LASTEXITCODE -ge 8) {
		throw "Data backup failed with exit code $LASTEXITCODE."
	}

	Copy-Item $installedExe $exeBackup -Force

	Write-Step 'Installing the new host'
	$tempExe = Join-Path $hostDir 'MacroDeckHostCustom.new.exe'
	try {
		Copy-Item $publishedExe $tempExe -Force
		Move-Item $tempExe $installedExe -Force
		Start-CustomHost -Port $port -Secret $secret
		if (-not (Wait-ForHost -Port $port -Secret $secret)) {
			throw 'The updated host did not become ready.'
		}

		Start-Process -FilePath $uiExe
	} catch {
		Write-Host ''
		Write-Host "Update failed: $($_.Exception.Message)" -ForegroundColor Red
		Get-Process MacroDeckHostCustom -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
		Start-Sleep -Seconds 2
		if (Test-Path $exeBackup) {
			Copy-Item $exeBackup $installedExe -Force
		}

		try {
			Start-CustomHost -Port $port -Secret $secret
			Start-Process -FilePath $uiExe
		} catch {
		}

		Write-Host "Restored the previous host. Data backup: $dataBackup"
		throw
	}

	Write-Host ''
	Write-Host 'Update complete.' -ForegroundColor Green
	Write-Host "Host backup: $exeBackup"
	Write-Host "Data backup: $dataBackup"
	Write-Host "Log: $logPath"
} finally {
	try {
		Stop-Transcript | Out-Null
	} catch {
	}
}
