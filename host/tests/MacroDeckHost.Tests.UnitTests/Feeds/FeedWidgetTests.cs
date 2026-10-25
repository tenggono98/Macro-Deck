using System.Net;
using System.Text.Json;
using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Model.Surfaces;
using MacroDeckHost.Integrations.Feeds;
using MacroDeckHost.Integrations.Feeds.Events;
using MacroDeckHost.Integrations.Feeds.Widgets;
using MacroDeckHost.Tests.UnitTests.Calendar;
using MacroDeckHost.Tests.UnitTests.Delegation;

namespace MacroDeckHost.Tests.UnitTests.Feeds;

[TestFixture]
internal sealed class FeedWidgetTests
{
	private const string NewsUrl = "https://news.example.com/feed.xml";
	private const string SecondUrl = "https://news.example.com/2";
	private const string ImageUrl = "https://img.example.com/second.png";
	private const string FirstImageUrl = "https://img.example.com/first.png";

	private static readonly Guid _newsId = Guid.Parse("0b6f4b7e-0000-0000-0000-00000000000a");
	private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
	private static readonly Guid _widgetId = Guid.Parse("0b6f4b7e-0000-0000-0000-0000000000ff");

	private FakeFeedServer _server = null!;
	private FeedsTestContext _context = null!;
	private FeedsIntegration _integration = null!;
	private FakeTimeProvider _time = null!;

	[SetUp]
	public async Task SetUp()
	{
		_server = new FakeFeedServer();
		_time = new FakeTimeProvider();
		_context = new FeedsTestContext();
		_context.AddFeed("News", NewsUrl, "news", id: _newsId);
		_integration = new FeedsIntegration(FeedsTestClients.Create(_server, null, _time), _time, false);
		await _integration.InitializeAsync(_context);

		_server.ServeXml(NewsUrl, FeedsIntegrationTests.News(FeedsIntegrationTests.First));
		await _integration.RefreshAsync(_newsId);
		_server.ServeXml(NewsUrl,
			FeedsIntegrationTests.News(FeedsIntegrationTests.First, FeedsIntegrationTests.Second));
		await _integration.RefreshAsync(_newsId);
	}

	[TearDown]
	public async Task TearDown()
	{
		await _integration.ShutdownAsync();
		_integration.Dispose();
		_server.Dispose();
	}

	[Test]
	public async Task The_list_shows_the_newest_articles_up_to_its_count()
	{
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId, new { count = 1 });

		var texts = CalendarTree.Texts(session.BuildTree().Root);

		Assert.Multiple(() =>
		{
			Assert.That(texts, Does.Contain("Second"));
			Assert.That(texts, Does.Not.Contain("First"));
		});
	}

	[Test]
	public async Task The_list_starts_at_the_configured_skip()
	{
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId, new { count = 1, skip = 1 });

		var texts = CalendarTree.Texts(session.BuildTree().Root);

		Assert.Multiple(() =>
		{
			Assert.That(texts, Does.Contain("First"));
			Assert.That(texts, Does.Not.Contain("Second"));
		});
	}

	[Test]
	public async Task The_list_reports_the_total_number_of_articles()
	{
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId,
			new { count = 1, skip = 1 });

		var texts = CalendarTree.Texts(session.BuildTree().Root);

		Assert.That(texts, Does.Contain("· 2"));
	}

	[Test]
	public async Task The_list_configuration_reports_the_total_article_count()
	{
		await using var session = (await _integration.CreateSessionAsync(new UiSessionRequest
			{
				Surface = new UiSurface
				{
					Kind = UiSurfaceKinds.Config,
					SessionMode = UiSessionModes.Exclusive,
					Attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
					{
						[UiConfigSurfaceAttributes.EntryPoint]
							= JsonSerializer.SerializeToElement(UiConfigEntryPoints.WidgetConfig),
						[UiConfigSurfaceAttributes.WidgetType]
							= JsonSerializer.SerializeToElement(FeedWidgetTypes.ListQualifiedId)
					}
				},
				UiModelVersion = 1
			},
			CancellationToken.None))!;

		var texts = CalendarTree.Texts(session.BuildTree().Root);

		Assert.That(texts, Does.Contain("Total articles: 2"));
	}

	[Test]
	public async Task The_details_dialog_offers_a_host_open_answer()
	{
		var origin = new RecordingOrigin();
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId,
			new { tap = "dialog", openTarget = "host" });

		Tap(session, origin);
		await CalendarTree.WaitForAsync(() => origin.Modals.Count == 1, "no dialog was opened");

		await using var dialog = await OpenDialogAsync(origin.Modals.Single());
		var open = CalendarTree.Get(dialog.BuildTree().Root, "open");
		var answer = open.Properties[UiComponentProperties.Answer];
		var answerText = answer.GetString();

		Assert.Multiple(() =>
		{
			Assert.That(answer.ValueKind, Is.EqualTo(JsonValueKind.String));
			Assert.That(answerText, Is.Not.Null);
			Assert.That(answerText, Does.StartWith("https://"));
			Assert.That(answerText, Is.Not.EqualTo("open"));
		});
	}

	[Test]
	public async Task The_details_dialog_moves_to_the_next_article_without_closing()
	{
		var origin = new RecordingOrigin();
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId,
			new { tap = "dialog" });

		Tap(session, origin);
		await CalendarTree.WaitForAsync(() => origin.Modals.Count == 1, "no dialog was opened");

		await using var dialog = await OpenDialogAsync(origin.Modals.Single());
		var before = CalendarTree.Texts(dialog.BuildTree().Root);
		var next = CalendarTree.Get(dialog.BuildTree().Root, "next");

		dialog.Dispatch(new UiEvent { NodeId = next.Id, Name = UiComponentEvents.Press });

		var after = CalendarTree.Texts(dialog.BuildTree().Root);

		Assert.Multiple(() =>
		{
			Assert.That(before, Does.Contain("1 / 2"));
			Assert.That(before, Does.Contain("Second"));
			Assert.That(after, Does.Contain("2 / 2"));
			Assert.That(after, Does.Contain("First"));
			Assert.That(after, Does.Not.Contain("Second"));
		});
	}

	[Test]
	public async Task The_details_dialog_loads_the_next_article_image_while_navigating()
	{
		await ServeBothWithImagesAsync();
		var origin = new RecordingOrigin();
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId,
			new { count = 1, tap = "dialog" });

		Tap(session, origin);
		await CalendarTree.WaitForAsync(() => origin.Modals.Count == 1, "no dialog was opened");

		await using var dialog = await OpenDialogAsync(origin.Modals.Single());
		await CalendarTree.WaitForAsync(
			() => CalendarTree.Find(dialog.BuildTree().Root, "image") is not null,
			"the first article image never loaded");

		var initial = JsonSerializer.Serialize(
			CalendarTree.Get(dialog.BuildTree().Root, "image").Properties[UiComponentProperties.Source]);
		var next = CalendarTree.Get(dialog.BuildTree().Root, "next");

		dialog.Dispatch(new UiEvent { NodeId = next.Id, Name = UiComponentEvents.Press });

		await CalendarTree.WaitForAsync(
			() =>
			{
				var image = CalendarTree.Find(dialog.BuildTree().Root, "image");
				return image is not null
					&& JsonSerializer.Serialize(image.Properties[UiComponentProperties.Source]) != initial;
			},
			"the next article image never loaded");

		Assert.That(CalendarTree.Texts(dialog.BuildTree().Root), Does.Contain("First"));
	}

	[Test]
	public async Task The_list_pages_on_swipe()
	{
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId, new { count = 1 });

		var before = CalendarTree.Texts(session.BuildTree().Root);
		var rows = CalendarTree.Get(session.BuildTree().Root, "rows");

		session.Dispatch(new UiEvent
		{
			NodeId = rows.Id,
			Name = UiComponentEvents.Swipe,
			Data = JsonSerializer.SerializeToElement("up")
		});

		var after = CalendarTree.Texts(session.BuildTree().Root);

		Assert.Multiple(() =>
		{
			Assert.That(before, Does.Contain("Second"));
			Assert.That(before, Does.Contain("1/2"));
			Assert.That(after, Does.Contain("First"));
			Assert.That(after, Does.Not.Contain("Second"));
			Assert.That(after, Does.Contain("2/2"));
		});
	}

	[Test]
	public async Task The_list_advances_its_page_on_its_cycle_seconds()
	{
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId,
			new { count = 1, cycleSeconds = 5 });

		Assert.That(CalendarTree.Texts(session.BuildTree().Root), Does.Contain("Second"));

		_time.Advance(TimeSpan.FromSeconds(5));

		var texts = CalendarTree.Texts(session.BuildTree().Root);

		Assert.Multiple(() =>
		{
			Assert.That(texts, Does.Contain("First"));
			Assert.That(texts, Does.Not.Contain("Second"));
		});
	}

	[Test]
	public async Task The_list_scales_the_article_text_and_image()
	{
		await ServeSecondWithImageAsync();
		await using var normal = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId, new { count = 1 });
		await using var scaled = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId,
			new { count = 1, textScale = 200, imageScale = 200 });

		await CalendarTree.WaitForAsync(
			() => CalendarTree.Find(normal.BuildTree().Root, "thumbnail") is not null
				&& CalendarTree.Find(scaled.BuildTree().Root, "thumbnail") is not null,
			"a widget never showed its thumbnail");

		var normalTitle = CalendarTree.Get(normal.BuildTree().Root, "title");
		var scaledTitle = CalendarTree.Get(scaled.BuildTree().Root, "title");
		var normalThumbnail = CalendarTree.Get(normal.BuildTree().Root, "thumbnail");
		var scaledThumbnail = CalendarTree.Get(scaled.BuildTree().Root, "thumbnail");

		Assert.Multiple(() =>
		{
			Assert.That(Basis(scaledTitle), Is.EqualTo(Basis(normalTitle) * 2).Within(1e-9));
			Assert.That(Basis(scaledThumbnail), Is.EqualTo(Basis(normalThumbnail) * 2).Within(1e-9));
		});
	}

	[Test]
	public async Task The_featured_article_names_its_feed_above_the_article_title()
	{
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.FeaturedQualifiedId,
			new { source = "feed", feed = _newsId.ToString("D") });

		var texts = CalendarTree.Texts(session.BuildTree().Root);

		Assert.Multiple(() =>
		{
			Assert.That(texts, Does.Contain("News"));
			Assert.That(texts, Does.Contain("Second"));
			Assert.That(texts.IndexOf("News"), Is.LessThan(texts.IndexOf("Second")));
			Assert.That(ArtworkLayer(session.BuildTree().Root), Is.Null);
		});
	}

	[Test]
	public async Task A_featured_article_with_an_image_draws_it_behind_the_feed_name_and_the_title()
	{
		await ServeSecondWithImageAsync();
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.FeaturedQualifiedId,
			new { source = "feed", feed = _newsId.ToString("D") });

		await CalendarTree.WaitForAsync(() => ArtworkLayer(session.BuildTree().Root) is not null,
			"the article image never became the tile's backdrop");
		var root = session.BuildTree().Root;
		var layer = ArtworkLayer(root)!;
		var overlaid = layer.Children.Skip(1).SelectMany(child => CalendarTree.Texts(child)).ToList();

		Assert.Multiple(() =>
		{
			Assert.That(overlaid, Does.Contain("News"));
			Assert.That(overlaid, Does.Contain("Second"));
			Assert.That(CalendarTree.Flatten(root).Where(CalendarTree.IsPressable), Is.Not.Empty);
		});
	}

	[Test]
	public async Task A_featured_article_without_a_shown_image_keeps_the_text_layout()
	{
		await ServeSecondWithImageAsync();
		await using var withoutImage = await OpenWidgetAsync(FeedWidgetTypes.FeaturedQualifiedId,
			new { source = "feed", feed = _newsId.ToString("D"), showThumbnail = false });

		var root = withoutImage.BuildTree().Root;
		var texts = CalendarTree.Texts(root);

		Assert.Multiple(() =>
		{
			Assert.That(ArtworkLayer(root), Is.Null);
			Assert.That(texts.IndexOf("News"), Is.LessThan(texts.IndexOf("Second")));
		});
	}

	[Test]
	public async Task A_tap_opens_the_article_on_the_device_that_tapped_and_marks_it_read()
	{
		var origin = new RecordingOrigin();
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId, new { });

		Tap(session, origin);

		await CalendarTree.WaitForAsync(() => _integration.Feeds.Find(_newsId)!.UnreadCount == 0,
			"the tapped article stayed unread");
		var opened = _context.RecordedEvents.Of(FeedEventIds.ItemOpened).Single();
		Assert.Multiple(() =>
		{
			Assert.That(origin.OpenedUrls, Is.EqualTo(new[] { new Uri(SecondUrl) }));
			Assert.That(origin.Modals, Is.Empty);
			Assert.That(opened[FeedEventPayload.Url], Is.EqualTo(SecondUrl));
			Assert.That(opened[FeedEventPayload.Widget], Is.EqualTo(_widgetId.ToString("D")));
		});
	}

	[Test]
	public async Task A_device_that_cannot_open_links_gets_the_details_with_the_address_and_no_open_button()
	{
		var origin = new RecordingOrigin { OpensUrls = false };
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.FeaturedQualifiedId, new { tap = "open" });

		Tap(session, origin);

		await CalendarTree.WaitForAsync(() => origin.Modals.Count == 1, "no dialog was opened");
		var modal = origin.Modals.Single();
		await using var dialog = await OpenDialogAsync(modal);
		var root = dialog.BuildTree().Root;

		Assert.Multiple(() =>
		{
			Assert.That(modal.ViewId, Is.EqualTo(FeedWidgetTypes.DialogViewId));
			Assert.That(modal.Data![FeedArticleDialog.CanOpenKey].GetBoolean(), Is.False);
			Assert.That(JsonSerializer.Serialize(root), Does.Contain(SecondUrl));
			Assert.That(CalendarTree.Texts(root), Does.Contain("Second"));
			Assert.That(PressableKeys(root), Has.None.EndsWith(".open"));
		});
	}

	[Test]
	public async Task The_details_mode_opens_the_dialog_with_an_open_button()
	{
		var origin = new RecordingOrigin();
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId, new { tap = "dialog" });

		Tap(session, origin);

		await CalendarTree.WaitForAsync(() => origin.Modals.Count == 1, "no dialog was opened");
		var modal = origin.Modals.Single();
		await using var dialog = await OpenDialogAsync(modal);
		Assert.Multiple(() =>
		{
			Assert.That(origin.OpenedUrls, Is.Empty);
			Assert.That(PressableKeys(dialog.BuildTree().Root), Has.Some.EndsWith(".open"));
		});
	}

	[Test]
	public async Task The_open_button_in_the_dialog_falls_back_to_the_address_when_the_device_cannot_open_it()
	{
		var widgetOrigin = new RecordingOrigin();
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId, new { tap = "dialog" });
		Tap(session, widgetOrigin);
		await CalendarTree.WaitForAsync(() => widgetOrigin.Modals.Count == 1, "no dialog was opened");
		await using var dialog = await OpenDialogAsync(widgetOrigin.Modals.Single());
		var open = CalendarTree.Flatten(dialog.BuildTree().Root)
			.First(node => node.Id.EndsWith(".open", StringComparison.Ordinal));

		dialog.Dispatch(new UiEvent { NodeId = open.Id, Name = UiComponentEvents.Press },
			new RecordingOrigin { OpensUrls = false });

		await CalendarTree.WaitForAsync(() => !HasOpenButton(dialog.BuildTree().Root),
			"the open button stayed although the device cannot open links");
		Assert.That(JsonSerializer.Serialize(dialog.BuildTree().Root), Does.Contain(SecondUrl));
	}

	[Test]
	public async Task The_flows_mode_only_tells_the_widgets_flows()
	{
		var origin = new RecordingOrigin();
		await using var session = await OpenWidgetAsync(FeedWidgetTypes.ListQualifiedId, new { tap = "flows" });

		Tap(session, origin);

		await CalendarTree.WaitForAsync(() => _context.RecordedEvents.Of(FeedEventIds.ItemOpened).Count == 1,
			"no feed-item-opened event was published");
		Assert.Multiple(() =>
		{
			Assert.That(origin.OpenedUrls, Is.Empty);
			Assert.That(origin.Modals, Is.Empty);
		});
	}

	[Test]
	public async Task The_configuration_offers_the_widgets_own_flows()
	{
		await using var session = (await _integration.CreateSessionAsync(new UiSessionRequest
			{
				Surface = new UiSurface
				{
					Kind = UiSurfaceKinds.Config,
					SessionMode = UiSessionModes.Exclusive,
					Attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
					{
						[UiConfigSurfaceAttributes.EntryPoint]
							= JsonSerializer.SerializeToElement(UiConfigEntryPoints.WidgetConfig),
						[UiConfigSurfaceAttributes.WidgetType]
							= JsonSerializer.SerializeToElement(FeedWidgetTypes.FeaturedQualifiedId)
					}
				},
				UiModelVersion = 1
			},
			CancellationToken.None))!;

		var nodes = CalendarTree.Flatten(session.BuildTree().Root).Select(node => node.Id).ToList();

		Assert.Multiple(() =>
		{
			Assert.That(nodes, Has.Some.EndsWith(FeedWidgetTypes.FlowsKey));
			Assert.That(nodes, Has.Some.EndsWith(FeedWidgetTypes.CycleSecondsKey));
		});
	}

	[Test]
	public async Task The_list_configuration_offers_skip_paging_and_size_options()
	{
		await using var session = (await _integration.CreateSessionAsync(new UiSessionRequest
			{
				Surface = new UiSurface
				{
					Kind = UiSurfaceKinds.Config,
					SessionMode = UiSessionModes.Exclusive,
					Attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
					{
						[UiConfigSurfaceAttributes.EntryPoint]
							= JsonSerializer.SerializeToElement(UiConfigEntryPoints.WidgetConfig),
						[UiConfigSurfaceAttributes.WidgetType]
							= JsonSerializer.SerializeToElement(FeedWidgetTypes.ListQualifiedId)
					}
				},
				UiModelVersion = 1
			},
			CancellationToken.None))!;

		var nodes = CalendarTree.Flatten(session.BuildTree().Root).Select(node => node.Id).ToList();

		Assert.Multiple(() =>
		{
			Assert.That(nodes, Has.Some.EndsWith(FeedWidgetTypes.SkipKey));
			Assert.That(nodes, Has.Some.EndsWith(FeedWidgetTypes.TextScaleKey));
			Assert.That(nodes, Has.Some.EndsWith(FeedWidgetTypes.ImageScaleKey));
			Assert.That(nodes, Has.Some.EndsWith(FeedWidgetTypes.CycleSecondsKey));
			Assert.That(nodes, Has.Some.EndsWith(FeedWidgetTypes.OpenTargetKey));
			Assert.That(nodes, Has.Some.EndsWith(FeedWidgetTypes.HostBrowserKey));
		});
	}

	private static void Tap(IUiSession session, IUiEventOrigin origin)
	{
		var node = CalendarTree.Flatten(session.BuildTree().Root).First(CalendarTree.IsPressable);
		session.Dispatch(new UiEvent { NodeId = node.Id, Name = UiComponentEvents.Press }, origin);
	}

	private async Task ServeSecondWithImageAsync()
	{
		_server.Serve(ImageUrl,
			_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_png) });
		_server.ServeXml(NewsUrl,
			FeedsIntegrationTests.News(FeedsIntegrationTests.First,
				FeedsIntegrationTests.Item("2", "Second", 2, $"<enclosure url=\"{ImageUrl}\" type=\"image/png\"/>")));
		await _integration.RefreshAsync(_newsId);
	}

	private async Task ServeBothWithImagesAsync()
	{
		_server.Serve(FirstImageUrl,
			_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_png) });
		_server.Serve(ImageUrl,
			_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_png) });
		_server.ServeXml(NewsUrl,
			FeedsIntegrationTests.News(
				FeedsIntegrationTests.Item("1", "First", 1,
					$"<enclosure url=\"{FirstImageUrl}\" type=\"image/png\"/>"),
				FeedsIntegrationTests.Item("2", "Second", 2,
					$"<enclosure url=\"{ImageUrl}\" type=\"image/png\"/>")));
		await _integration.RefreshAsync(_newsId);
	}

	// The artwork is the layer's first child, so everything after it is painted on top of the image.
	private static UiNode? ArtworkLayer(UiNode root)
	{
		return CalendarTree.Flatten(root)
			.FirstOrDefault(node => node.Type == UiComponents.Layer
				&& node.Children.Count > 1
				&& CalendarTree.Flatten(node.Children[0]).Any(HasSource));
	}

	private static bool HasSource(UiNode node)
	{
		return node.Properties.ContainsKey(UiComponentProperties.Source);
	}

	private static double Basis(UiNode node)
	{
		var size = node.Properties[UiComponentProperties.Size];

		return size.ValueKind == JsonValueKind.Number ? size.GetDouble() : size.GetProperty("basis").GetDouble();
	}

	private static bool HasOpenButton(UiNode root)
	{
		return PressableKeys(root).Any(key => key.EndsWith(".open", StringComparison.Ordinal));
	}

	private static List<string> PressableKeys(UiNode root)
	{
		return [.. CalendarTree.Flatten(root).Where(CalendarTree.IsPressable).Select(node => node.Id)];
	}

	private async Task<IUiSession> OpenWidgetAsync(string widgetType, object data)
	{
		var attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
		{
			[UiWidgetSurfaceAttributes.WidgetType] = JsonSerializer.SerializeToElement(widgetType),
			[UiWidgetSurfaceAttributes.Data] = JsonSerializer.SerializeToElement(data),
			[UiWidgetSurfaceAttributes.WidgetId] = JsonSerializer.SerializeToElement(_widgetId.ToString())
		};

		return (await _integration.CreateSessionAsync(new UiSessionRequest
			{
				Surface = new UiSurface
					{ Kind = UiSurfaceKinds.Widget, SessionMode = UiSessionModes.Shared, Attributes = attributes },
				UiModelVersion = 1
			},
			CancellationToken.None))!;
	}

	private async Task<IUiSession> OpenDialogAsync(ModalDefinition modal)
	{
		var attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
		{
			[UiDialogSurfaceAttributes.ViewId] = JsonSerializer.SerializeToElement(modal.ViewId),
			[UiDialogSurfaceAttributes.Data] = JsonSerializer.SerializeToElement(modal.Data)
		};

		return (await _integration.CreateSessionAsync(new UiSessionRequest
			{
				Surface = new UiSurface
					{ Kind = UiSurfaceKinds.Dialog, SessionMode = UiSessionModes.Exclusive, Attributes = attributes },
				UiModelVersion = 1
			},
			CancellationToken.None))!;
	}
}
