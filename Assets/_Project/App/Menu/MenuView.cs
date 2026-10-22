using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Shell.GameShell;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Kturker.UnityBase.App.Menu
{
    // The menu: a top bar (the coins on the left, the settings gear in the right corner), the title or the game's logo
    // in the middle with the side columns of round feature buttons at its sides, the big Play at the bottom. While room
    // is kept for the banner (SafeArea.ReservedTop > 0, from before the ad loads: SafeArea already keeps the content
    // below it) the top bar moves down one button height more, so no button sits next to the ad.
    public sealed class MenuView : MonoBehaviour, IMenuView, IMenuSideButtons, IMenuCoins
    {
        // Each further game's button sits this many canvas units above the previous one's top edge
        private const float GameGap = 16f;
        // Further games' buttons: quieter than the first game's Play, narrow enough for the gap between the side columns
        public static readonly Vector2 GameCopySize = new Vector2(420f, 150f);
        // Round buttons per side column
        public const int SideSlots = 3;
        // Units between the safe area's top and the top bar with no banner
        public const float TopInset = 24f;
        // Units between the title's last line and its stroke
        public const float StrokeGap = 12f;

        [SerializeField] private TMP_Text title;
        [SerializeField] private Image titleLogo;
        [SerializeField] private RectTransform titleStroke;
        [SerializeField] private Image brandBackdrop;
        [SerializeField] private Image brandScrim;
        [SerializeField] private GameObject decor;
        [SerializeField] private TMP_Text version;
        [SerializeField] private Button playButton;
        [SerializeField] private RectTransform topBar;
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject coins;
        [SerializeField] private FallbackIcon coinIcon;
        [SerializeField] private TMP_Text coinCount;
        [SerializeField] private RectTransform leftSide;
        [SerializeField] private RectTransform rightSide;
        [SerializeField] private MenuSideButton sideTemplate;
        [SerializeField] private RectTransform extraSlot;

        private readonly List<Button> gameButtons = new List<Button>();
        private readonly List<MenuSideButton> left = new List<MenuSideButton>();
        private readonly List<MenuSideButton> right = new List<MenuSideButton>();
        private UiBinder binder;
        private bool bannerSpace;

        public event Action<int> GameClicked;
        public event Action SettingsClicked;

        public IReadOnlyList<Button> GameButtons => gameButtons;
        public Button SettingsButton => settingsButton;
        public RectTransform TopBar => topBar;
        public GameObject Coins => coins;
        public TMP_Text CoinCount => coinCount;
        public FallbackIcon CoinIcon => coinIcon;
        public RectTransform TitleStroke => titleStroke;
        public TMP_Text Title => title;

        // A column under the title where features add rows (the showcase's best score, premium, stats)
        public Transform ExtraSlot => extraSlot;

        // Binds the side buttons added at runtime (the scene itself was bound before); without it (EditMode tests) they
        // keep the builder's colours
        [Inject]
        public void Construct(UiBinder uiBinder) => binder = uiBinder;

        private void Awake()
        {
            ShowBranding(GameBranding.TitleLogo, BackgroundFor(GameBranding.MenuBackground, GameSkin.Current), GameSkin.Current?.Scrim);
            settingsButton.onClick.AddListener(OnSettings);
            ReserveBannerSpace(SafeArea.ReservedTop > 0f);
        }

        private void OnEnable() => SafeArea.ReservationChanged += FollowBannerSpace;

        private void OnDisable() => SafeArea.ReservationChanged -= FollowBannerSpace;

        private void Update() => FollowBannerSpace();

        private void FollowBannerSpace()
        {
            var shown = SafeArea.ReservedTop > 0f;
            if (shown != bannerSpace) ReserveBannerSpace(shown);
        }

        // A rotation or a new safe area re-flows the title, so its stroke follows
        private void OnRectTransformDimensionsChange()
        {
            if (title != null && !string.IsNullOrEmpty(title.text)) PlaceTitleStroke();
        }

        private void OnDestroy()
        {
            settingsButton.onClick.RemoveListener(OnSettings);
            foreach (var button in gameButtons) button.onClick.RemoveAllListeners();
        }

        public void SetTitle(string text)
        {
            title.text = text;
            PlaceTitleStroke();
        }

        // The stroke under the title's last line, however many lines the name takes at its auto size
        public void PlaceTitleStroke()
        {
            title.ForceMeshUpdate();
            var info = title.textInfo;
            if (info == null || info.characterCount == 0) return;
            var bottom = title.transform.TransformPoint(new Vector3(0f, title.textBounds.min.y, 0f)).y;
            var scale = titleStroke.lossyScale.y;
            var position = titleStroke.position;
            position.y = bottom - (StrokeGap + titleStroke.rect.height * (1f - titleStroke.pivot.y)) * scale;
            titleStroke.position = position;
        }

        // The menu's picture: Compass's rendered still, else the skin's menu background; null leaves the theme's decor
        public static Sprite BackgroundFor(Sprite branding, GameSkin skin) => branding != null ? branding : skin?.MenuBackground;

        public void ShowBranding(Sprite logo, Sprite background) => ShowBranding(logo, background, null);

        // The game's logo in place of the title text and its stroke; the game's picture, covering the screen under a
        // light Scrim (the skin's scrim colour when it has one), in place of the decor shapes. Null keeps that part as
        // the theme draws it.
        public void ShowBranding(Sprite logo, Sprite background, Color? scrim)
        {
            var hasLogo = logo != null;
            titleLogo.sprite = logo;
            titleLogo.preserveAspect = true;
            titleLogo.gameObject.SetActive(hasLogo);
            title.gameObject.SetActive(!hasLogo);
            titleStroke.gameObject.SetActive(!hasLogo);

            var hasBackground = background != null;
            brandBackdrop.sprite = background;
            if (hasBackground)
            {
                var fitter = brandBackdrop.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = background.rect.width / background.rect.height;
            }
            brandBackdrop.gameObject.SetActive(hasBackground);
            brandScrim.gameObject.SetActive(hasBackground);
            KitShape.Fix(brandScrim, scrim);
            decor.SetActive(!hasBackground);
        }

        public void SetVersion(string text) => version.text = text;

        // The wallet's currency at the top bar's left with its mark in its own colour; null count hides them (no economy)
        public void SetCoins(int? count, CurrencyMark mark)
        {
            coins.SetActive(count.HasValue);
            if (!count.HasValue) return;
            coinCount.text = count.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            coinIcon.ShowCurrency(mark);
        }

        // The top bar at TopInset below the safe area's top, one settings button height lower while room is kept for the
        // banner
        public void ReserveBannerSpace(bool bannerShown)
        {
            bannerSpace = bannerShown;
            var gap = bannerShown ? ((RectTransform)settingsButton.transform).rect.height : 0f;
            topBar.anchoredPosition = new Vector2(topBar.anchoredPosition.x, -(TopInset + gap));
        }

        public IMenuSideButton AddSideButton(MenuSide side, Sprite icon, string labelKey, Action onClick)
        {
            var column = side == MenuSide.Left ? left : right;
            if (column.Count >= SideSlots)
                throw new InvalidOperationException($"the menu's {side} column holds {SideSlots} buttons");
            var button = Instantiate(sideTemplate, side == MenuSide.Left ? leftSide : rightSide, false);
            button.name = $"{side}{column.Count}";
            button.gameObject.SetActive(true);
            button.Setup(icon, labelKey, onClick);
            binder?.BindTree(button.gameObject);
            column.Add(button);
            return button;
        }

        // The scene's Play button serves the first game; smaller copies above it serve the rest (the upstream template
        // has the sample and both showcase games, a generated project one). Copies are bound here: the scene was bound
        // before.
        public void ShowGames(IReadOnlyList<string> labelKeys, UiBinder uiBinder)
        {
            var first = (RectTransform)playButton.transform;
            var top = first.anchoredPosition.y + first.rect.height / 2f;
            for (var i = 0; i < labelKeys.Count; i++)
            {
                var button = i == 0 ? playButton : Instantiate(playButton, playButton.transform.parent);
                if (i > 0)
                {
                    var rect = (RectTransform)button.transform;
                    rect.sizeDelta = GameCopySize;
                    rect.anchoredPosition = new Vector2(first.anchoredPosition.x, top + GameGap + GameCopySize.y / 2f);
                    top += GameGap + GameCopySize.y;
                }
                var index = i;
                button.onClick.AddListener(() => GameClicked?.Invoke(index));
                button.GetComponentInChildren<LocalizedText>(true).SetKey(labelKeys[i]);
                if (i > 0) uiBinder.BindTree(button.gameObject);
                gameButtons.Add(button);
            }
            playButton.gameObject.SetActive(labelKeys.Count > 0);
        }

        public void ShowLevel(int index, int level)
        {
            if (index < 0 || index >= gameButtons.Count) return;
            gameButtons[index].GetComponentInChildren<LocalizedText>(true).SetKey(ShellKeys.Level, level);
        }

        private void OnSettings() => SettingsClicked?.Invoke();
    }
}
