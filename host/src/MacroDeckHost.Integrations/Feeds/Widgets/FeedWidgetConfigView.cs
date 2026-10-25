using System.Text.Json;
using MacroDeck.Ui.Config;
using MacroDeck.Ui.Config.Options;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Runtime;
using MacroDeckHost.Integrations.Feeds.State;
using Strings = MacroDeckHost.Localization.AppStrings.Widgets.Feeds;

namespace MacroDeckHost.Integrations.Feeds.Widgets;

internal static class FeedWidgetConfigView
{
	public static UiElement Build(FeedWidgetKind kind, JsonElement data, IReadOnlyList<FeedSnapshot> feeds)
	{
		ArgumentNullException.ThrowIfNull(feeds);

		var options = FeedWidgetOptions.Parse(kind, data);
		var source = new UiState<string>(options.Source);
		var feed = new UiState<string>(options.FeedId?.ToString("D") ?? string.Empty);
		var group = new UiState<string>(options.Group ?? string.Empty);
		var count = new UiState<double>(options.Count);
		var skip = new UiState<double>(options.Skip);
		var textScale = new UiState<double>(options.TextScale);
		var imageScale = new UiState<double>(options.ImageScale);
		var showDescription = new UiState<bool>(options.ShowDescription);
		var showThumbnail = new UiState<bool>(options.ShowThumbnail);
		var cycleSeconds = new UiState<double>(options.CycleSeconds);
		var tap = new UiState<string>(options.Tap);
		var openTarget = new UiState<string>(options.OpenTarget);
		var hostBrowser = new UiState<string>(options.HostBrowser);
		var hostPrivate = new UiState<bool>(options.HostPrivate);
		var hostBrowserPath = new UiState<string>(options.HostBrowserPath ?? string.Empty);
		var flows = new UiState<JsonElement>(FeedWidgetOptions.ReadFlows(data));

		var browsers = HostBrowsers.Detect();
		var groups = feeds.Select(candidate => candidate.Group)
			.OfType<string>()
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Order(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var children = new List<UiElement>
		{
			new UiHeading
			{
				Key = "heading",
				Text = kind == FeedWidgetKind.List ? Strings.FeedList.Name() : Strings.Featured.Name()
			},
			new UiChoiceInput
			{
				Key = FeedWidgetTypes.SourceKey,
				Label = Strings.Config.Source(),
				Binding = Bind.To(source),
				Options = UiValue.Of<IReadOnlyList<UiOption>>([
					UiOption.Of(FeedWidgetTypes.AllSource, Strings.Config.SourceAll()),
					UiOption.Of(FeedWidgetTypes.FeedSource, Strings.Config.SourceFeed()),
					UiOption.Of(FeedWidgetTypes.GroupSource, Strings.Config.SourceGroup())
				])
			},
			new UiChoiceInput
			{
				Key = FeedWidgetTypes.FeedKey,
				Label = Strings.Config.Feed(),
				Binding = Bind.To(feed),
				Options = UiValue.Of<IReadOnlyList<UiOption>>([
					.. feeds.Select(candidate => UiOption.Of(candidate.EntryId.ToString("D"), candidate.Title))
				]),
				VisibleWhen = new UiVisibleWhen
				{
					ParameterName = FeedWidgetTypes.SourceKey,
					Values = [FeedWidgetTypes.FeedSource],
					SiblingValue = () => source.Value
				}
			},
			new UiChoiceInput
			{
				Key = FeedWidgetTypes.GroupKey,
				Label = Strings.Config.Group(),
				Binding = Bind.To(group),
				Options = UiValue.Of<IReadOnlyList<UiOption>>([.. groups.Select(name => UiOption.Of(name, name))]),
				VisibleWhen = new UiVisibleWhen
				{
					ParameterName = FeedWidgetTypes.SourceKey,
					Values = [FeedWidgetTypes.GroupSource],
					SiblingValue = () => source.Value
				}
			},
			new UiNumberInput
			{
				Key = FeedWidgetTypes.CountKey,
				Label = kind == FeedWidgetKind.List ? Strings.Config.Count() : Strings.Config.FeaturedCount(),
				Description = kind == FeedWidgetKind.List
					? Strings.Config.PageCountDescription()
					: Strings.Config.FeaturedCountDescription(),
				Binding = Bind.To(count),
				Min = 1,
				Max = FeedWidgetTypes.MaxCount,
				Step = 1
			},
			new UiProse
			{
				Key = "totalArticles",
				Text = UiText.From(() => $"Total articles: {TotalArticles(feeds, source.Value, feed.Value, group.Value)}")
			},
			new UiNumberInput
			{
				Key = FeedWidgetTypes.SkipKey,
				Label = Strings.Config.Skip(),
				Description = Strings.Config.SkipDescription(),
				Binding = Bind.To(skip),
				Min = 0,
				Max = FeedWidgetTypes.MaxSkip,
				Step = 1
			},
			new UiNumberInput
			{
				Key = FeedWidgetTypes.TextScaleKey,
				Label = Strings.Config.TextScale(),
				Description = Strings.Config.TextScaleDescription(),
				Binding = Bind.To(textScale),
				Min = FeedWidgetTypes.MinTextScale,
				Max = FeedWidgetTypes.MaxTextScale,
				Step = 5,
				ShowSlider = true
			},
			new UiBooleanInput
			{
				Key = FeedWidgetTypes.ShowDescriptionKey,
				Label = Strings.Config.ShowDescription(),
				Binding = Bind.To(showDescription)
			},
			new UiBooleanInput
			{
				Key = FeedWidgetTypes.ShowThumbnailKey,
				Label = Strings.Config.ShowThumbnail(),
				Binding = Bind.To(showThumbnail)
			}
		};

		if (kind == FeedWidgetKind.List)
		{
			children.Add(new UiNumberInput
			{
				Key = FeedWidgetTypes.ImageScaleKey,
				Label = Strings.Config.ImageScale(),
				Description = Strings.Config.ImageScaleDescription(),
				Binding = Bind.To(imageScale),
				Min = FeedWidgetTypes.MinImageScale,
				Max = FeedWidgetTypes.MaxImageScale,
				Step = 5,
				ShowSlider = true
			});
		}

		children.Add(new UiNumberInput
		{
			Key = FeedWidgetTypes.CycleSecondsKey,
			Label = kind == FeedWidgetKind.List ? Strings.Config.PageSeconds() : Strings.Config.CycleSeconds(),
			Description = kind == FeedWidgetKind.List
				? Strings.Config.PageSecondsDescription()
				: Strings.Config.CycleSecondsDescription(),
			Binding = Bind.To(cycleSeconds),
			Min = 0,
			Max = FeedWidgetTypes.MaxCycleSeconds,
			Step = 1
		});

		children.Add(new UiChoiceInput
		{
			Key = FeedWidgetTypes.TapKey,
			Label = Strings.Config.Tap(),
			Description = Strings.Config.TapDescription(),
			Binding = Bind.To(tap),
			Options = UiValue.Of<IReadOnlyList<UiOption>>([
				UiOption.Of(FeedWidgetTypes.OpenTap, Strings.Config.TapOpen()),
				UiOption.Of(FeedWidgetTypes.DialogTap, Strings.Config.TapDialog()),
				UiOption.Of(FeedWidgetTypes.FlowsTap, Strings.Config.TapFlows())
			])
		});
		children.Add(new UiChoiceInput
		{
			Key = FeedWidgetTypes.OpenTargetKey,
			Label = Strings.Config.OpenTarget(),
			Description = Strings.Config.OpenTargetDescription(),
			Binding = Bind.To(openTarget),
			Options = UiValue.Of<IReadOnlyList<UiOption>>([
				UiOption.Of(FeedWidgetTypes.ClientTarget, Strings.Config.OpenTargetClient()),
				UiOption.Of(FeedWidgetTypes.HostTarget, Strings.Config.OpenTargetHost())
			])
		});

		var browserOptions = new List<UiOption>
		{
			UiOption.Of(FeedWidgetTypes.DefaultBrowser, Strings.Config.HostBrowserDefault())
		};
		browserOptions.AddRange(browsers.Select(browser => UiOption.Of(browser.Id, browser.Name)));
		browserOptions.Add(UiOption.Of(FeedWidgetTypes.CustomBrowser, Strings.Config.HostBrowserCustom()));
		if (options.HostBrowser is not (FeedWidgetTypes.DefaultBrowser or FeedWidgetTypes.CustomBrowser)
			&& browsers.All(browser => browser.Id != options.HostBrowser))
		{
			browserOptions.Add(UiOption.Of(options.HostBrowser, options.HostBrowser));
		}

		children.Add(new UiChoiceInput
		{
			Key = FeedWidgetTypes.HostBrowserKey,
			Label = Strings.Config.HostBrowser(),
			Description = Strings.Config.HostBrowserDescription(),
			Binding = Bind.To(hostBrowser),
			Options = UiValue.Of<IReadOnlyList<UiOption>>(browserOptions),
			VisibleWhen = new UiVisibleWhen
			{
				ParameterName = FeedWidgetTypes.OpenTargetKey,
				Values = [FeedWidgetTypes.HostTarget],
				SiblingValue = () => openTarget.Value
			}
		});
		children.Add(new UiFileInput
		{
			Key = FeedWidgetTypes.HostBrowserPathKey,
			Label = Strings.Config.HostBrowserPath(),
			Description = Strings.Config.HostBrowserPathDescription(),
			Binding = Bind.To(hostBrowserPath),
			FileExtensions = UiValue.Of<IReadOnlyList<string>>(["exe"]),
			VisibleWhen = new UiVisibleWhen
			{
				ParameterName = FeedWidgetTypes.HostBrowserKey,
				Values = [FeedWidgetTypes.CustomBrowser],
				SiblingValue = () => hostBrowser.Value
			}
		});

		var privateBrowsers = browsers
			.Where(browser => browser.SupportsPrivate)
			.Select(browser => browser.Id)
			.ToList();
		if (privateBrowsers.Count > 0)
		{
			children.Add(new UiBooleanInput
			{
				Key = FeedWidgetTypes.HostPrivateKey,
				Label = Strings.Config.HostPrivate(),
				Description = Strings.Config.HostPrivateDescription(),
				Binding = Bind.To(hostPrivate),
				VisibleWhen = new UiVisibleWhen
				{
					ParameterName = FeedWidgetTypes.HostBrowserKey,
					Values = privateBrowsers,
					SiblingValue = () => hostBrowser.Value
				}
			});
		}

		children.Add(UiWidgetAppearance.Section(data,
			UiWidgetAppearanceFields.Border
			| UiWidgetAppearanceFields.BackgroundColor
			| UiWidgetAppearanceFields.TransparentBackground));

		return new UiWidgetConfiguration
		{
			Key = "root",
			Properties = new UiWidgetProperties { Key = "properties", Children = children },
			Editor = new UiWidgetEditor
			{
				Key = "editor",
				Children =
				[
					new UiActionsListEditor
					{
						Key = FeedWidgetTypes.FlowsKey,
						Binding = Bind.To(flows),
						CanRun = true
					}
				]
			}
		};
	}

	private static int TotalArticles(
		IReadOnlyList<FeedSnapshot> feeds,
		string source,
		string feed,
		string group)
	{
		if (source == FeedWidgetTypes.FeedSource)
		{
			return Guid.TryParse(feed, out var feedId)
				? feeds.Where(candidate => candidate.EntryId == feedId).Sum(candidate => candidate.Items.Count)
				: 0;
		}

		if (source == FeedWidgetTypes.GroupSource)
		{
			return feeds
				.Where(candidate => string.Equals(candidate.Group, group, StringComparison.OrdinalIgnoreCase))
				.Sum(candidate => candidate.Items.Count);
		}

		return feeds.Sum(candidate => candidate.Items.Count);
	}
}
