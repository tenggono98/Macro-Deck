@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Update-MacroDeck.ps1" %*
set code=%errorlevel%
echo.
if not "%code%"=="0" (
    echo Update failed with code %code%.
) else (
    echo Update finished.
)
pause
exit /b %code%
