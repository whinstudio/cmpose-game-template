# UnityBase

A Unity 6 (6000.3.25f1) starter for Android and iOS games: URP, VContainer + UniTask, uGUI with TextMeshPro,
Boot → Menu → Game scenes, settings, pause, deep links, review requests, update checks and a sample feature module.

## Open

Open the folder with Unity Hub using editor **6000.3.25f1** (Android Build Support and iOS Build Support modules).

## Layout

- `Assets/_Project/Core/` — logging, scene loading, presenters, the root DI scope, and the core modules:
  Preferences, Audio, Haptics, Lifecycle (pause), DeepLink, About, Review, Update, Localization (string tables)
- `Assets/_Project/App/` — the Boot, Menu and Game scenes' scopes and screens (Settings, About, pause, the
  "Start here" guide)
- `Assets/_Project/Render/GameRig.prefab` — the Game scene's camera and lights
- `Assets/_Project/Features/Sample/` — a feature module (Domain, Presentation, Installer); copy it to start a new one
  (each module's panel fills the Game screen; with more than one module the screen scrolls between them). The Game
  scene has a full-screen `RaycastSurface` (on `Modules/Viewport`) behind the module panels, so taps on empty space
  never reach the world; for world touches (a `PhysicsRaycaster` on the camera) turn off that surface's
  `raycastTarget` (empty space then no longer drags the module scroll), or read world touches from the Input System
  directly instead of through the UI's EventSystem
- `Assets/_Project/Tests/` — EditMode and PlayMode tests (Window → General → Test Runner)
- `Assets/Plugins/Android/CmposePlay.androidlib/` — a small Android library that calls Google Play's in-app review
  and update APIs

## What is in the box

- **Menu:** Play, Settings (music and sound effect volume, vibration and Rate the app, plus Theme and Language when
  those parts are included) and About (version, privacy and terms links, open-source licences). Settings are saved
  with PlayerPrefs.
- **Game:** a pause button at the top right opens Resume / Settings / Menu; the game also pauses (without the
  panel) when the app goes to the background. The "Start here" panel shows only in the Unity Editor (never in a
  player build) until you press "Got it". The sample's Complete button is a positive moment for review requests. The sample also counts how many times Complete was
  pressed and keeps the count across restarts.
- **Buttons** play a click sound and a light vibration (vibration on devices only).
- **Legal links:** set `privacyUrl` and `termsUrl` in `Assets/_Project/Core/About/AppConfig.asset`; a blank URL
  hides its button.

## Look: style family, fonts and the UI kit

Every screen is drawn in one **style family**: a palette, a pair of fonts (a display font for titles and buttons, a
body font for text), a corner radius, a surface (Flat, Soft shadows or Raised), a motion profile (Calm, Snappy or
Bouncy), a background (Solid, Gradient or Pattern), post-processing and the game world's colours. The applied family
lives in `Core/Presentation/Resources/Theme/Style.asset` and `Light.asset`; the six families (`bright`, `neon`,
`pastel`, `bold`, `nature`, `clean`) are described in `.cmpose/themes.json`. The bundled fonts (SIL Open Font Licence,
in `Core/Presentation/Fonts`) carry every letter of the five languages; only the applied family's two fonts enter a
build.

Build your own screens with the kit in `Core/Presentation/Kit` instead of plain uGUI parts, so they follow the family:
```csharp
var play = KitFactory.Button(parent, "Start", KitButtonVariant.Play);          // Primary, Secondary, Icon, Play
var shop = KitFactory.Button(parent, "Shop", KitButtonVariant.Secondary, sprite); // a glyph at its left
var card = KitFactory.Card(parent, "Card");                                     // also Sheet, Switch, Slider,
uiBinder.BindTree(card.gameObject);                                             // Segmented, Badge, CountUp, Decor
```
Buttons spring when pressed, panels slide and fade in (`KitScreens.Show`), and all of it runs on unscaled time, so it
also plays while the game is paused. Every text needs a `ThemedText` (its font role) and every image a
`ThemedGraphic` (its colour role); the EditMode `UiCoverageTests` check the template's screens for both.

Your game's own look goes on top of the family. Put a title logo at `Assets/_Project/Resources/Branding/title-logo.png`
(imported as a Sprite, transparent) and the menu and the boot screen show it instead of the title text; a picture at
`Branding/menu-background.png` covers the menu's background (under a light dim, in place of the drifting shapes).
`Resources/GameSkin.json` can give the boot screen a brand colour behind the logo
(`{"version":1,"brand":{"background":"#RRGGBB"}}`) and dress the buttons and panels in sprites of your own. Leave the
files out and the screens keep the family's look.

<!-- cmpose:multiLang -->
## Texts and languages

Every text on screen comes from a string table: `Resources/Strings/<language>/*.json` files with flat
`{"key": "text"}` objects (the core's in `Core/Localization`, the sample's in `Features/Sample`, translations in
`Core/Language`). A `LocalizedText` next to a TextMeshPro text names its key; an empty key means code writes the
text. In code, `ILocalizer.Get("key", args)` formats `{0}`-style arguments. English (`en`) is always there; the
game also ships Turkish, French, Spanish and Italian. The first launch picks the device language when it is one
of these, English otherwise; Settings → Language changes it (saved as `lang.code`). To add a language, copy the
`en` files of every module to a new folder (for example `de`), translate the values and add it to
`LanguageSelector.Supported`.
<!-- /cmpose:multiLang -->

<!-- cmpose:uiTheme -->
## Colours and themes

Colours come from palettes (`Resources/Theme/Light.asset` and `Dark.asset`), one colour per role: background,
surface, primary, on-primary, text, muted text, track, scrim, accent, on-accent, success, danger and the rest of
`ThemeRole`. A `ThemedGraphic` gives an image or text its
role, a `ThemedCamera` clears with the background. Settings → Theme switches between System (follows the device,
re-read when the game comes back to the foreground), Light and Dark (saved as `theme.mode`). The theme module and
its tests are in `Core/Theme/`.
<!-- /cmpose:uiTheme -->

<!-- cmpose:saveSystem -->
## Saving game state

`ISaveStore` keeps one file per slot under the app's data folder:
```csharp
public static readonly SaveSchema<Progress> Schema = new SaveSchema<Progress>("progress", 1);
var progress = store.Load(Schema) ?? new Progress();   // null: nothing saved yet, or unreadable
store.Save(Schema, progress);
```
`Progress` is a `[Serializable]` class (`JsonUtility`). Writes are atomic (a crash keeps the previous save) and the
previous save stays as a backup. When the data's shape changes, raise the version and pass an `ISaveMigration`
for each step (`From`, `To`, a function on the data JSON); a save from a newer version of the game is not loaded.
Preferences (volume, theme, language) stay in `IPreferences`. The save system and its tests are in `Core/Save/`.
<!-- /cmpose:saveSystem -->

<!-- cmpose:network -->
## Network

With the network flag the game has a small JSON client:

```csharp
public sealed class LeaderboardPresenter
{
    private readonly IApiClient api;

    public LeaderboardPresenter(IApiClient api) => this.api = api;

    public async UniTask<int?> TopScoreAsync(CancellationToken cancellation)
    {
        var result = await api.GetJson<TopScore>("scores/top", cancellation);
        if (!result.IsSuccess) return null;   // result.Error.Kind: NoConnection, Timeout, Http, Parse, Cancelled
        return result.Value.Score;
    }
}
```

- The base URL is in `Assets/_Project/Core/Network/ApiConfig.cs`: `DebugBaseUrl` (editor and development builds) and
  `ReleaseBaseUrl`. Requests time out after 15 seconds.
- `IRemoteImageLoader.LoadAsync(url)` downloads a texture and keeps the last 32 in memory; do not destroy what it
  returns.
- The sample's Game screen shows a GitHub user card (`users/octocat`) with Retry; replace it with your own API.
- With the network inspector flag, development builds show a small **NET** button: the last 50 requests with their
  status, time, headers (Authorization hidden) and the start of the body. Release builds never show it.
<!-- /cmpose:network -->

<!-- cmpose:purchases -->
## Premium and purchases

With the purchases flag the game sells premium through RevenueCat. Ask `IPurchases` (inject it) whether premium is on:

```csharp
if (purchases.IsPremium) { /* the premium feature */ }
purchases.Changed += Refresh;   // after a purchase, a restore or RevenueCat's own update
```

The paywall opens once on the first menu (closable; never for premium players) and from Settings → Premium. Open it
from your own button with `IPaywall.Show(PaywallSource.Settings)`. The paywall in
`Assets/_Project/Features/Shell/Purchases` shows the flow; redraw it for your game.

The template uses RevenueCat's **Test Store** key: development builds (`tools/unity.sh android . out.apk --development`,
or Development Build in Build Settings) list test packages and complete test purchases without a store account. Release
builds and the editor keep purchases off.

Before you send the game to the stores:
1. In RevenueCat, create the project's App Store and Google Play apps, the products, an entitlement and a current
   offering with its packages.
2. Put the public SDK keys in `Assets/_Project/Core/Purchases/PurchasesConfig.cs` (`ApiKeyApple`: `appl_…`,
   `ApiKeyGoogle`: `goog_…`) and the entitlement id in `PremiumEntitlement`.
3. Build a release and check the log: `CMPOSE_PURCHASES available=true`.
<!-- /cmpose:purchases -->

<!-- cmpose:ads -->
## Ads

With the ads flag the game shows Google AdMob ads: a banner, interstitials and rewarded ads. Inject `IAds`:

```csharp
await ads.ShowInterstitialAsync(token);            // at a natural break; the pacer may skip it
var result = await ads.ShowRewardedAsync(token);   // Rewarded or Granted: give the reward
ads.ShowBanner();                                  // and ads.HideBanner()
```

At start the game asks for consent with Google's UMP form where the law requires it, then (iOS) shows the App
Tracking Transparency prompt; Settings → Privacy choices reopens the form where it applies. Premium players
(purchases flag) see no ads and get rewards without one. Compass capture runs (flag file
`compass-autoplay`) skip consent entirely.

The template uses Google's **test** IDs, which show test ads in every build. Before you send the game to the stores:
1. In AdMob, add the Android and iOS apps with a banner, an interstitial and a rewarded ad unit each; set up the
   consent message (Privacy & messaging) and, for iOS, the IDFA explainer.
2. Put the App IDs in `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` (`adMobAndroidAppId`,
   `adMobIOSAppId`) and the ad unit IDs in `Assets/_Project/Core/Ads/AdsConfig.cs`.
3. Build a release and check the log: `CMPOSE_ADS available=true` and no "test IDs" warning.
<!-- /cmpose:ads -->

<!-- cmpose:leaderboards -->
## Leaderboards and achievements

With the leaderboards flag the game has a high-score board and achievements on Google Play Games (Android) and Game
Center (iOS). Inject `IGameServices`:

```csharp
await gameServices.SubmitScoreAsync(score, token);   // keeps your best and sends it once the player is signed in
await gameServices.UnlockAchievementAsync(AchievementKeys.FirstCompletion, token);
await gameServices.IncrementAchievementAsync(AchievementKeys.TenCompletions, 1, token);
await gameServices.ShowLeaderboardAsync(token);      // asks the player to sign in first; ShowAchievementsAsync too
```

Nothing here throws or waits for the network: at start the game only signs in silently, and anything you report
while the player is signed out or offline is kept and sent at the next sign-in. Settings → Game services shows the
state and lets the player sign in. The template ships **placeholder** IDs, so the service stays off (Settings says
"Not set up") until you set your own. Before you send the game to the stores:
1. Google Play Console → Play Games Services: set up the game (link your app and its signing SHA-1), add a
   leaderboard ("larger is better") and two achievements (one standard, one incremental with 10 steps), publish them.
2. App Store Connect → your app → Game Center: enable it, add the same leaderboard ("high to low") and the two
   achievements (the incremental one reports progress as a percentage).
3. Put the six IDs in `Assets/_Project/Core/Leaderboards/LeaderboardsConfig.cs` and the Android app id (the Play Games
   project number) in `Assets/_Project/Core/Leaderboards/Plugins/Android/CmposeGames.androidlib/src/main/res/values/games-ids.xml`.
   iOS signing needs a team with the Game Center capability; the build adds the entitlement for you.
4. Build a release and check the log: `CMPOSE_GAMES available=true signedIn=true` and no "placeholder IDs" warning.
<!-- /cmpose:leaderboards -->

## Showcase or blank

cmpose.dev/game makes two kinds of project from this template. **Blank** gives you the menu, settings and the
`Features/Sample` screen to start your own game from. **Showcase** gives you a small playable game instead, picked by
the render: with the 2D render a tap-to-jump endless runner (`Features/Showcase/Runner2D`), with the 3D render a
three-lane endless track (`Features/Showcase/Track3D`: swipe to change lanes, tap to jump over gaps, dodge the blocks,
collect gems). Both come with a game-over panel and best score, and, with the matching flags, a rewarded "Continue"
(ads; free for premium players), a Premium button that opens the paywall (purchases; premium also paints the runner or
the ball gold), a leaderboard and achievements (leaderboards) and run totals (save system). Each game's rules are plain
C# in its `Domain` folder (`RunnerSimulation` with `RunnerConfig`, `TrackSimulation` with `TrackConfig`), drawn by
`RunnerWorldView` or `TrackWorldView`; replace them with your game and keep the shared parts in
`Features/Showcase/Common`, which talk to any game through `IShowcaseRun`.

## Game shell

The Game scene (blank kind) carries the standard parts of a game screen, so a game never draws them itself: the HUD (the
score counting up, "Best: n" or "Level n", an optional counter pill for coins or lives, short toasts like "Perfect!"),
a "Tap to start" hint and the end cards. Game code asks for `IGameShell` (`Features/Shell/GameShell`) in its presenter:
```csharp
shell.Begin(ShellMode.Endless);          // or ShellMode.Levels (needs the save system)
shell.ShowStartHint();                   // hide it with HideStartHint() on the first touch
shell.SetScore(score);                   // counts up; SetBest(best), SetCounter(coins), Toast("shell.perfect")
var choice = await shell.ShowGameOver(score, best, newBest);   // Retry, Menu or Continue
```
The game acts on the choice: Retry starts a new run, Menu loads the menu, Continue revives the run (offered once per
run, only while a rewarded ad is ready or for premium players; ads part). Level games call
`shell.CompleteLevel(level, stars)` and then `ShowLevelComplete(level, stars, reward)` (the stars, 0–3, land one by
one and the reward counts up; Next or Menu) or `ShowLevelFailed(level)` (Retry, Continue or Menu); `CurrentLevel` is
the level to play and the menu's Play button reads "Level n" once the level flow has begun. Progress and stars are kept
in the save slot `levels`, so the level flow needs the save system: without it `Begin(ShellMode.Levels)` throws in the
editor and falls back to the endless HUD on a device. With ads, a finished run or level is a natural break for an
interstitial. Every card opens and animates while the game is paused. The shell's texts are in
`Features/Shell/GameShell/Resources/Strings`.

## Game world: presentation and juice

`Render/GameRig.prefab` lights and frames the world in the family's colours (a key, fill and rim light with soft
shadows, the family's backdrop and post-processing). For the world itself, use `Core/Render`:
```csharp
var mesh = RoundedMesh.Box(new Vector3(1f, 1f, 1f), 0.15f);                    // also Cylinder, Capsule
renderer.sharedMaterial = WorldPalette.Material(index, RenderMaterials.SoftGloss); // the family's world colours
CameraShake.Shake(0.3f);  SquashStretch.Play(piece.transform);  HitFlash.Flash(renderer);
ParticlePresets.Burst(position, color);  FloatingText.Spawn(position, "+10", color);
```
Materials are assets in `Core/Presentation/Resources/Materials` (Lit, Matte, SoftGloss, Glow, Unlit,
ParticlesUnlit): never create one with `Shader.Find`, a build can strip the shader. Give every player action a visible,
audible or haptic answer (`IUiFeedback.Success()`, `Failure()`, `Impact(...)`).

## Reviews and updates

### Asking for a review

Call `RecordPositiveEvent()` on `ReviewPrompter` (constructor-injected) right after a moment the player enjoyed —
a level won, a goal reached — never at launch or in the middle of play. The third such moment asks the store for its
review dialog, then every tenth. Apple and Google allow only their own dialog and decide whether it shows; the game
never learns whether the player rated it. The sample's **Complete** button
(`Features/Sample/Presentation/SamplePresenter.cs`) shows where the call goes.

**Rate the app** in Settings opens the store page: Google Play on Android; on iOS the App Store's write-a-review
page, which needs your numeric App Store ID (App Store Connect → App Information → Apple ID) in `appStoreId` in
`Assets/_Project/Core/About/AppConfig.asset`. Until it is set, the button is hidden on iOS.

### Forcing and offering updates

Host a JSON file over HTTPS and put its URL in `minVersionUrl` in `AppConfig.asset`:

```json
{ "minVersion": { "android": 12, "ios": "1.4.0" } }
```

`android` is the Bundle Version Code and `ios` the Version (Player Settings). An older build shows **Update
required** over the menu; it cannot be closed, and other scenes send the player back to it. A missing, unreachable
or broken file never forces an update. The game checks once a day when the menu first opens (on every launch while
an update is required), with a 5 second timeout, without delaying startup.

Without a forced update, iOS asks the App Store for the current version and shows **Update available** (Update /
Later) when a newer one is out; on Android, Google Play shows its own flexible update screen instead. On Android,
Update on the required panel starts Google Play's immediate update, or opens the Play Store page.

Google Play's review and update screens appear only for an app installed from Google Play: use an internal testing
track. A build installed with `adb` logs a warning and carries on. The iOS review dialog shows every time in a
debug build and never in TestFlight. The first Android build downloads the Play libraries from Google Maven, so it
needs a network connection. Unity adds the INTERNET permission because the game uses `UnityWebRequest`.

## Deep links

The URL scheme is Player Settings → iOS → Supported URL schemes, element 0 (`unitybase`); the Android build uses
the same scheme. Built-in paths: `unitybase://menu` and `unitybase://game`. Try them:

```bash
adb -s emulator-5554 shell am start -a android.intent.action.VIEW -d unitybase://game
xcrun simctl openurl booted unitybase://game
```

The iOS simulator asks "Open in …?" first; press Open.

A feature adds its own path, and gets the query parameters:

```csharp
// in a presenter's Start(); dispose the registration in Dispose()
registration = router.Register("shop", link => OpenShop(link.Query.TryGetValue("item", out var item) ? item : null));
```

`router` is the `DeepLinkRouter` from the root scope (constructor-injected).

## Build from the command line

```bash
Unity -batchmode -nographics -projectPath . -buildTarget Android \
  -executeMethod Kturker.UnityBase.Editor.Build.BuildScript.BuildAndroid -cmposeOut Builds/game.apk
Unity -batchmode -nographics -projectPath . -buildTarget iOS \
  -executeMethod Kturker.UnityBase.Editor.Build.BuildScript.BuildIOS -cmposeOut Builds/ios -cmposeSimulator
```

Android: minimum API 25, IL2CPP, ARM64; add `-cmposeAab` for a Play Store bundle. iOS: minimum 15.0; open the
generated Xcode project in `Builds/ios`. `Assets/Plugins/iOS/PrivacyInfo.xcprivacy` is added to the app as its
privacy manifest.

## Signing for the stores

Android: copy `keystore.example.properties` to `keystore.properties` (it is git-ignored) and fill in `storeFile`,
`storePassword`, `keyAlias` and `keyPassword`. The command-line build signs with it and leaves your project
settings untouched; without it the build uses the debug key, which Google Play rejects. Create the keystore once
with `keytool` (instructions in the example file) and back it up: you cannot update the app without it.

iOS: open `Builds/ios/Unity-iPhone.xcodeproj` in Xcode, pick your team under Signing & Capabilities and archive.

## Tests

```bash
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Builds/editmode.xml
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Builds/playmode.xml
```
