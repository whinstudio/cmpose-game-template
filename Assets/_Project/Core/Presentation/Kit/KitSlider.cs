using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A themed Slider (KitFactory.Slider builds it): a pill Control rail, a Primary fill that ends under the handle's
    // centre, a round Primary handle with an OnPrimary dot and a soft shadow; the handle grows while it is held
    public sealed class KitSlider : KitElement, IPointerDownHandler, IPointerUpHandler
    {
        public const float HeldScale = 1.15f;

        [SerializeField] private Image rail;
        [SerializeField] private Image fill;
        [SerializeField] private Image handle;
        [SerializeField] private Image dot;
        [SerializeField] private Image handleShadow;
        private KitTween grow;

        public Image Rail => rail;
        public Image Fill => fill;
        public Image Handle => handle;
        public Slider Slider => GetComponent<Slider>();

        internal void Init(Image railImage, Image fillImage, Image handleImage, Image dotImage, Image shadowImage)
        {
            rail = railImage;
            fill = fillImage;
            handle = handleImage;
            dot = dotImage;
            handleShadow = shadowImage;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            var timings = Timings;
            grow.Start(CurrentScale, HeldScale, timings.Press, Ease.OutCubic);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            var timings = Timings;
            grow.Start(CurrentScale, 1f, timings.Release, timings.ReleaseEase);
        }

        private float CurrentScale => handle != null ? handle.rectTransform.parent.localScale.x : 1f;

        private void Update()
        {
            if (handle == null || !grow.Step(Time.unscaledDeltaTime)) return;
            var value = grow.Value;
            // The handle and its shadow share a parent (the slider's handleRect)
            handle.rectTransform.parent.localScale = new Vector3(value, value, 1f);
        }

        private void OnDisable()
        {
            grow.Snap(1f);
            if (handle != null) handle.rectTransform.parent.localScale = Vector3.one;
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            if (rail == null || fill == null || handle == null) return;
            KitShape.Round(rail, KitSprites.Pill);
            KitShape.Round(fill, KitSprites.Pill);
            KitShape.Round(handle, KitSprites.Pill);
            if (dot != null) KitShape.Round(dot, KitSprites.Pill);
            if (handleShadow != null)
            {
                var strength = style != null ? Mathf.Max(style.ShadowStrength, 0.25f) : 0.25f;
                KitShape.SoftShadow(handleShadow, 30f, strength);
            }
        }
    }
}
