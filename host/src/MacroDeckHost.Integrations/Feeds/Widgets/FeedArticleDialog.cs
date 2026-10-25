using System.Text.Json;
using MacroDeck.Localization;
using MacroDeck.Sdk.Logging;
using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.Resources;
using MacroDeck.Ui.Model.Surfaces;
using MacroDeck.Ui.Runtime;
using MacroDeckHost.Integrations.Feeds.Connections;
using MacroDeckHost.Integrations.Feeds.Protocol;
using MacroDeckHost.Integrations.Feeds.State;
using MacroDeckHost.Integrations.Ui;
using Serilog;
using Strings = MacroDeckHost.Localization.AppStrings.Widgets.Feeds.Dialog;

namespace MacroDeckHost.Integrations.Feeds.Widgets;

internal sealed record FeedDialogEntry(FeedItem? Item, string Meta, UiResource? Image, Uri? Url);

internal static class FeedArticleDialog
{
	public const string EntryKey = "entry";

	public const string ItemKey = "item";

	public const string EntriesKey = "entries";

	public const string IndexKey = "index";

	public const string CanOpenKey = "canOpen";

	public const string CultureKey = "culture";

	public const string HostOpenKey = "hostOpen";

	private const string Separator = " · ";

	private static readonly ILogger _logger = IntegrationLog.For<FeedDialogEntry>(FeedsIntegration.IntegrationId);

	private static readonly UiSize _titleText = UiSize.FromBasis(0.05);
	private static readonly UiSize _text = UiSize.FromBasis(0.042);
	private static readonly UiSize _smallText = UiSize.FromBasis(0.034);
	private static readonly UiSize _icon = UiSize.FromBasis(0.042);
	private static readonly UiSize _gap = UiSize.FromBasis(0.022);

	public static UiViewSession Create(UiSurface surface, IFeedSource source, IFeedImages images, TimeProvider time)
	{
		ArgumentNullException.ThrowIfNull(surface);
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(images);

		var data = surface.Attributes.TryGetValue(UiDialogSurfaceAttributes.Data, out var element) ? element : default;
		var culture = ReadString(data, CultureKey)
			?? (surface.Attributes.TryGetValue(UiDialogSurfaceAttributes.Culture, out var dialogCulture)
				&& dialogCulture.ValueKind == JsonValueKind.String
					? dialogCulture.GetString()
					: null);
		var format = new FeedDateFormat(FeedDateFormat.CultureOf(culture), time.LocalTimeZone, time.GetUtcNow());
		var entries = ReadEntries(data, source, format, images);
		var index = new UiState<int>(entries.Count == 0 ? 0 : Math.Clamp(ReadInt(data, IndexKey), 0, entries.Count - 1));
		var hostOpen = ReadBool(data, HostOpenKey);
		var canOpen = new UiState<bool>(
			data.ValueKind != JsonValueKind.Object
				|| !data.TryGetProperty(CanOpenKey, out var canOpenValue)
				|| canOpenValue.ValueKind != JsonValueKind.False);
		var feedback = new UiState<LocalizedText>(default);

		UiViewSession? session = null;
		UiView? view = null;
		view = new UiView(surface,
			Build(entries,
				index,
				canOpen,
				feedback,
				hostOpen,
				() => _ = OpenAsync(session?.Origin, entries, index, canOpen, feedback, view!)));
		session = new UiViewSession(view);
		return session;
	}

	private static List<FeedDialogEntry> ReadEntries(
		JsonElement data,
		IFeedSource source,
		FeedDateFormat format,
		IFeedImages images)
	{
		var entries = new List<FeedDialogEntry>();
		if (data.ValueKind == JsonValueKind.Object
			&& data.TryGetProperty(EntriesKey, out var references)
			&& references.ValueKind == JsonValueKind.Array)
		{
			foreach (var reference in references.EnumerateArray())
			{
				if (ReadString(reference, "entry") is { } entryText
					&& Guid.TryParse(entryText, out var entryId)
					&& ReadString(reference, "item") is { } itemKey)
				{
					entries.Add(Resolve(source, format, images, entryId, itemKey));
				}
			}

			return entries;
		}

		if (Guid.TryParse(ReadString(data, EntryKey), out var fallbackEntryId)
			&& ReadString(data, ItemKey) is { } fallbackKey)
		{
			entries.Add(Resolve(source, format, images, fallbackEntryId, fallbackKey));
		}

		return entries;
	}

	private static FeedDialogEntry Resolve(
		IFeedSource source,
		FeedDateFormat format,
		IFeedImages images,
		Guid entryId,
		string itemKey)
	{
		var feed = source.Find(entryId);
		var item = feed?.Find(itemKey);
		var meta = feed is null || item is null
			? string.Empty
			: string.Join(Separator,
				new[] { feed.Title, item.Author ?? string.Empty, format.Long(item.Published) }
					.Where(part => part.Length > 0));
		var url = Uri.TryCreate(item?.Url, UriKind.Absolute, out var parsed) && FeedClient.IsWebUrl(parsed)
			? parsed
			: null;

		return new FeedDialogEntry(item, meta, item?.ImageUrl is { } imageUrl ? images.Find(imageUrl) : null, url);
	}

	private static FeedDialogEntry? Current(List<FeedDialogEntry> entries, UiState<int> index)
	{
		return entries.Count == 0
			? null
			: entries[Math.Clamp(index.Value, 0, entries.Count - 1)];
	}

	private static async Task OpenAsync(
		IUiEventOrigin? origin,
		List<FeedDialogEntry> entries,
		UiState<int> index,
		UiState<bool> canOpen,
		UiState<LocalizedText> feedback,
		UiView view)
	{
		var url = Current(entries, index)?.Url;
		var opened = false;
		if (origin is not null && url is not null)
		{
			try
			{
				opened = await origin.OpenUrlAsync(url).ConfigureAwait(false);
			}
#pragma warning disable CA1031 // A press has nobody to report to; the dialog shows the address instead.
			catch (Exception exception)
#pragma warning restore CA1031
			{
				_logger.Warning(exception, "Opening a feed article from its dialog failed");
			}
		}

		if (opened)
		{
			return;
		}

		using (view.Batch())
		{
			canOpen.Set(false);
			feedback.Set(Strings.CannotOpen());
		}
	}

	private static UiStack Build(
		List<FeedDialogEntry> entries,
		UiState<int> index,
		UiState<bool> canOpen,
		UiState<LocalizedText> feedback,
		bool hostOpen,
		Action open)
	{
		var children = new List<UiElement>
		{
			new UiWhen
			{
				Key = "missingGate",
				Condition = () => Current(entries, index)?.Item is null,
				Content = () => new UiTextRun
				{
					Key = "missing",
					Text = Strings.Unavailable(),
					Size = _text,
					Role = UiComponentTextRoles.Secondary,
					Align = UiComponentAlignments.Center,
					Wrap = true,
					Fill = true
				}
			},
			new UiWhen
			{
				Key = "itemGate",
				Condition = () => Current(entries, index)?.Item is not null,
				Content = () => ItemContent(entries, index, canOpen, feedback, hostOpen, open)
			}
		};

		var layout = new List<UiElement>
		{
			new UiList
			{
				Key = "content",
				Fill = true,
				Gap = _gap,
				Children = children
			}
		};
		if (entries.Count > 0)
		{
			layout.Add(Navigation(entries, index, canOpen, feedback));
		}

		return new UiStack
		{
			Key = "feedArticle",
			Direction = UiComponentDirections.Vertical,
			Padding = 0.04,
			Fill = true,
			Children = layout
		};
	}

	private static UiStack ItemContent(
		List<FeedDialogEntry> entries,
		UiState<int> index,
		UiState<bool> canOpen,
		UiState<LocalizedText> feedback,
		bool hostOpen,
		Action open)
	{
		var children = new List<UiElement>
		{
			new UiWhen
			{
				Key = "imageGate",
				Condition = () => Current(entries, index)?.Image is not null,
				Content = () => new UiImage
				{
					Key = "image",
					Source = UiValue.Optional(() => Image(Current(entries, index)?.Image)),
					MainSize = UiSize.FromBasis(0.4)
				}
			},
			new UiTextRun
			{
				Key = "title",
				Text = UiText.From(() => Current(entries, index)?.Item?.Title ?? string.Empty),
				Size = _titleText,
				Weight = UiComponentTextWeights.SemiBold,
				Wrap = true
			},
			new UiWhen
			{
				Key = "metaGate",
				Condition = () => Current(entries, index)?.Meta.Length > 0,
				Content = () => new UiTextRun
				{
					Key = "meta",
					Text = UiText.From(() => Current(entries, index)?.Meta ?? string.Empty),
					Size = _smallText,
					Role = UiComponentTextRoles.Secondary,
					Wrap = true
				}
			},
			new UiWhen
			{
				Key = "descriptionGate",
				Condition = () => Current(entries, index)?.Item?.Description is not null,
				Content = () => new UiTextRun
				{
					Key = "description",
					Text = UiText.From(() => Current(entries, index)?.Item?.Description ?? string.Empty),
					Size = _text,
					Wrap = true
				}
			},
			new UiWhen
			{
				Key = "urlGate",
				Condition = () => Current(entries, index)?.Url is not null,
				Content = () => new UiTextField
				{
					Key = "url",
					Text = UiValue.From(() => Current(entries, index)?.Url?.AbsoluteUri ?? string.Empty),
					Size = _smallText
				}
			},
			new UiWhen
			{
				Key = "openGate",
				Condition = () => canOpen.Value && Current(entries, index)?.Url is not null,
				Content = () => OpenButton(entries, index, hostOpen, open)
			},
			new UiWhen
			{
				Key = "feedbackGate",
				Condition = () => !feedback.Value.IsEmpty,
				Content = () => new UiTextRun
				{
					Key = "feedback",
					Text = UiText.Optional(() => feedback.Value),
					Size = _smallText,
					Role = UiComponentTextRoles.Secondary,
					Wrap = true
				}
			}
		};

		return new UiStack
		{
			Key = "item",
			Direction = UiComponentDirections.Vertical,
			Gap = _gap,
			Children = children
		};
	}

	private static UiStack Navigation(
		List<FeedDialogEntry> entries,
		UiState<int> index,
		UiState<bool> canOpen,
		UiState<LocalizedText> feedback)
	{
		void Move(int delta)
		{
			index.Set((index.Value + delta + entries.Count) % entries.Count);
			canOpen.Set(true);
			feedback.Set(default);
		}

		var position = new UiTextRun
		{
			Key = "position",
			Text = UiText.From(() => $"{index.Value + 1} / {entries.Count}"),
			Size = _smallText,
			Role = UiComponentTextRoles.Secondary,
			MaxLines = 1,
			Digits = 6
		};

		var children = new List<UiElement>();
		if (entries.Count > 1)
		{
			children.Add(NavigationButton("previous", UiIcons.ArrowLeft, () => Move(-1)));
		}

		children.Add(position);

		if (entries.Count > 1)
		{
			children.Add(NavigationButton("next", UiIcons.ArrowRight, () => Move(1)));
		}

		return new UiStack
		{
			Key = "navigation",
			Direction = UiComponentDirections.Horizontal,
			Align = UiComponentAlignments.Center,
			Justify = entries.Count > 1 ? UiComponentJustify.SpaceBetween : UiComponentJustify.Center,
			Gap = _gap,
			Children = children
		};
	}

	private static UiButton NavigationButton(string key, string icon, Action press)
	{
		return new UiButton
		{
			Key = key,
			Direction = UiComponentDirections.Horizontal,
			Justify = UiComponentJustify.Center,
			Align = UiComponentAlignments.Center,
			Padding = _gap,
			Events = [UiEventHandler.On(UiComponentEvents.Press, press)],
			Children =
			[
				new UiIcon { Key = "icon", Icon = icon, Size = _icon, MainSize = _icon }
			]
		};
	}

	private static UiButton OpenButton(
		List<FeedDialogEntry> entries,
		UiState<int> index,
		bool hostOpen,
		Action open)
	{
		return new UiButton
		{
			Key = "open",
			Direction = UiComponentDirections.Horizontal,
			Justify = UiComponentJustify.Center,
			Align = UiComponentAlignments.Center,
			Gap = _gap,
			Padding = _gap,
			Answer = hostOpen
				? UiValue.From(() => Current(entries, index)?.Url?.AbsoluteUri ?? string.Empty)
				: default,
			Events = hostOpen
				? new[] { UiEventHandler.On(UiComponentEvents.Press, () => { }) }
				: new[] { UiEventHandler.On(UiComponentEvents.Press, open) },
			Children =
			[
				new UiIcon { Key = "icon", Icon = UiIcons.ExternalLink, Size = _icon, MainSize = _icon },
				new UiTextRun
				{
					Key = "label",
					Text = Strings.Open(),
					Size = _text,
					Weight = UiComponentTextWeights.Medium,
					MaxLines = 1
				}
			]
		};
	}

	private static UiValue<UiResource> Image(UiResource? resource)
	{
		return resource is null ? UiValue.None<UiResource>() : resource;
	}

	private static string? ReadString(JsonElement data, string name)
	{
		return data.ValueKind == JsonValueKind.Object
			&& data.TryGetProperty(name, out var value)
			&& value.ValueKind == JsonValueKind.String
				? value.GetString()
				: null;
	}

	private static int ReadInt(JsonElement data, string name)
	{
		return data.ValueKind == JsonValueKind.Object
			&& data.TryGetProperty(name, out var value)
			&& value.ValueKind == JsonValueKind.Number
			&& value.TryGetInt32(out var number)
				? number
				: 0;
	}

	private static bool ReadBool(JsonElement data, string name)
	{
		return data.ValueKind == JsonValueKind.Object
			&& data.TryGetProperty(name, out var value)
			&& value.ValueKind == JsonValueKind.True;
	}
}
