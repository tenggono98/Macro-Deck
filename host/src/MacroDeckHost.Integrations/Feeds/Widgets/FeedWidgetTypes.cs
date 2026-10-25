using System.Text.Json;

namespace MacroDeckHost.Integrations.Feeds.Widgets;

internal enum FeedWidgetKind
{
	List,
	Featured
}

// Every id and data key here is persisted in profiles and exports, so the spellings are frozen.
internal static class FeedWidgetTypes
{
	public const string OwnerId = "app.macro-deck.feeds";

	public const string ListLocalId = "feed-list";

	public const string FeaturedLocalId = "featured-article";

	public const string ListQualifiedId = OwnerId + "::" + ListLocalId;

	public const string FeaturedQualifiedId = OwnerId + "::" + FeaturedLocalId;

	public const string DialogViewId = "feed-article";

	public const string SourceKey = "source";

	public const string FeedKey = "feed";

	public const string GroupKey = "group";

	public const string CountKey = "count";

	public const string SkipKey = "skip";

	public const string TextScaleKey = "textScale";

	public const string ImageScaleKey = "imageScale";

	public const string ShowDescriptionKey = "showDescription";

	public const string ShowThumbnailKey = "showThumbnail";

	public const string CycleSecondsKey = "cycleSeconds";

	public const string TapKey = "tap";

	public const string BackgroundColorKey = "backgroundColor";

	public const string FlowsKey = "flows";

	public const string AllSource = "all";

	public const string FeedSource = "feed";

	public const string GroupSource = "group";

	public const string OpenTap = "open";

	public const string DialogTap = "dialog";

	public const string FlowsTap = "flows";

	public const int MaxCount = 10;

	public const int MaxCycleSeconds = 3600;

	public const int MaxSkip = 50;

	public const int MinTextScale = 50;

	public const int MaxTextScale = 250;

	public const int MinImageScale = 50;

	public const int MaxImageScale = 300;

	// The feed engine keeps at most 50 items per feed. A group or all-feeds widget still needs a bound.
	public const int MaxRows = 200;

	private static readonly string[] _nullableString = ["string", "null"];

	public static IReadOnlyList<string> Sources { get; } = [AllSource, FeedSource, GroupSource];

	public static IReadOnlyList<string> Taps { get; } = [OpenTap, DialogTap, FlowsTap];

	public static string QualifiedId(FeedWidgetKind kind)
	{
		return kind == FeedWidgetKind.List ? ListQualifiedId : FeaturedQualifiedId;
	}

	public static string DefaultData(FeedWidgetKind kind)
	{
		var defaults = FeedWidgetOptions.Defaults(kind);
		return JsonSerializer.Serialize(new Dictionary<string, object>
		{
			[SourceKey] = AllSource,
			[CountKey] = defaults.Count,
			[SkipKey] = 0,
			[TextScaleKey] = 100,
			[ImageScaleKey] = 100,
			[ShowDescriptionKey] = defaults.ShowDescription,
			[ShowThumbnailKey] = defaults.ShowThumbnail,
			[CycleSecondsKey] = defaults.CycleSeconds,
			[TapKey] = OpenTap
		});
	}

	public static string DataSchema()
	{
		return JsonSerializer.Serialize(new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				[SourceKey] = new { type = "string", @enum = Sources },
				[FeedKey] = new { type = "string" },
				[GroupKey] = new { type = "string" },
				[CountKey] = new { type = "number", minimum = 1, maximum = MaxCount },
				[SkipKey] = new { type = "number", minimum = 0, maximum = MaxSkip },
				[TextScaleKey] = new { type = "number", minimum = MinTextScale, maximum = MaxTextScale },
				[ImageScaleKey] = new { type = "number", minimum = MinImageScale, maximum = MaxImageScale },
				[ShowDescriptionKey] = new { type = "boolean" },
				[ShowThumbnailKey] = new { type = "boolean" },
				[CycleSecondsKey] = new { type = "number", minimum = 0, maximum = MaxCycleSeconds },
				[TapKey] = new { type = "string", @enum = Taps },
				[BackgroundColorKey] = new { type = _nullableString, description = "#rrggbb or transparent" },
				[FlowsKey] = new Dictionary<string, object>
				{
					["anyOf"] = new object[] { new { type = "string" }, new { type = "array" } }
				}
			}
		});
	}
}
