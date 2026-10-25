# Unofficial Macro Deck feeds fork

This is a personal, unofficial fork of [Macro Deck](https://github.com/Macro-Deck-App/Macro-Deck).
It adds features to the built-in RSS/Atom feeds integration. It is not an official Macro Deck
release, not affiliated with or endorsed by the original project, and not supported by its
maintainers.

## Credit and attribution

- **Original project:** Macro Deck, https://github.com/Macro-Deck-App/Macro-Deck
- **Original author and contributors:** Manuel Mayer and the Macro Deck Contributors.
- **License:** Apache License 2.0. The original `LICENSE`, `NOTICE` and `THIRD-PARTY-NOTICES`
  files are retained in this fork.
- **Branding:** the Macro Deck name, logo and branding assets are not licensed under Apache-2.0.
  They are copyright Radioactive.exe and licensed exclusively to the Macro Deck project. This
  fork does not claim the Macro Deck branding. The original logo was removed from this fork's
  README to respect that. The repository name is inherited from the upstream fork and is not a
  claim to the brand.
- **Upstream status:** this fork has not pushed to the upstream repository and has not opened a
  pull request. The upstream project is not involved in these changes.
- **Maintainer of this fork:** tenggono98.

The features below are additions made in this fork. The rest of the project is the original
Macro Deck code, documentation and assets, used under the Apache License 2.0.

## Features added in this fork

### Feed List paging

- Swipe the Feed List up/down or left/right to move between pages.
- A `1/3` page counter appears in the widget header when there is more than one page.
- Reuses the existing **Next page after (seconds)** setting to advance pages automatically.
- The Featured Article widget keeps its original article cycling behavior.

### Start at article

- A per-widget **Start at article** offset skips the newest N articles before the first page.
- Useful for feeds that always begin with a pinned item such as "Upcoming Repacks".

### Text and image sizes

- **Text size (%)** scales the article title, meta and description from 50% to 250%.
- **Image size (%)** scales the Feed List thumbnails from 50% to 300%.
- Both default to 100%, so existing widgets look unchanged.

### Total Articles

- `FeedWidgetState` now carries the raw number of stored articles for the selected feed,
  group or all feeds.
- The Feed List header shows `1/2 · 10`: page 1 of 2, 10 total articles.
- The widget settings show `Total articles: N` for the selected source.
- The total is the raw stored count before **Start at article**, so a skipped pinned item is
  included. The page counter still reflects the pages after the skip.

### Open links on the host

- Each widget can choose **Open links on: This device** or **This computer**. The default
  remains the tapped device, preserving the original behavior.
- **This computer** can use the host's default browser or a detected browser: Firefox,
  Microsoft Edge, Google Chrome, Brave, Opera or Vivaldi. **Other browser** accepts an
  executable path for anything else.
- A **Private window** option opens the browser's private or incognito window where supported.
  The command-line flags used are:
  - Firefox: `-new-tab <url>` normally, `-private-window <url>` privately.
  - Microsoft Edge: `<url>` normally, `--inprivate <url>` privately.
  - Chrome, Brave, Vivaldi: `<url>` normally, `--incognito <url>` privately.
  - Opera: `<url>` normally, `--private <url>` privately.
- A normal link focuses the existing browser window and opens a new tab. If the browser is not
  running, it starts with the article.
- In the details dialog, host mode settles the dialog with an `open` answer, so the dialog closes
  before the host browser starts. Client mode keeps the original behavior and keeps the dialog
  open after opening on the tapped device.
- If the host browser cannot start, the device shows a failure message.

### Details dialog navigation

- The article details dialog shows the current position in the widget's list, for example `3 / 9`.
- Previous and next buttons swap the article in place without closing and reopening the dialog.
- The dialog loads the image for the article currently shown, including an article that is not on the
  widget's current page.
- The position counts the list after **Start at article**.
- In host mode, **Open** uses the article currently shown and closes the dialog before launching the
  browser.

## Validation

- Feed tests: 86 passed.
- Shipped localization catalog tests: 20 passed.
- Full solution build with warnings as errors: 0 warnings, 0 errors.
- Browser command-line argument tests for Firefox, Microsoft Edge and Chrome, for normal and
  private modes. The host-open path is wired into the installed build; tap an article with host
  mode selected to verify the browser and the dialog close on your machine.

## Automatic build and install

Run `Update-MacroDeck.cmd` from the repository root, or run
`tools\Update-MacroDeck.ps1` from PowerShell. The script:

- builds Release with warnings as errors;
- runs the Feeds tests;
- publishes the Production `win-x64` host;
- backs up the current data and the installed custom host;
- installs the new host as `MacroDeckHostCustom.exe`;
- restarts Macro Deck and waits for the host to become ready.

Use `-SkipTests` for a faster update, or `-BuildOnly` to stop after publishing. The script is
Windows-only because this custom host setup is Windows-only. Backups and the update log are under
`%LOCALAPPDATA%\MacroDeckCustomBuild`.

## Build

This is a source fork, not a separate installer or release. Use the upstream project's setup and
build documentation. The fork was developed against tag `v3.0.0-beta.19` and the changes are in
the feeds integration under `host/src/MacroDeckHost.Integrations/Feeds/`.

```bash
dotnet build MacroDeck.slnx -c Release -warnaserror
dotnet test host/tests/MacroDeckHost.Tests.UnitTests -c Release --filter "FullyQualifiedName~Feeds"
```

## Commits in this fork

- `f845f4f` Add feed list paging and display options
- `59e8359` Show total articles in feed list
- `6a489cb` Open feed articles in a host browser

## Notes and limitations

- This is a personal fork for hobby use. There is no support, update guarantee or warranty.
- Official Macro Deck updates may overwrite a custom host build. Rebuild from this branch after
  an update if you use the fork's host.
- The localization files contain English placeholders for the new labels in non-English
  catalogs. The original project's translations are otherwise unchanged.
- The separate private/incognito mode is only implemented for the detected browsers listed above.
  A custom browser executable gets the URL but no private flag.

## Thank you

Thank you to Manuel Mayer and the Macro Deck Contributors for the original project, its
documentation, assets and the Apache-2.0 licensed code this fork builds on.
