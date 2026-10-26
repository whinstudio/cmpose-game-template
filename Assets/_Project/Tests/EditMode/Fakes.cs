using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.App.About;
using Kturker.UnityBase.App.Game;
using Kturker.UnityBase.App.Menu;
using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.App.Update;
using Kturker.UnityBase.Core.About;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Navigation;
using Kturker.UnityBase.Core.Preferences;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Core.Review;
using Kturker.UnityBase.Core.Update;
using Kturker.UnityBase.Features.Economy;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class FakeLog : ILog
    {
        public readonly List<string> Lines = new List<string>();
        public void Debug(string message) => Lines.Add("DEBUG " + message);
        public void Info(string message) => Lines.Add("INFO " + message);
        public void Warning(string message) => Lines.Add("WARNING " + message);
        public void Error(string message, Exception exception = null) => Lines.Add("ERROR " + message);
        public void Marker(string line) => Lines.Add("MARKER " + line);
    }

    public sealed class FakeOverlay : ILoadingOverlay
    {
        public int Shown;
        public bool Visible;
        // Runs as the overlay hides: what the screen shows from its first visible frame
        public Action OnHide;
        public void Show() { Shown++; Visible = true; }
        public void Hide()
        {
            Visible = false;
            OnHide?.Invoke();
        }
    }

    public sealed class FakeGateway : ISceneGateway
    {
        public readonly List<string> Loaded = new List<string>();
        public UniTaskCompletionSource Pending;
        public Exception Failure;

        public async UniTask LoadAsync(string sceneName, CancellationToken cancellation)
        {
            Loaded.Add(sceneName);
            if (Failure != null) throw Failure;
            if (Pending != null) await Pending.Task;
        }
    }

    // Completes synchronously; the first Failures requests fail like a scene missing from Build Settings
    public sealed class FakeSceneLoader : ISceneLoader
    {
        public readonly List<SceneId> Requested = new List<SceneId>();
        public int Failures;
        public Action<SceneId> OnLoad;
        public bool IsLoading => false;
        public event Action<SceneId> Revealing;
        public event Action<SceneId> Loaded;

        public UniTask LoadAsync(SceneId id, CancellationToken cancellation = default)
        {
            Requested.Add(id);
            OnLoad?.Invoke(id);
            if (Failures > 0)
            {
                Failures--;
                return UniTask.FromException(new InvalidOperationException($"Scene '{id}' is not in Build Settings"));
            }
            Revealing?.Invoke(id);
            Loaded?.Invoke(id);
            return UniTask.CompletedTask;
        }
    }

    public sealed class FakeBootErrorView : IBootErrorView
    {
        public int Shown;
        public string Message;
        public string Detail;
        public Action Retry;

        public void Show(string messageKey, string detail, Action onRetry)
        {
            Shown++;
            Message = messageKey;
            Detail = detail;
            Retry = onRetry;
        }
    }

    public sealed class FakeStep : IBootStep
    {
        private readonly List<string> order;
        private int failuresLeft;

        public FakeStep(string name, List<string> order, int failures = 0)
        {
            Name = name;
            this.order = order;
            failuresLeft = failures;
        }

        public string Name { get; }
        public int Runs { get; private set; }

        // Awaited by runs after the configured failures, to keep a retry pending
        public UniTaskCompletionSource Gate;

        // Throws OperationCanceledException, as a step does when the scope's token is cancelled
        public bool CancelOnRun;

        // Fails once after Gate completes, i.e. the returned task faults asynchronously
        public bool FailAfterGate;

        public async UniTask RunAsync(CancellationToken cancellation)
        {
            Runs++;
            order.Add(Name);
            if (CancelOnRun) throw new OperationCanceledException();
            if (failuresLeft > 0)
            {
                failuresLeft--;
                throw new InvalidOperationException(Name + " failed");
            }
            if (Gate != null) await Gate.Task;
            if (FailAfterGate)
            {
                FailAfterGate = false;
                throw new InvalidOperationException(Name + " faulted");
            }
        }
    }

    public sealed class FakeMenuView : IMenuView
    {
        public string Title;
        public string Version;
        public List<string> GameKeys;
        public (int Index, int Level)? LevelShown;
        public event Action<int> GameClicked;
        public event Action SettingsClicked;
        public void SetTitle(string text) => Title = text;
        public void SetVersion(string text) => Version = text;
        public void ShowGames(IReadOnlyList<string> labelKeys, UiBinder binder) => GameKeys = new List<string>(labelKeys);
        public void ShowLevel(int index, int level) => LevelShown = (index, level);
        public void ClickGame(int index) => GameClicked?.Invoke(index);
        public void ClickSettings() => SettingsClicked?.Invoke();
    }

    // Typed in-memory store: a value of another type reads as the default, like a key PlayerPrefs cannot read
    public sealed class FakePreferences : IPreferences
    {
        public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
        public int Saves;

        public bool GetBool(string key, bool defaultValue) => Values.TryGetValue(key, out var v) && v is bool b ? b : defaultValue;
        public void SetBool(string key, bool value) => Values[key] = value;
        public int GetInt(string key, int defaultValue) => Values.TryGetValue(key, out var v) && v is int i ? i : defaultValue;
        public void SetInt(string key, int value) => Values[key] = value;
        public float GetFloat(string key, float defaultValue) => Values.TryGetValue(key, out var v) && v is float f ? f : defaultValue;
        public void SetFloat(string key, float value) => Values[key] = value;
        public string GetString(string key, string defaultValue) => Values.TryGetValue(key, out var v) && v is string s ? s : defaultValue;
        public void SetString(string key, string value) => Values[key] = value;
        public void Save() => Saves++;
    }

    public sealed class FakeAudioPlayer : Kturker.UnityBase.Core.Audio.IAudioPlayer
    {
        public int SfxPlayed;
        public bool MusicPaused;
        public int MusicPauses;
        public int MusicResumes;
        public void PlayMusic(UnityEngine.AudioClip clip) { }
        public void StopMusic() { }
        public void PauseMusic() { MusicPaused = true; MusicPauses++; }
        public void ResumeMusic() { MusicPaused = false; MusicResumes++; }
        public void PlaySfx(UnityEngine.AudioClip clip) => SfxPlayed++;
    }

    public sealed class FakeVibrator : Kturker.UnityBase.Core.Haptics.IVibrator
    {
        public readonly List<Kturker.UnityBase.Core.Haptics.HapticStrength> Played = new List<Kturker.UnityBase.Core.Haptics.HapticStrength>();
        public readonly List<Kturker.UnityBase.Core.Haptics.HapticEvent> Events = new List<Kturker.UnityBase.Core.Haptics.HapticEvent>();
        public void Vibrate(Kturker.UnityBase.Core.Haptics.HapticStrength strength) => Played.Add(strength);
        public void Vibrate(Kturker.UnityBase.Core.Haptics.HapticEvent e) => Events.Add(e);
    }

    public sealed class FakeTimeScale : Kturker.UnityBase.Core.Lifecycle.ITimeScale
    {
        public float Scale { get; set; } = 1f;
    }

    public sealed class FakeUiFeedback : IUiFeedback
    {
        public int Clicks;
        public int Successes;
        public int Failures;
        public readonly List<Kturker.UnityBase.Core.Haptics.HapticStrength> Impacts = new List<Kturker.UnityBase.Core.Haptics.HapticStrength>();
        public void Click() => Clicks++;
        public void Success() => Successes++;
        public void Failure() => Failures++;
        public void Impact(Kturker.UnityBase.Core.Haptics.HapticStrength strength) => Impacts.Add(strength);
    }

    public sealed class FakeUrlOpener : IUrlOpener
    {
        public readonly List<string> Opened = new List<string>();
        public void Open(string url) => Opened.Add(url);
    }

    public sealed class FakeSettingsPanel : ISettingsPanel
    {
        public int Opened;
        public IAboutPanel Offered;
        public bool IsOpen => Opened > 0;
        public void Open() => Opened++;
        public void OfferAbout(IAboutPanel about) => Offered = about;
    }

    public sealed class FakeAboutPanel : IAboutPanel
    {
        public int Opened;
        public bool IsOpen => Opened > 0;
        public void Open() => Opened++;
    }

    public sealed class FakeSettingsView : ISettingsView
    {
        public int Shown;
        public int Hidden;
        public float Music;
        public float Sfx;
        public bool Vibration;
        public event Action<float> MusicChanged;
        public event Action<float> SfxChanged;
        public event Action<bool> VibrationChanged;
        public event Action CloseClicked;

        public void Show(float music, float sfx, bool vibration)
        {
            Shown++;
            Music = music;
            Sfx = sfx;
            Vibration = vibration;
        }

        public void Hide() => Hidden++;
        public void ChangeMusic(float value) => MusicChanged?.Invoke(value);
        public void ChangeSfx(float value) => SfxChanged?.Invoke(value);
        public void ChangeVibration(bool value) => VibrationChanged?.Invoke(value);
        public void ClickClose() => CloseClicked?.Invoke();
        public bool RateVisible;
        public event Action RateClicked;
        public void SetRateVisible(bool visible) => RateVisible = visible;
        public void ClickRate() => RateClicked?.Invoke();
        public bool AboutVisible;
        public event Action AboutClicked;
        public void SetAboutVisible(bool visible) => AboutVisible = visible;
        public void ClickAbout() => AboutClicked?.Invoke();
        public bool CreditsVisible;
        public string CreditsShown;
        public int CreditsHidden;
        public event Action CreditsClicked;
        public event Action CreditsCloseClicked;
        public void SetCreditsVisible(bool visible) => CreditsVisible = visible;
        public void ShowCredits(string text) => CreditsShown = text;
        public void HideCredits() => CreditsHidden++;
        public void ClickCredits() => CreditsClicked?.Invoke();
        public void ClickCreditsClose() => CreditsCloseClicked?.Invoke();
        public IReadOnlyList<SettingsRow> Rows = new List<SettingsRow>();
        public int SectionRenders;
        public event Action<int> SectionClicked;
        public void ShowSections(IReadOnlyList<SettingsRow> rows)
        {
            Rows = rows;
            SectionRenders++;
        }
        public void ClickSection(int index) => SectionClicked?.Invoke(index);
    }

    public sealed class FakeAboutView : IAboutView
    {
        public int Shown;
        public int Hidden;
        public string Version;
        public bool PrivacyVisible;
        public bool TermsVisible;
        public string Licenses;
        public event Action PrivacyClicked;
        public event Action TermsClicked;
        public event Action CloseClicked;

        public void Show(string version, bool privacyVisible, bool termsVisible, string licenses)
        {
            Shown++;
            Version = version;
            PrivacyVisible = privacyVisible;
            TermsVisible = termsVisible;
            Licenses = licenses;
        }

        public void Hide() => Hidden++;
        public void ClickPrivacy() => PrivacyClicked?.Invoke();
        public void ClickTerms() => TermsClicked?.Invoke();
        public void ClickClose() => CloseClicked?.Invoke();
    }

    public sealed class FakePauseView : IPauseView
    {
        public int PanelShown;
        public int PanelHidden;
        public event Action PauseClicked;
        public event Action ResumeClicked;
        public event Action SettingsClicked;
        public event Action MenuClicked;
        public void ShowPanel() => PanelShown++;
        public void HidePanel() => PanelHidden++;
        public void ClickPause() => PauseClicked?.Invoke();
        public void ClickResume() => ResumeClicked?.Invoke();
        public void ClickSettings() => SettingsClicked?.Invoke();
        public void ClickMenu() => MenuClicked?.Invoke();
    }

    public sealed class FakeReviewRequester : IReviewRequester
    {
        public int Reviews;
        public readonly List<(string Url, string Fallback)> StorePages = new List<(string Url, string Fallback)>();
        public void RequestReview() => Reviews++;
        public void OpenStorePage(string url, string fallbackUrl) => StorePages.Add((url, fallbackUrl));
    }

    // Answers from Responses (missing: null, like a failed request); Pending keeps every request open; Failure throws
    public sealed class FakeHttpText : IHttpText
    {
        public readonly Dictionary<string, string> Responses = new Dictionary<string, string>();
        public readonly List<string> Requested = new List<string>();
        public Exception Failure;
        public UniTaskCompletionSource<string> Pending;

        public UniTask<string> GetAsync(string url, CancellationToken cancellation)
        {
            Requested.Add(url);
            if (Failure != null) throw Failure;
            if (Pending != null) return Pending.Task;
            return UniTask.FromResult(Responses.TryGetValue(url, out var text) ? text : null);
        }
    }

    public sealed class FakeClock : IClock
    {
        public long NowMillis { get; set; }
    }

    public sealed class FakePlatformUpdater : IPlatformUpdater
    {
        private AppVersion version = new AppVersion("1.0.0", 1);
        public Exception VersionFailure;
        public bool FlexibleOffered;
        public int FlexibleRequests;
        public bool CanUpdate = true;
        public readonly List<string> Updates = new List<string>();

        public Kturker.UnityBase.Core.Domain.StorePlatform Platform { get; set; } = Kturker.UnityBase.Core.Domain.StorePlatform.Android;

        public AppVersion CurrentVersion
        {
            get
            {
                if (VersionFailure != null) throw VersionFailure;
                return version;
            }
            set => version = value;
        }

        public UniTask<bool> StartFlexibleUpdateAsync(CancellationToken cancellation)
        {
            FlexibleRequests++;
            return UniTask.FromResult(FlexibleOffered);
        }

        public bool CanStartUpdate(string storeUrl) => CanUpdate;

        public void StartUpdate(string storeUrl) => Updates.Add(storeUrl);
    }

    public sealed class FakeUpdateView : IUpdateView
    {
        public int RequiredShown;
        public int AvailableShown;
        public int Hidden;
        public bool CanUpdate;
        public event Action UpdateClicked;
        public event Action LaterClicked;

        public void ShowRequired(bool canUpdate)
        {
            RequiredShown++;
            CanUpdate = canUpdate;
        }

        public void ShowAvailable() => AvailableShown++;
        public void Hide() => Hidden++;
        public void ClickUpdate() => UpdateClicked?.Invoke();
        public void ClickLater() => LaterClicked?.Invoke();
    }

    public sealed class FakeStringSource : IStringSource
    {
        private readonly Dictionary<string, List<StringFile>> files = new Dictionary<string, List<StringFile>>();

        public FakeStringSource Add(string language, string name, string json)
        {
            if (!files.TryGetValue(language, out var list))
            {
                list = new List<StringFile>();
                files[language] = list;
            }
            list.Add(new StringFile(name, json));
            return this;
        }

        public IReadOnlyList<StringFile> Load(string language) =>
            files.TryGetValue(language, out var list) ? list : new List<StringFile>();
    }

    public sealed class FakeThemeSource : IThemeSource
    {
        public Palette Current { get; set; }
        public ThemeStyle Style { get; set; }
        public event Action Changed;

        public void Switch(Palette palette)
        {
            Current = palette;
            Changed?.Invoke();
        }
    }

    // Sets the game skin for a test (null: none). Test assemblies a flag can remove reach GameSkin.ForTests through this
    // instead of their own InternalsVisibleTo in Presentation, which would name them in a game built without them.
    public static class TestSkin
    {
        public static void Use(GameSkin skin) => GameSkin.ForTests(skin);
    }

    public sealed class FakeSection : ISettingsSection
    {
        private readonly SettingsOption[] options;
        public readonly List<int> Selections = new List<int>();

        public FakeSection(string titleKey, params SettingsOption[] options)
        {
            TitleKey = titleKey;
            this.options = options;
        }

        public string TitleKey { get; }
        public IReadOnlyList<SettingsOption> Options => options;
        public int Selected { get; set; }
        public event Action Changed;

        public void Select(int index)
        {
            Selections.Add(index);
            Selected = index;
            Changed?.Invoke();
        }
    }

    // The sample's entry, for tests that need any game
    public static class TestGames
    {
        public static readonly GameScene Game = new GameScene(SceneId.Game, "menu.play");
    }

    // Writes "key" or "key(arg,arg)", so tests read which text a presenter asked for
    public sealed class FakeLocalizer : ILocalizer
    {
        public string Language => "en";
        public event Action Changed;
        public string Get(string key, params object[] args) =>
            args == null || args.Length == 0 ? key : $"{key}({string.Join(",", args)})";
        public void Change() => Changed?.Invoke();
    }

    public sealed class FakeEconomyAds : IEconomyAds
    {
        public bool RewardedAvailable { get; set; } = true;
        public bool Earns = true;
        public Exception Failure;
        public int Shows;
        public bool BannerHeld;
        // When set, ShowRewardedAsync waits for it (the ad is on screen)
        public UniTaskCompletionSource<bool> Pending;

        public UniTask<bool> ShowRewardedAsync(CancellationToken ct)
        {
            Shows++;
            if (Failure != null) throw Failure;
            return Pending != null ? Pending.Task : UniTask.FromResult(Earns);
        }

        public void HoldBanner(bool held) => BannerHeld = held;
    }

    public sealed class FakeMenuSideButton : IMenuSideButton
    {
        public MenuSide Side;
        public UnityEngine.Sprite Icon;
        public string LabelKey;
        public Action OnClick;
        public MenuBadge Badge;
        public string Timer;

        public void SetBadge(MenuBadge badge, string timer = null)
        {
            Badge = badge;
            Timer = badge == MenuBadge.Timer ? timer : null;
        }

        public void Click() => OnClick?.Invoke();
    }

    public sealed class FakeMenuSideButtons : IMenuSideButtons, IMenuCoins
    {
        public readonly List<FakeMenuSideButton> Buttons = new List<FakeMenuSideButton>();
        public int? Coins;
        public CurrencyMark CoinMark;
        public int CoinWrites;

        public IMenuSideButton AddSideButton(MenuSide side, UnityEngine.Sprite icon, string labelKey, Action onClick)
        {
            var button = new FakeMenuSideButton { Side = side, Icon = icon, LabelKey = labelKey, OnClick = onClick };
            Buttons.Add(button);
            return button;
        }

        public void SetCoins(int? count, CurrencyMark mark)
        {
            Coins = count;
            CoinMark = mark;
            CoinWrites++;
        }

        public FakeMenuSideButton Find(string labelKey) => Buttons.Find(b => b.LabelKey == labelKey);
    }

    public sealed class FakeEconomyWindows : IEconomyWindows
    {
        public readonly List<string> Opened = new List<string>();
        public bool OpenSupply() { Opened.Add("supply"); return true; }
        public bool OpenWheel() { Opened.Add("wheel"); return true; }
        public bool OpenLogin() { Opened.Add("login"); return true; }
        public bool OpenExtra(IEconomyExtraContent content) { Opened.Add(content.ExtraId); return true; }
    }

    public sealed class FakeBoosterOfferView : IBoosterOfferView
    {
        public bool Open;
        public int Opened;
        public string NameKey;
        public UnityEngine.Sprite Icon;
        public int Price;
        public int Coins;
        public EconomyCurrency Currency;
        public object[] MessageArgs;
        public bool CanBuy;
        public bool AdShown;
        public string Message;
        public bool Busy;
        public event Action BuyClicked;
        public event Action WatchClicked;
        public event Action CloseClicked;

        public void SetOpen(bool open)
        {
            if (open && !Open) Opened++;
            Open = open;
        }

        public void ShowBooster(string nameKey, string descriptionKey, UnityEngine.Sprite icon)
        {
            NameKey = nameKey;
            Icon = icon;
        }

        public void ShowPrice(int price, int coins, EconomyCurrency currency)
        {
            Price = price;
            Coins = coins;
            Currency = currency;
        }

        public void SetActions(bool canBuy, bool adShown)
        {
            CanBuy = canBuy;
            AdShown = adShown;
        }

        public void ShowMessage(string key, params object[] args)
        {
            Message = key;
            MessageArgs = args;
        }

        public void SetBusy(bool busy) => Busy = busy;
        public void ClickBuy() => BuyClicked?.Invoke();
        public void ClickWatch() => WatchClicked?.Invoke();
        public void ClickClose() => CloseClicked?.Invoke();
    }

    public sealed class FakeBoosterPickerView : IBoosterPickerView
    {
        public bool Open;
        public int Opened;
        public IReadOnlyList<PickerRow> Rows = new PickerRow[0];
        public EconomyCurrency Currency;
        public event Action<int> RowClicked;
        public event Action PlayClicked;
        public int MaxRows => 3;

        public void SetOpen(bool open)
        {
            if (open && !Open) Opened++;
            Open = open;
        }

        public void ShowRows(IReadOnlyList<PickerRow> rows, EconomyCurrency currency)
        {
            Rows = rows;
            Currency = currency;
        }

        public void ClickRow(int index) => RowClicked?.Invoke(index);
        public void ClickPlay() => PlayClicked?.Invoke();
    }

    public sealed class FakeBoosterBarView : IBoosterBarView
    {
        public IReadOnlyList<BoosterSlot> Shown = new BoosterSlot[0];
        public readonly Dictionary<int, int> Counts = new Dictionary<int, int>();
        public event Action<int> Clicked;
        public int Slots => 3;
        public void Show(IReadOnlyList<BoosterSlot> slots) => Shown = slots;
        public void SetCount(int index, int count) => Counts[index] = count;
        public void Click(int index) => Clicked?.Invoke(index);
    }

    public sealed class FakeSupplyWindowView : ISupplyWindowView
    {
        public bool Open;
        public int FreeCoins;
        public bool FreeReady;
        public string FreeTimer;
        public IReadOnlyList<SupplyRow> Rows = new SupplyRow[0];
        public CurrencyMark Currency;
        public string Message;
        public bool Busy;
        public event Action FreeClicked;
        public event Action<int> AdClicked;
        public event Action CloseClicked;
        public int MaxAdRows => 4;
        public void SetOpen(bool open) => Open = open;

        public void ShowFree(int coins, bool ready, string timer)
        {
            FreeCoins = coins;
            FreeReady = ready;
            FreeTimer = timer;
        }

        public void ShowAdRows(IReadOnlyList<SupplyRow> rows) => Rows = rows;

        public void ShowCurrency(CurrencyMark mark) => Currency = mark;

        public void ShowMessage(string key) => Message = key;
        public void SetBusy(bool busy) => Busy = busy;
        public void ClickFree() => FreeClicked?.Invoke();
        public void ClickAd(int row) => AdClicked?.Invoke(row);
        public void ClickClose() => CloseClicked?.Invoke();
    }

    public sealed class FakeWheelWindowView : IWheelWindowView
    {
        public bool Open;
        public IReadOnlyList<WheelSliceLabel> Slices = new WheelSliceLabel[0];
        public bool FreeReady;
        public bool AdShown;
        public string Status;
        public string Note;
        public IReadOnlyList<string> Odds;
        public readonly List<int> SpunTo = new List<int>();
        public event Action SpinClicked;
        public event Action SpinAdClicked;
        public event Action InfoClicked;
        public event Action CloseClicked;
        public int MaxSlices => 8;
        public void SetOpen(bool open) => Open = open;
        public void ShowSlices(IReadOnlyList<WheelSliceLabel> slices) => Slices = slices;

        public void ShowControls(bool freeReady, bool adShown, string status, string note)
        {
            FreeReady = freeReady;
            AdShown = adShown;
            Status = status;
            Note = note;
        }

        public void ShowOdds(IReadOnlyList<string> lines) => Odds = lines;

        public UniTask SpinTo(int index, CancellationToken ct)
        {
            SpunTo.Add(index);
            return UniTask.CompletedTask;
        }

        public void SetBusy(bool busy)
        {
        }

        public void ClickSpin() => SpinClicked?.Invoke();
        public void ClickSpinAd() => SpinAdClicked?.Invoke();
        public void ClickInfo() => InfoClicked?.Invoke();
        public void ClickClose() => CloseClicked?.Invoke();
    }

    public sealed class FakeLoginBonusView : ILoginBonusView
    {
        public bool Open;
        public IReadOnlyList<LoginDay> Days = new LoginDay[0];
        public bool ClaimReady;
        public string Next;
        public event Action ClaimClicked;
        public event Action CloseClicked;
        public void SetOpen(bool open) => Open = open;
        public void ShowDays(IReadOnlyList<LoginDay> days) => Days = days;

        public void ShowClaim(bool ready, string next)
        {
            ClaimReady = ready;
            Next = next;
        }

        public void ClickClaim() => ClaimClicked?.Invoke();
        public void ClickClose() => CloseClicked?.Invoke();
    }

    public sealed class FakeExtraWindowView : IExtraWindowView
    {
        public bool Open;
        public string Title;
        public bool Busy;
        public event Action CloseClicked;
        public UnityEngine.RectTransform Content => null;
        public void SetOpen(bool open) => Open = open;
        public void ShowTitle(string key) => Title = key;
        public void SetBusy(bool busy) => Busy = busy;
        public void ClickClose() => CloseClicked?.Invoke();
    }

    // A custom extra's content that records what the frame did with it
    public sealed class FakeExtraContent : IEconomyExtraContent
    {
        public FakeExtraContent(string id) => ExtraId = id;

        public string ExtraId { get; }
        public int Shown;
        public int Ticks;
        public int Hidden;
        public IEconomyExtraFrame Frame;

        public void Show(UnityEngine.RectTransform parent, IEconomyExtraFrame frame)
        {
            Shown++;
            Frame = frame;
        }

        public void Tick() => Ticks++;
        public void Hide() => Hidden++;
    }
}
