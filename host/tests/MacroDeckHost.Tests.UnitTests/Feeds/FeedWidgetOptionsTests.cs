using System.Text.Json;
using MacroDeckHost.Integrations.Feeds.Widgets;

namespace MacroDeckHost.Tests.UnitTests.Feeds;

[TestFixture]
internal sealed class FeedWidgetOptionsTests
{
	[Test]
	public void The_options_use_the_defaults_when_the_keys_are_absent()
	{
		var options = FeedWidgetOptions.Parse(FeedWidgetKind.List, JsonSerializer.SerializeToElement(new { }));

		Assert.Multiple(() =>
		{
			Assert.That(options.Skip, Is.Zero);
			Assert.That(options.TextScale, Is.EqualTo(100));
			Assert.That(options.ImageScale, Is.EqualTo(100));
		});
	}

	[Test]
	public void The_options_parse_host_open_settings()
	{
		var options = FeedWidgetOptions.Parse(FeedWidgetKind.List,
			JsonSerializer.SerializeToElement(new
			{
				openTarget = "host",
				hostBrowser = "firefox",
				hostPrivate = true,
				hostBrowserPath = @"C:\Program Files\Mozilla Firefox\firefox.exe"
			}));

		Assert.Multiple(() =>
		{
			Assert.That(options.OpenTarget, Is.EqualTo(FeedWidgetTypes.HostTarget));
			Assert.That(options.HostBrowser, Is.EqualTo("firefox"));
			Assert.That(options.HostPrivate, Is.True);
			Assert.That(options.HostBrowserPath, Is.EqualTo(@"C:\Program Files\Mozilla Firefox\firefox.exe"));
		});
	}

	[Test]
	public void The_options_clamp_skip_and_scales_to_their_limits()
	{
		var options = FeedWidgetOptions.Parse(FeedWidgetKind.List,
			JsonSerializer.SerializeToElement(new { skip = 999, textScale = 1, imageScale = 999 }));

		Assert.Multiple(() =>
		{
			Assert.That(options.Skip, Is.EqualTo(FeedWidgetTypes.MaxSkip));
			Assert.That(options.TextScale, Is.EqualTo(FeedWidgetTypes.MinTextScale));
			Assert.That(options.ImageScale, Is.EqualTo(FeedWidgetTypes.MaxImageScale));
		});
	}
}
