using System;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // A bottom sheet or a modal over a dimmed screen (KitFactory.Sheet builds it). Open shows it and plays the rise
    // (Bottom) or the pop (Center) on unscaled time, so it opens in the pause panel too; Close plays it back and hides
    // the object. Content goes under Content.
    public sealed class KitSheet : KitElement
    {
        // How far below its rest a bottom sheet starts, beyond its own height (its shadow)
        private const float Below = 120f;
        private const float PopFrom = 0.85f;

        [SerializeField] private SheetPlacement placement;
        [SerializeField] private CanvasGroup dim;
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private RectTransform content;
        private KitTween progress;
        private Vector2 rest;
        private bool restKnown;

        public SheetPlacement Placement => placement;
        public RectTransform Panel => panel;
        public RectTransform Content => content;
        public CanvasGroup Dim => dim;
        public bool IsOpen { get; private set; }
        // 0 hidden .. 1 open (beyond 1 while a spring overshoots)
        public float Progress => progress.Value;
        public bool Animating => progress.Running;

        // Opened as the sheet starts to open; Closed once it is hidden
        public event Action Opened;
        public event Action Closed;

        internal void Init(SheetPlacement where, CanvasGroup dimGroup, RectTransform panelRect, CanvasGroup group, RectTransform contentRect)
        {
            placement = where;
            dim = dimGroup;
            panel = panelRect;
            panelGroup = group;
            content = contentRect;
        }

        public void Open() => SetOpen(true, true);

        public void Close() => SetOpen(false, true);

        public void SetOpen(bool open, bool animate)
        {
            Remember();
            var target = open ? 1f : 0f;
            if (open == IsOpen && !progress.Running && progress.Value == target)
            {
                // Already there; a closed sheet left active in the scene hides
                gameObject.SetActive(open);
                return;
            }
            if (open) gameObject.SetActive(true);
            IsOpen = open;
            var timings = Timings;
            if (animate)
            {
                if (open) progress.Start(Mathf.Clamp01(progress.Value), 1f, timings.Sheet, timings.Enter);
                else progress.Start(Mathf.Clamp01(progress.Value), 0f, timings.Transition, Ease.OutCubic);
            }
            else
            {
                progress.Snap(open ? 1f : 0f);
            }
            Show();
            if (open) Opened?.Invoke();
            if (!progress.Running) Finish();
        }

        private void Update()
        {
            if (!progress.Step(Time.unscaledDeltaTime)) return;
            Show();
            if (!progress.Running) Finish();
        }

        private void Finish()
        {
            if (IsOpen) return;
            Closed?.Invoke();
            gameObject.SetActive(false);
        }

        private void Remember()
        {
            if (restKnown || panel == null) return;
            rest = panel.anchoredPosition;
            restKnown = true;
        }

        private void Show()
        {
            var p = progress.Value;
            if (dim != null)
            {
                dim.alpha = Mathf.Clamp01(p);
                dim.blocksRaycasts = IsOpen;
            }
            if (panel == null) return;
            if (panelGroup != null) panelGroup.interactable = IsOpen;
            if (placement == SheetPlacement.Bottom)
            {
                var hidden = rest.y - panel.rect.height - Below;
                panel.anchoredPosition = new Vector2(rest.x, Mathf.LerpUnclamped(hidden, rest.y, p));
                if (panelGroup != null) panelGroup.alpha = 1f;
            }
            else
            {
                var scale = Mathf.LerpUnclamped(PopFrom, 1f, p);
                panel.localScale = new Vector3(scale, scale, 1f);
                if (panelGroup != null) panelGroup.alpha = Mathf.Clamp01(p * 2f);
            }
        }

        // The skin sprite of the panel (its KitCard draws it): the sheet's at the bottom, the dialog's in the centre
        public static Sprite PanelSprite(GameSkin skin, SheetPlacement placement) =>
            placement == SheetPlacement.Bottom ? skin.Sheet : skin.Dialog;

        protected override void Apply(Palette palette, ThemeStyle style)
        {
        }
    }
}
