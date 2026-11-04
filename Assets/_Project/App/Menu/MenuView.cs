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
    // The menu: a top bar (the coins pill on the left, with the shop's "+" mark at its right end while a shop can show;
    // the settings gear in the right corner), the title or the game's logo in the top third, and the bottom block
    // BottomInset above the safe area's bottom, growing upward with BlockGap between its parts, top down: the features'
    // rows (ExtraSlot), the further games' smaller Play copies side by side, one centred row of round feature buttons
    // (SideRow, a KitFlowRow: the Left ones first) and the big Play. While room is kept for the banner
    // (SafeArea.ReservedTop > 0, from before the ad loads: SafeArea already keeps the content below it) the top bar moves
    // down one button height more, so no button sits next to the ad.
    // Turned to landscape (Arrange: the view wider than high) the title and the logo stand left, centred at
    // (LandscapeTitleX, LandscapeTitleY) of the view and LandscapeTitleShare of its width, the version text under them;
    // the bottom block holds the round buttons' row and Play in the right half, LandscapeSideInset from the right edge
    // and LandscapeBottomInset above the bottom, scaled down as a whole when it is taller than the room under the top bar
    // (never so far that a round button's circle or Play drops under KitLayout.MinTouch); the features' rows and the
    // further games move to a column under the title (the half is too short for them on a landscape phone with a banner
    // and too narrow to set them beside Play), the title giving up height to them. Portrait puts everything back as
    // built (kept at Awake), except a title or logo band the top bar (moved down for the banner) or a busy bottom block
    // would reach: that one moves and shrinks between the top bar and the bottom block, and where even its smallest
    // does not fit, the features' rows scale down together (never a button among them under KitLayout.MinTouch).
    public sealed class MenuView : MonoBehaviour, IMenuView, IMenuSideButtons, IMenuCoins
    {
        // Units between the further games' buttons, side by side in their row
        public const float GameGap = 16f;
        // Further games' buttons: quieter than the first game's Play
        public static readonly Vector2 GameCopySize = new Vector2(420f, 150f);
        // The first game's Play: 70 % of the 1080-unit reference width
        public static readonly Vector2 PlaySize = new Vector2(756f, 230f);
        // Units between the safe area's bottom and the bottom block (the version text sits under it)
        public const float BottomInset = 200f;
        // Units between the bottom block's parts
        public const float BlockGap = 24f;
        // Round buttons per side (MenuSide): the row holds both sides' buttons
        public const int SideSlots = 3;
        // The row's captions share one size (its KitTextGroup): from CaptionMax down to CaptionMin, and on to CaptionFloor
        // rather than wrap
        public const float CaptionMax = 32f;
        public const float CaptionMin = 22f;
        public const float CaptionFloor = 18f;
        // Units between the safe area's top and the top bar with no banner
        public const float TopInset = 24f;
        // Units between the title's last line and its stroke
        public const float StrokeGap = 12f;
        // Landscape: the title's and the logo's centre as fractions of the view, and their share of its width
        public const float LandscapeTitleX = 0.27f;
        public const float LandscapeTitleY = 0.55f;
        public const float LandscapeTitleShare = 0.46f;
        // Landscape: units between the bottom block and the view's right edge, and its bottom
        public const float LandscapeSideInset = 60f;
        public const float LandscapeBottomInset = 48f;
        // Landscape: the title's and the logo's smallest scale when the column under them takes their height
        private const float MinBandScale = 0.3f;
        // The Scrim role's opacity over the menu's picture: the full role over Compass's still (blurred on import,
        // MenuStillImporter; the role's 0.6 alpha darkens it by about 35 % as seen), 35 % of it over a skin's designed
        // background, which the buttons read on while it still shows
        public const float StillScrimOpacity = 1f;
        public const float PictureScrimOpacity = 0.35f;

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
        [SerializeField] private Button coinsButton;
        [SerializeField] private FallbackIcon coinIcon;
        [SerializeField] private TMP_Text coinCount;
        [SerializeField] private GameObject buyMark;
        [SerializeField] private RectTransform bottomBlock;
        [SerializeField] private RectTransform gameRow;
        [SerializeField] private RectTransform sideRow;
        [SerializeField] private KitTextGroup captions;
        [SerializeField] private MenuSideButton sideTemplate;
        [SerializeField] private RectTransform extraSlot;

        private readonly List<Button> gameButtons = new List<Button>();
        private readonly List<MenuSideButton> left = new List<MenuSideButton>();
        private readonly List<MenuSideButton> right = new List<MenuSideButton>();
        private UiBinder binder;
        private bool bannerSpace;
        // The rects Arrange moves, as built (null until kept); the landscape column under the title (made on the first
        // landscape arrangement); Arrange running (a rect it writes must not start it again); the sizes it arranged for
        private Built built;
        private RectTransform column;
        private bool arranging;
        private Vector2 arrangedView, arrangedBlock, arrangedColumn;
        private float arrangedBar;
        private readonly Vector3[] corners = new Vector3[4];

        public event Action<int> GameClicked;
        public event Action SettingsClicked;
        public event Action CoinsClicked;

        public IReadOnlyList<Button> GameButtons => gameButtons;
        public Button SettingsButton => settingsButton;
        public RectTransform TopBar => topBar;
        public GameObject Coins => coins;
        public TMP_Text CoinCount => coinCount;
        public FallbackIcon CoinIcon => coinIcon;
        public RectTransform TitleStroke => titleStroke;
        public TMP_Text Title => title;
        public Image TitleLogo => titleLogo;
        public TMP_Text Version => version;
        // The shop's "+" at the coins pill's right end (ShowBuy)
        public GameObject BuyMark => buyMark;
        // The features' rows, the further games, the round feature buttons and Play together: placed as a whole
        public RectTransform BottomBlock => bottomBlock;
        // The round feature buttons' row in the bottom block, right above Play (a KitFlowRow)
        public RectTransform SideRow => sideRow;

        // A column at the bottom block's top where features add rows (the showcase's best score, premium, stats)
        public Transform ExtraSlot => extraSlot;

        // Binds the side buttons added at runtime (the scene itself was bound before); without it (EditMode tests) they
        // keep the builder's colours
        [Inject]
        public void Construct(UiBinder uiBinder) => binder = uiBinder;

        private void Awake()
        {
            Keep();
            var still = GameBranding.MenuBackground;
            ShowBranding(GameBranding.TitleLogo, BackgroundFor(still, GameSkin.Current), GameSkin.Current?.Scrim, still != null);
            settingsButton.onClick.AddListener(OnSettings);
            coinsButton.onClick.AddListener(OnCoins);
            ReserveBannerSpace(SafeArea.ReservedTop > 0f);
            Arrange();
        }

        // Shown again (after a rotation while it was hidden): arranged before its first frame
        private void OnEnable()
        {
            SafeArea.ReservationChanged += FollowBannerSpace;
            if (built != null) Arrange();
        }

        private void OnDisable() => SafeArea.ReservationChanged -= FollowBannerSpace;

        private void Update()
        {
            FollowBannerSpace();
            // A banner kept or let go moves the top bar, a feature's button or row changes the block's or the column's size
            if (Moved()) Arrange();
        }

        private void FollowBannerSpace()
        {
            var shown = SafeArea.ReservedTop > 0f;
            if (shown != bannerSpace) ReserveBannerSpace(shown);
        }

        // A rotation or a new safe area: arranged for the new shape, the title's stroke following it
        private void OnRectTransformDimensionsChange()
        {
            if (built != null) Arrange();
        }

        private void OnDestroy()
        {
            settingsButton.onClick.RemoveListener(OnSettings);
            coinsButton.onClick.RemoveListener(OnCoins);
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

        // The menu's picture: Compass's rendered still (blurred when Unity imports it, MenuStillImporter in
        // Core/Presentation/Editor), else the skin's menu background (as designed, never blurred); null leaves the
        // theme's decor
        public static Sprite BackgroundFor(Sprite branding, GameSkin skin) => branding != null ? branding : skin?.MenuBackground;

        public void ShowBranding(Sprite logo, Sprite background) => ShowBranding(logo, background, null);

        // The game's logo in place of the title text and its stroke; the game's picture, covering the screen under the
        // Scrim, in place of the decor shapes: over Compass's still (still) the Scrim role at full tone, whatever the
        // skin; over another picture (a skin's designed background) the skin's scrim colour when it has one, else the
        // role at PictureScrimOpacity. Null keeps that part as the theme draws it.
        public void ShowBranding(Sprite logo, Sprite background, Color? scrim, bool still = false)
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
            // The still is always darkened by the role itself: a skin's scrim colour is tuned for its own designed
            // background (often a light veil) and is left for that
            KitShape.Fix(brandScrim, still ? null : scrim);
            brandScrim.GetComponent<ThemedGraphic>().SetTone(0f, still ? StillScrimOpacity : PictureScrimOpacity);
            decor.SetActive(!hasBackground);
        }

        // Lays the menu out for its rect's shape: landscape (wider than high) as the class comment says, portrait as built.
        // Idempotent; writes only its children's rects and only those that differ, and does nothing while it runs (the
        // rects it writes call OnRectTransformDimensionsChange)
        public void Arrange()
        {
            if (arranging || title == null) return;
            arranging = true;
            try
            {
                Keep();
                var view = ((RectTransform)transform).rect.size;
                if (view.x > view.y) Landscape(view);
                else Portrait();
                if (!string.IsNullOrEmpty(title.text)) PlaceTitleStroke();
                arrangedView = view;
                arrangedBlock = bottomBlock.rect.size;
                arrangedColumn = column != null ? column.rect.size : Vector2.zero;
                arrangedBar = topBar.anchoredPosition.y;
            }
            finally
            {
                arranging = false;
            }
        }

        private bool Moved() =>
            built != null && (((RectTransform)transform).rect.size != arrangedView || bottomBlock.rect.size != arrangedBlock ||
                (column != null ? column.rect.size : Vector2.zero) != arrangedColumn || !Mathf.Approximately(topBar.anchoredPosition.y, arrangedBar));

        // The built places, once
        private void Keep()
        {
            if (built != null || title == null) return;
            built = new Built
            {
                Title = Placement.Of(title.rectTransform),
                Logo = Placement.Of(titleLogo.rectTransform),
                Stroke = Placement.Of(titleStroke),
                Version = Placement.Of(version.rectTransform),
                Block = Placement.Of(bottomBlock),
                ExtrasIndex = extraSlot.GetSiblingIndex(),
                GamesIndex = gameRow.GetSiblingIndex()
            };
        }

        private void Portrait()
        {
            // The stroke's height is PlaceTitleStroke's once the title has a name
            var stroke = built.Stroke;
            if (!string.IsNullOrEmpty(title.text)) stroke.Position.y = titleStroke.anchoredPosition.y;
            stroke.Apply(titleStroke);
            built.Version.Apply(version.rectTransform);
            built.Block.Apply(bottomBlock, true);
            Move(extraSlot, bottomBlock, built.ExtrasIndex);
            Move(gameRow, bottomBlock, built.GamesIndex);
            if (column != null && column.gameObject.activeSelf) column.gameObject.SetActive(false);
            ScaleExtras(1f);

            // A built band the top bar (moved down for the banner) or the bottom block (a busy one) would reach moves down
            // and shrinks as in landscape, between them. Otherwise it stays as built
            var view = ((RectTransform)transform).rect.size;
            var top = Local(topBar).yMin - BlockGap;
            LayoutRebuilder.ForceRebuildLayoutImmediate(bottomBlock);
            var above = Local(bottomBlock).yMax + BlockGap;
            var strokeRoom = StrokeGap + titleStroke.rect.height;
            var titleIn = Top(built.Title, view) <= top && Bottom(built.Title, view) - strokeRoom >= above;
            var logoIn = Top(built.Logo, view) <= top && Bottom(built.Logo, view) >= above;
            if (titleIn) built.Title.Apply(title.rectTransform);
            if (logoIn) built.Logo.Apply(titleLogo.rectTransform);
            if (titleIn && logoIn) return;

            // Too little room for a shown band even at its smallest: the features' rows scale down until it fits (never
            // so far that a button among them drops under the touch size)
            var need = 0f;
            if (!titleIn && title.gameObject.activeSelf) need = Mathf.Max(need, built.Title.Size.y * MinBandScale + strokeRoom);
            if (!logoIn && titleLogo.gameObject.activeSelf) need = Mathf.Max(need, built.Logo.Size.y * MinBandScale);
            var shortfall = above + need - top;
            var rows = RowsHeight();
            if (shortfall > 0f && rows > 0f)
            {
                ScaleExtras(Mathf.Max((rows - shortfall) / rows, ExtrasTouchFloor(), MinBandScale));
                LayoutRebuilder.ForceRebuildLayoutImmediate(bottomBlock);
                above = Local(bottomBlock).yMax + BlockGap;
            }
            if (!titleIn) Band(title.rectTransform, built.Title, above + strokeRoom, top, view);
            if (!logoIn) Band(titleLogo.rectTransform, built.Logo, above, top, view);
        }

        // The features' rows at scale (their column, a VerticalLayoutGroup that counts its rows' scale, as tall as they
        // are shown), written only where it differs
        private void ScaleExtras(float scale)
        {
            var value = new Vector3(scale, scale, 1f);
            foreach (Transform row in extraSlot)
                if (row.localScale != value) row.localScale = value;
        }

        // The shown features' rows' own heights together (their column's spacing does not scale), in the view's units
        private float RowsHeight()
        {
            if (!extraSlot.gameObject.activeSelf) return 0f;
            var height = 0f;
            foreach (Transform row in extraSlot)
                if (row.gameObject.activeSelf) height += Local((RectTransform)row).height;
            return height;
        }

        // The features' rows' smallest scale: every shown button among them keeps a touch area MinTouch on each side
        private float ExtrasTouchFloor()
        {
            var floor = 0f;
            foreach (var button in extraSlot.GetComponentsInChildren<Selectable>())
            {
                var size = ((RectTransform)button.transform).rect.size;
                if (button.GetComponentInChildren<KitTouchArea>(true) != null) size = Vector2.Max(size, Vector2.one * KitLayout.MinTouch);
                var side = Mathf.Min(size.x, size.y);
                if (side > 0f) floor = Mathf.Max(floor, KitLayout.MinTouch / side);
            }
            return Mathf.Min(floor, 1f);
        }

        // A built band's top and bottom in the view's units from its bottom (its anchors at one height)
        private static float Top(Placement band, Vector2 view) =>
            band.AnchorMin.y * view.y + band.Position.y + band.Size.y * (1f - band.Pivot.y);

        private static float Bottom(Placement band, Vector2 view) => band.AnchorMin.y * view.y + band.Position.y - band.Size.y * band.Pivot.y;

        // A portrait band where it was built across, its built height at most, between bottom and top
        private static void Band(RectTransform band, Placement place, float bottom, float top, Vector2 view) =>
            Band(band, new Vector2((place.AnchorMin.x + place.AnchorMax.x) / 2f, place.AnchorMin.y), place.AnchorMax.x - place.AnchorMin.x,
                place.Size.y, bottom, top, view);

        private void Landscape(Vector2 view)
        {
            var titlePoint = new Vector2(LandscapeTitleX, LandscapeTitleY);
            var versionPlace = built.Version;
            versionPlace.AnchorMin.x = versionPlace.AnchorMax.x = LandscapeTitleX;
            versionPlace.Apply(version.rectTransform);
            var stroke = built.Stroke;
            stroke.AnchorMin = stroke.AnchorMax = titlePoint;
            stroke.Position = new Vector2(0f, titleStroke.anchoredPosition.y);
            stroke.Apply(titleStroke);

            // The features' rows and the further games into the column, the block left with the round buttons and Play
            var side = Column();
            Move(extraSlot, side, 0);
            Move(gameRow, side, 1);
            ScaleExtras(1f);
            var block = new Placement
            {
                AnchorMin = new Vector2(0.5f, 0f), AnchorMax = new Vector2(1f, 0f), Pivot = new Vector2(1f, 0f),
                Position = new Vector2(-LandscapeSideInset, bottomBlock.anchoredPosition.y), Size = new Vector2(-LandscapeSideInset, 0f),
                Scale = bottomBlock.localScale
            };
            block.Apply(bottomBlock, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(bottomBlock);
            LayoutRebuilder.ForceRebuildLayoutImmediate(side);

            // Scaled down to the room under the top bar, never under the touch size; on a screen too short even then the
            // block comes down towards the kit's edge gap
            var top = Local(topBar).yMin - BlockGap;
            var height = bottomBlock.rect.height;
            var scale = height > 0f ? Mathf.Clamp((top - LandscapeBottomInset) / height, TouchFloor(), 1f) : 1f;
            block.Position.y = Mathf.Clamp(top - height * scale, KitLayout.Gap, LandscapeBottomInset);
            block.Scale = new Vector3(scale, scale, 1f);
            block.Apply(bottomBlock, true);

            // The column over the version text, as wide as the title at most
            var bottom = Local(version.rectTransform).yMax + BlockGap;
            var content = side.rect.size;
            // Never so small that a further game's button drops under the touch size
            var columnScale = content.x > 0f ? Mathf.Min(1f, LandscapeTitleShare * view.x / content.x) : 1f;
            columnScale = Mathf.Max(columnScale, ColumnTouchFloor());
            new Placement
            {
                AnchorMin = new Vector2(LandscapeTitleX, 0f), AnchorMax = new Vector2(LandscapeTitleX, 0f), Pivot = new Vector2(0.5f, 0f),
                Position = new Vector2(0f, bottom), Size = side.sizeDelta, Scale = new Vector3(columnScale, columnScale, 1f)
            }.Apply(side);
            var above = content.y >= 1f ? bottom + content.y * columnScale + BlockGap : bottom;

            // The title (its stroke under it) and the logo between the column and the top bar
            Band(title.rectTransform, titlePoint, LandscapeTitleShare, built.Title.Size.y, above + StrokeGap + titleStroke.rect.height, top, view);
            Band(titleLogo.rectTransform, titlePoint, LandscapeTitleShare, built.Logo.Size.y, above, top, view);
        }

        // A title or logo band centred at anchor (fractions of the view) and share of its width, its built height scaled
        // down to fit between bottom and top and moved as little as it needs to stay between them
        private static void Band(RectTransform band, Vector2 anchor, float share, float height, float bottom, float top, Vector2 view)
        {
            var scale = Mathf.Clamp((top - bottom) / height, MinBandScale, 1f);
            var half = height * scale / 2f;
            var centre = Mathf.Max(bottom + half, Mathf.Min(anchor.y * view.y, top - half));
            new Placement
            {
                AnchorMin = anchor, AnchorMax = anchor, Pivot = new Vector2(0.5f, 0.5f), Position = new Vector2(0f, centre - anchor.y * view.y),
                Size = new Vector2(share * view.x / scale, height), Scale = new Vector3(scale, scale, 1f)
            }.Apply(band);
        }

        // The column's smallest scale: every shown further game's button stays MinTouch high
        private float ColumnTouchFloor()
        {
            var floor = 0f;
            if (!gameRow.gameObject.activeSelf) return floor;
            foreach (Transform copy in gameRow)
            {
                var height = ((RectTransform)copy).rect.height;
                if (copy.gameObject.activeSelf && height > 0f) floor = Mathf.Max(floor, KitLayout.MinTouch / height);
            }
            return Mathf.Min(floor, 1f);
        }

        // The block's smallest scale: Play and every shown round button's circle (at its row's scale) stay MinTouch high
        private float TouchFloor()
        {
            var floor = 0f;
            if (playButton.gameObject.activeSelf) floor = KitLayout.MinTouch / ((RectTransform)playButton.transform).rect.height;
            foreach (var list in new[] { left, right })
                foreach (var each in list)
                {
                    if (!each.gameObject.activeSelf) continue;
                    var circle = ((RectTransform)each.Button.transform).rect.height * each.transform.localScale.y;
                    if (circle > 0f) floor = Mathf.Max(floor, KitLayout.MinTouch / circle);
                }
            return Mathf.Min(floor, 1f);
        }

        // The landscape column under the title: a stack like the block's, as big as its parts, shown in landscape only
        private RectTransform Column()
        {
            if (column == null)
            {
                var go = new GameObject("Column", typeof(RectTransform)) { layer = gameObject.layer };
                column = (RectTransform)go.transform;
                // Last, so the built parts keep their places among the view's children
                column.SetParent(transform, false);
                var source = bottomBlock.GetComponent<VerticalLayoutGroup>();
                var stack = go.AddComponent<VerticalLayoutGroup>();
                stack.spacing = source.spacing;
                stack.childAlignment = source.childAlignment;
                stack.childControlWidth = true;
                stack.childControlHeight = true;
                stack.childForceExpandWidth = false;
                stack.childForceExpandHeight = false;
                var fitter = go.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            if (!column.gameObject.activeSelf) column.gameObject.SetActive(true);
            return column;
        }

        private static void Move(Transform child, Transform parent, int index)
        {
            if (child.parent != parent) child.SetParent(parent, false);
            if (child.GetSiblingIndex() != index) child.SetSiblingIndex(index);
        }

        // A rect in the view's units from its bottom left corner
        private Rect Local(RectTransform rect)
        {
            var view = (RectTransform)transform;
            rect.GetWorldCorners(corners);
            var min = (Vector2)view.InverseTransformPoint(corners[0]) - view.rect.min;
            var max = (Vector2)view.InverseTransformPoint(corners[2]) - view.rect.min;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private sealed class Built
        {
            public Placement Title, Logo, Stroke, Version, Block;
            public int ExtrasIndex, GamesIndex;
        }

        // A rect's place: written only where it differs
        private struct Placement
        {
            public Vector2 AnchorMin, AnchorMax, Pivot, Position, Size;
            public Vector3 Scale;

            public static Placement Of(RectTransform rect) => new Placement
            {
                AnchorMin = rect.anchorMin, AnchorMax = rect.anchorMax, Pivot = rect.pivot, Position = rect.anchoredPosition,
                Size = rect.sizeDelta, Scale = rect.localScale
            };

            // ownHeight: the rect's height stays its content's (a ContentSizeFitter's)
            public void Apply(RectTransform rect, bool ownHeight = false)
            {
                var size = ownHeight ? new Vector2(Size.x, rect.sizeDelta.y) : Size;
                if (rect.anchorMin != AnchorMin) rect.anchorMin = AnchorMin;
                if (rect.anchorMax != AnchorMax) rect.anchorMax = AnchorMax;
                if (rect.pivot != Pivot) rect.pivot = Pivot;
                if (rect.sizeDelta != size) rect.sizeDelta = size;
                if (rect.anchoredPosition != Position) rect.anchoredPosition = Position;
                if (rect.localScale != Scale) rect.localScale = Scale;
            }
        }

        public void SetVersion(string text) => version.text = text;

        // The wallet's currency at the top bar's left with its mark in its own colour; null count hides them (no economy)
        public void SetCoins(int? count, CurrencyMark mark)
        {
            coins.SetActive(count.HasValue);
            if (!count.HasValue)
            {
                buyMark.SetActive(false);
                return;
            }
            coinCount.text = count.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            coinIcon.ShowCurrency(mark);
        }

        // The shop's "+" mark at the coins pill's right end: shown while a shop can show (EconomyMenu)
        public void ShowBuy(bool shown) => buyMark.SetActive(shown);

        // The top bar at TopInset below the safe area's top, one settings button height lower while room is kept for the
        // banner
        public void ReserveBannerSpace(bool bannerShown)
        {
            bannerSpace = bannerShown;
            var gap = bannerShown ? ((RectTransform)settingsButton.transform).rect.height : 0f;
            topBar.anchoredPosition = new Vector2(topBar.anchoredPosition.x, -(TopInset + gap));
        }

        // Into the one row above Play: the Left buttons first, then the Right ones, each side in the order added. A button a
        // feature hides (SetActive(false)) leaves no gap: the row lays out the shown ones only. The shown ones' captions
        // share one size.
        public IMenuSideButton AddSideButton(MenuSide side, Sprite icon, string labelKey, Action onClick)
        {
            var list = side == MenuSide.Left ? left : right;
            if (list.Count >= SideSlots)
                throw new InvalidOperationException($"the menu's {side} side holds {SideSlots} buttons");
            var button = Instantiate(sideTemplate, sideRow, false);
            button.name = $"{side}{list.Count}";
            if (side == MenuSide.Left) button.transform.SetSiblingIndex(left.Count);
            else button.transform.SetAsLastSibling();
            button.gameObject.SetActive(true);
            button.Setup(icon, labelKey, onClick);
            binder?.BindTree(button.gameObject);
            list.Add(button);
            var texts = new List<TMP_Text>();
            foreach (var each in left) texts.Add(each.Caption.GetComponent<TMP_Text>());
            foreach (var each in right) texts.Add(each.Caption.GetComponent<TMP_Text>());
            captions.Configure(CaptionMax, CaptionMin, CaptionFloor, 0f, texts.ToArray());
            return button;
        }

        // The scene's Play button serves the first game; smaller copies side by side in one row above the round buttons'
        // row serve the rest, the second game's on the left (the upstream template has the sample and both showcase
        // games, a generated project one): stacked, they would reach the logo on a portrait tablet. The row shows only
        // with copies. Copies are bound here: the scene was bound before.
        public void ShowGames(IReadOnlyList<string> labelKeys, UiBinder uiBinder)
        {
            for (var i = 0; i < labelKeys.Count; i++)
            {
                var button = i == 0 ? playButton : Instantiate(playButton, gameRow, false);
                if (i > 0)
                {
                    ((RectTransform)button.transform).sizeDelta = GameCopySize;
                }
                var index = i;
                button.onClick.AddListener(() => GameClicked?.Invoke(index));
                button.GetComponentInChildren<LocalizedText>(true).SetKey(labelKeys[i]);
                if (i > 0) uiBinder.BindTree(button.gameObject);
                gameButtons.Add(button);
            }
            playButton.gameObject.SetActive(labelKeys.Count > 0);
            gameRow.gameObject.SetActive(labelKeys.Count > 1);
        }

        public void ShowLevel(int index, int level)
        {
            if (index < 0 || index >= gameButtons.Count) return;
            gameButtons[index].GetComponentInChildren<LocalizedText>(true).SetKey(ShellKeys.Level, level);
        }

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnCoins() => CoinsClicked?.Invoke();
    }
}
