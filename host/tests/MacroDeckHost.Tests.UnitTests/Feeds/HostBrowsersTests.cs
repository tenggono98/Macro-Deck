using MacroDeckHost.Integrations.Feeds.Widgets;

namespace MacroDeckHost.Tests.UnitTests.Feeds;

[TestFixture]
internal sealed class HostBrowsersTests
{
	private static readonly Uri _url = new("https://example.com/article");

	[Test]
	public void Firefox_opens_a_new_tab_or_a_private_window()
	{
		var browser = new HostBrowserOption("firefox", "Mozilla Firefox", "firefox.exe", true);

		var normal = HostBrowsers.CreateStartInfo(browser, _url, false);
		var privateWindow = HostBrowsers.CreateStartInfo(browser, _url, true);

		Assert.Multiple(() =>
		{
			Assert.That(normal.ArgumentList, Is.EqualTo(new[] { "-new-tab", _url.AbsoluteUri }));
			Assert.That(privateWindow.ArgumentList, Is.EqualTo(new[] { "-private-window", _url.AbsoluteUri }));
		});
	}

	[Test]
	public void Edge_opens_a_new_tab_or_an_inprivate_window()
	{
		var browser = new HostBrowserOption("edge", "Microsoft Edge", "msedge.exe", true);

		var normal = HostBrowsers.CreateStartInfo(browser, _url, false);
		var privateWindow = HostBrowsers.CreateStartInfo(browser, _url, true);

		Assert.Multiple(() =>
		{
			Assert.That(normal.ArgumentList, Is.EqualTo(new[] { _url.AbsoluteUri }));
			Assert.That(privateWindow.ArgumentList, Is.EqualTo(new[] { "--inprivate", _url.AbsoluteUri }));
		});
	}

	[Test]
	public void Chromium_browsers_use_incognito()
	{
		var browser = new HostBrowserOption("chrome", "Google Chrome", "chrome.exe", true);

		var privateWindow = HostBrowsers.CreateStartInfo(browser, _url, true);

		Assert.That(privateWindow.ArgumentList, Is.EqualTo(new[] { "--incognito", _url.AbsoluteUri }));
	}
}
