using System.Diagnostics;

namespace MacroDeckHost.Integrations.Feeds.Widgets;

internal sealed record HostBrowserOption(string Id, string Name, string Executable, bool SupportsPrivate);

internal static class HostBrowsers
{
	public static IReadOnlyList<HostBrowserOption> Detect()
	{
		var browsers = new List<HostBrowserOption>();
		foreach (var (id, name, paths, supportsPrivate) in Known())
		{
			var executable = paths.FirstOrDefault(File.Exists);
			if (executable is not null)
			{
				browsers.Add(new HostBrowserOption(id, name, executable, supportsPrivate));
			}
		}

		return browsers;
	}

	public static bool Open(Uri url, string browserId, bool privateWindow, string? customPath)
	{
		ArgumentNullException.ThrowIfNull(url);

		try
		{
			if (string.IsNullOrWhiteSpace(browserId) || browserId == FeedWidgetTypes.DefaultBrowser)
			{
				using var defaultProcess = Process.Start(new ProcessStartInfo
				{
					FileName = url.AbsoluteUri,
					UseShellExecute = true
				});
				return defaultProcess is not null;
			}

			HostBrowserOption? browser;
			if (browserId == FeedWidgetTypes.CustomBrowser)
			{
				if (string.IsNullOrWhiteSpace(customPath) || !File.Exists(customPath))
				{
					return false;
				}

				browser = new HostBrowserOption(browserId, "Custom", customPath, false);
			}
			else
			{
				browser = Detect().FirstOrDefault(candidate => candidate.Id == browserId);
			}

			if (browser is null)
			{
				return Open(url, FeedWidgetTypes.DefaultBrowser, false, null);
			}

			using var process = Process.Start(CreateStartInfo(browser, url, privateWindow && browser.SupportsPrivate));
			return process is not null;
		}
#pragma warning disable CA1031 // The caller reports a browser that cannot start.
		catch (Exception)
#pragma warning restore CA1031
		{
			return false;
		}
	}

	internal static ProcessStartInfo CreateStartInfo(HostBrowserOption browser, Uri url, bool privateWindow)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = browser.Executable,
			UseShellExecute = false
		};

		switch (browser.Id)
		{
			case "firefox":
				startInfo.ArgumentList.Add(privateWindow ? "-private-window" : "-new-tab");
				break;
			case "edge":
				if (privateWindow)
				{
					startInfo.ArgumentList.Add("--inprivate");
				}

				break;
			case "chrome":
			case "brave":
			case "vivaldi":
				if (privateWindow)
				{
					startInfo.ArgumentList.Add("--incognito");
				}

				break;
			case "opera":
				if (privateWindow)
				{
					startInfo.ArgumentList.Add("--private");
				}

				break;
		}

		startInfo.ArgumentList.Add(url.AbsoluteUri);
		return startInfo;
	}

	private static IEnumerable<(string Id, string Name, string[] Paths, bool SupportsPrivate)> Known()
	{
		if (!OperatingSystem.IsWindows())
		{
			yield break;
		}

		var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
		var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
		var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

		yield return ("firefox", "Mozilla Firefox",
		[
			Path.Combine(programFiles, "Mozilla Firefox", "firefox.exe"),
			Path.Combine(programFilesX86, "Mozilla Firefox", "firefox.exe")
		], true);

		yield return ("edge", "Microsoft Edge",
		[
			Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
			Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe")
		], true);

		yield return ("chrome", "Google Chrome",
		[
			Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
			Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
			Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe")
		], true);

		yield return ("brave", "Brave",
		[
			Path.Combine(programFiles, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
			Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "Application", "brave.exe")
		], true);

		yield return ("opera", "Opera",
		[
			Path.Combine(localAppData, "Programs", "Opera", "opera.exe"),
			Path.Combine(programFiles, "Opera", "opera.exe")
		], true);

		yield return ("vivaldi", "Vivaldi",
		[
			Path.Combine(localAppData, "Vivaldi", "Application", "vivaldi.exe"),
			Path.Combine(programFiles, "Vivaldi", "Application", "vivaldi.exe")
		], true);
	}
}
