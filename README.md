# 🎃 BooRoute

A small .NET MAUI Halloween app built for a live GitHub Copilot demo.
Find participating houses, choose your scare level, mark private visits, and
switch to the demo-host experience to register a fictional house or change its status.

**This is a local demo, not a public neighborhood service.** Both experiences
share one asynchronous, in-app mock backend. There is no server, account verification,
cloud synchronization, or tracking. The initial stops use real public venue
addresses around Microsoft's Cologne office at Holzmarkt 2a. **These venues are
not registered Halloween hosts.** All treats, statuses, welcoming hours, scare
levels, quiet-visit flags and accessibility details are simulated; do not visit
these places for treats. Do not enter real children's information or private
home addresses.

## Run

The app targets .NET 11 using the installed MAUI preview toolchain. The SDK used
to build the demo is recorded in `global.json`; install a compatible SDK and
MAUI workloads, plus Xcode for iOS/Mac Catalyst or the Android SDK for Android.

The project sets `PublishAot` to `true` unconditionally. With the .NET 11 mobile
toolchain, Debug builds use CoreCLR.

All projects treat warnings as errors. Android NativeAOT remains experimental;
the app intentionally opts in for this demo and suppresses only its `XA1040`
warning. Trimming and AOT compatibility warnings remain enabled.

```sh
dotnet workload restore src/BooRoute/BooRoute.csproj
dotnet build src/BooRoute/BooRoute.csproj -f net11.0-ios
```

Launch from your MAUI-capable IDE, or install the built `.app` on a booted
Apple Silicon simulator:

```sh
xcrun simctl install booted src/BooRoute/bin/Debug/net11.0-ios/iossimulator-arm64/BooRoute.app
xcrun simctl launch booted com.stephanedelcroix.booroute
```

For Android, deploy through the build target so fast-deployed managed assemblies
are updated along with the APK, then cold-start the app:

```sh
dotnet build src/BooRoute/BooRoute.csproj -t:Run -f net11.0-android \
  -p:AdbTarget="-s emulator-5554"
adb -s emulator-5554 shell am force-stop com.stephanedelcroix.booroute
adb -s emulator-5554 shell monkey -p com.stephanedelcroix.booroute 1
```

Replace `emulator-5554` with the serial from `adb devices`. Installing a Debug
APK alone can leave older fast-deployed assemblies in use.

The **Map** tab uses Google Maps on
Android, so replace `YOUR_GOOGLE_MAPS_API_KEY` in
`src/BooRoute/Platforms/Android/AndroidManifest.xml` with your own key
([setup guide](https://learn.microsoft.com/dotnet/maui/user-interface/controls/map)).
iOS and Mac Catalyst use Apple Maps and need no key. The Map tab is not available on Windows.
For an APK that can be installed directly with `adb` instead of IDE fast deployment:

```sh
dotnet build src/BooRoute/BooRoute.csproj -f net11.0-android \
  -p:RuntimeIdentifier=android-arm64 -p:EmbedAssembliesIntoApk=true
```

For Mac Catalyst on Apple Silicon:

```sh
dotnet build src/BooRoute/BooRoute.csproj -t:Rebuild -f net11.0-maccatalyst \
  -p:RuntimeIdentifier=maccatalyst-arm64
open src/BooRoute/bin/Debug/net11.0-maccatalyst/maccatalyst-arm64/BooRoute.app
```

Use the Xcode version required by the installed Mac Catalyst workload. For local
preview testing with Xcode 27.1 and the .NET 11 RC2 workload that expects Xcode 27.0,
append `-p:ValidateXcodeVersion=false` to the build command. This only skips the
version check; it does not guarantee compatibility with other Xcode versions.
The iOS and Mac apps use MAUI's scene lifecycle, as required by the Xcode 27 SDK, while
retaining a single window.
When changing Apple platform `Info.plist` files, use `-t:Rebuild` to ensure the
updated metadata is packaged by the preview toolchain.

`ValidateXcodeVersion` is set to `false` to allow the demo to build with Xcode 27.1
while the installed iOS workload expects 27.0. This bypasses only the version check;
the mismatched toolchain remains unsupported and other build errors may still occur.

## App artwork

The app icon and native launch splash share the supplied MAUI DAY artwork in
`src/BooRoute/Resources/Splash/maui_day.png`. The original opaque PNG is retained
without cropping, recoloring, or changing its lettering. `MauiIcon` places it over
a dark background, with an Android-only foreground scale of `0.6` to keep the wide
lettering inside adaptive-icon masks. `MauiSplashScreen` resizes it proportionally
to a 192-by-192 logical-pixel base size on the same dark background. There is no
separate in-app image logo.
## MAUI DevFlow

Debug builds include [MAUI DevFlow](https://github.com/dotnet/maui-labs/tree/main/src/DevFlow)
for visual-tree inspection, UI automation, screenshots, and app diagnostics.
The agent and repository-local `Microsoft.Maui.Cli` tool are pinned to
`0.1.0-preview.13.26512.5`, the latest published preview at setup time.
This is the current `Microsoft.Maui.*` tooling, not the archived `Redth.MauiDevFlow.*` packages.

Restore the CLI from the repository root; no global tool installation is needed:

```sh
dotnet tool restore
dotnet tool run maui -- devflow version
```

Build and launch a **Debug** app using the Run instructions above. Run DevFlow
commands from the app directory so discovery can identify the correct project:

```sh
cd src/BooRoute
dotnet tool run maui -- devflow diagnose
dotnet tool run maui -- devflow wait
dotnet tool run maui -- devflow agent status
dotnet tool run maui -- devflow ui tree --depth 8
```

The broker discovers the agent's dynamic port automatically. If multiple app
instances are running, use `dotnet tool run maui -- devflow list` to find the
desired platform's port and pass `--agent-port <port>` to inspection commands.
`--platform` is not an agent-selection filter.
Mac Catalyst keeps its sandbox enabled and adds the network-server entitlement only in Debug.
Release builds contain neither the DevFlow package nor its startup registration
or additional entitlement.

Mac Catalyst is the verified runtime target for this integration. The iOS app
builds, but its existing startup configuration lacks the UIKit scene lifecycle
required on iOS 27/27.1, so the agent cannot start there until that separate
startup issue is resolved.

For Android, forward the broker before launching the app, then forward the agent
port reported by `devflow list`:

```sh
adb reverse tcp:19223 tcp:19223
adb forward tcp:<agent-port> tcp:<agent-port>
```

The CLI's bundled GitHub Copilot skills are checked in under `.github/skills`.
After updating the CLI version in `dotnet-tools.json`, refresh them from the
repository root with:

```sh
dotnet tool run maui -- devflow init --scope project --target github --ci
```

Clients that support MCP can launch `dotnet tool run maui -- devflow mcp` from
the app directory as a stdio server. No MCP connection is required for the CLI
or skills. DevFlow can inspect and change app state; use it only with local
development builds and fictional demo data.

## PR review demo

The [GPT-only inline review workflow](docs/demo-review.md) provides small,
nonblocking demo comments on PRs, with an explicitly non-AI fallback when needed.
It is a presentation aid, not a correctness or security gate.

## Try the demo

1. Browse **Explore**, turn on **Welcoming visitors now**, and try scare-level or quiet-visit filters.
2. Open a house and mark it visited for a short pumpkin-confetti celebration, then
   find it under **My visits**. The pumpkins only fly after a successful save, not
   when undoing a visit, and respect the device's reduced-motion setting.
3. Switch to **Demo host**, edit that house, and change its status to **Out of treats**.
4. Return to Explore: it no longer appears under welcoming-only filtering.
5. Open **Map** to see every stop from the list as a pin at its address. Tap a pin's
   callout to open its details. Addresses are looked up with the platform geocoder
   (Apple Maps or Google), so the map needs a network connection; houses whose
   address can't be found are left off the map and counted in the summary.
5. Register another fictional house. Restart the app to see that houses and visits persist.

Ten houses are seeded on first use. Their initial daily welcoming windows are
relative to the first launch time, so the demo works during the day or across midnight.
Existing saved neighborhoods, including older fictional addresses, are preserved;
upgrading does not overwrite houses or visits. Use a fresh installation to see
the Cologne seed.
The closing boundary is exclusive. Status and time both determine availability.
The foreground list refreshes every 30 seconds, on appearance, on resume, and
after successful mock-backend changes.

Data is saved to `neighborhood.json` in the app's private data directory using
atomic file replacement. Both reads and writes use source-generated
`System.Text.Json` metadata so persistence does not require reflection under NativeAOT.
Errors are displayed rather than replacing corrupt data
with a fresh success-shaped dataset. To start over, uninstall and reinstall the
app (this deletes its local demo data).

### Cologne demo locations

The public venues below provide recognizable geography for the live demo, not
an actual trick-or-treat route. Address sources are linked for verification.
Museum Schnütgen and the Rautenstrauch-Joest-Museum share an entrance address.

| Public venue | Address in Köln | Source |
|---|---|---|
| Microsoft Cologne | Holzmarkt 2a, 50676 | [Microsoft](https://news.microsoft.com/de-de/standorte-directions/) |
| art'otel Cologne | Holzmarkt 4, 50676 | [Hotel](https://artotel.com/en/hotels/cologne/central-cologne/contact/) |
| Schokoladenmuseum | Am Schokoladenmuseum 1a, 50678 | [Museum](https://www.schokoladenmuseum.de/impressum/) |
| Deutsches Sport & Olympia Museum | Im Zollhafen 1, 50678 | [Museum](https://sportmuseum.de/) |
| Trinitatiskirche | Filzengraben 4, 50676 | [Venue](https://www.trinitatiskirche-koeln.de/service-kontakt/wegbeschreibung/) |
| NH Köln Altstadt | Holzmarkt 47, 50676 | [Hotel](https://www.nh-hotels.com/en/hotel/nh-koeln-altstadt) |
| Motel One Köln-Altstadt | Tel-Aviv-Straße 6, 50676 | [Hotel](https://www.motel-one.com/de/hotels/koeln/hotel-koeln-altstadt/) |
| Wasserturm Hotel Cologne | Kaygasse 2, 50676 | [Hotel](https://www.wasserturm-hotel-cologne.com/en/contacts-location) |
| Museum Schnütgen | Cäcilienstraße 29-33, 50667 | [Museum](https://www.museum-schnuetgen.de/) |
| Rautenstrauch-Joest-Museum | Cäcilienstraße 29-33, 50667 | [City of Cologne](https://www.stadt-koeln.de/service/adressen/00142/index.html) |

## Adaptive entry collections

Explore, My visits, and Demo host share a native-virtualized `CollectionView`.
Following the [bespoke collections skill's scenario-selection guidance](https://github.com/dotnet/maui-labs/blob/redth-bespoke-collection-research/plugins/dotnet-maui/skills/maui-bespoke-collections/references/scenario-selection.md),
this regular grid retains MAUI's existing platform handlers rather than adding
a custom scrolling engine or handler registration.

Columns follow the collection's available width, not the device model. Cards
have a 320-DIP minimum width, 14-DIP column gaps, and 22-DIP outer insets. Narrow
windows use one column; larger windows add columns as space permits, including
after rotation or window resizing. Cards remain self-sized so wrapped names,
addresses, visit indicators, and automatically scaled text are not clipped to
the first card's height.

Refreshes reconcile entries by house ID instead of clearing the list. Unchanged
cards retain their identity and cached presentation strings; edits, filtering,
visits, and welcoming-time transitions emit targeted collection changes.
Overlapping refreshes are serialized. Item bindings use typed C# getters, and
the collection remains the page's finite, directly hosted scrolling viewport.
The mock backend still supplies a complete in-memory snapshot; this is not a
paged data source.

The portable regression suite verifies that an unchanged 10,000-house refresh
emits **zero collection notifications** and retains every card instance. It also
covers individual changes, reordering, empty sources, duplicate keys, and exact
column breakpoints. These are update-work checks, not a scrolling FPS benchmark.

The implementation builds for iOS, Mac Catalyst, and Android with the existing
preview toolchain. Native behavior was checked on iOS 18.6 simulators: one column
on iPhone, two columns on portrait iPad, three after landscape rotation, and
details navigation plus visit updates on the intended grid entry. The installed
iOS 27 simulator rejects the existing iOS scene-lifecycle configuration at
startup; that separate migration is not part of this change. Mac Catalyst and
Android runtime behavior, Windows, full disconnect/reconnect and focus testing,
physical-device performance, allocation bounds, and frame/hitch measurements
remain unverified.

## Canadian English and French

Open the toolbar gear in **Explore**, **My visits**, or **Demo host**, or the
existing **Settings** tab, then choose **Use device settings**,
**English (Canada)**, or **Français (Canada)**, then apply. Navigation, pages,
map labels, validation and regional time formatting update immediately; the
choice persists across restarts. Settings becomes **Réglages** in French.
Applying the setting updates existing pages in place: navigation stacks, unsaved
editor fields, selected filters, map viewport, artwork, and safe areas are retained.
Device mode rereads the system/per-app language and regional settings on return
to device mode and when the app resumes.
Canadian English is the English fallback; French resources cover `fr-CA` and
other French locales. Android 13+ and iOS also expose system per-app language
selection; the explicit in-app selection takes precedence.

[Shiny LocalizeGen](https://shinylib.net/localizegen/) generates typed accessors
and DI registration from `src/BooRoute.Core/Localization/AppText.resx` and
`AppText.fr.resx`. Keep both files' keys and format arguments in sync. Navigation,
filters, statuses, accessibility descriptions, validation, and errors use these
resources; enum values and the `neighborhood.json` format remain unchanged.

New demo stops retain their real Cologne venue names and addresses, with a
description in the first-launch language. Saved names, addresses and descriptions
are editable data: changing language does not translate or overwrite those fields,
reset visits, or reseed data. The no-participation and simulated-accessibility
warnings remain visible in both languages.

House cards and native time-picker fields use the selected locale's short-time
formatter. All three choices preserve the native user's forced 12/24-hour preference,
including native time-picker dialogs, without changing device language or timezone.
Welcoming rules still use device-local wall time for this single-device demo.
Timezone coverage includes Newfoundland's half-hour offset, the autumn clock
change, Saskatchewan, and Yukon. A future multi-region service would need each
house's timezone, not just the viewer's device timezone.

## Structure

- `src/BooRoute`: native MAUI pages, MVVM view models, and dependency injection.
- `src/BooRoute.Core`: models, welcoming/filter rules, mock backend, and JSON persistence.
- `tests/BooRoute.Core.Tests`: clock-controlled business-rule, persistence, and
  portable entry-presentation/grid tests (linked from the app's actual source).
- `docs/initial-prompt.txt`: the prompt used to start the build.

```sh
dotnet test tests/BooRoute.Core.Tests/BooRoute.Core.Tests.csproj
```

Verified for this initial version: the iOS simulator build, launch, welcoming/scare/quiet
filters, private visit tracking, demo-host status changes, registration validation,
and persistence after process restart. The portable suite covers resource
parity/fallback, pluralization, Unicode, cross-language persistence, and Canadian
timezone cases. Mac Catalyst startup
has also been verified on Apple Silicon with Xcode 27.1 using the preview version
check override above. The localized app also builds for iOS, Android (Debug and
Release), and Mac Catalyst; French startup has been checked on an iOS simulator and
an Android API 35 emulator, including the trimmed Android Release package.
Windows and physical devices have not been verified.

The service boundary can later be replaced with a real backend. Real authentication,
host ownership, moderation, consent, and safe address sharing would be necessary
before turning this into a public service.
