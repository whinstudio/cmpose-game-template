using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // Rounds a plain themed Image to the style's corner radius times scale: boxes that are not kit components (the
    // shop's rows and coin packs, scroll boxes, a selection bar) follow the family's shape too
    [RequireComponent(typeof(Image))]
    public sealed class KitRounded : KitElement
    {
        [SerializeField] private float scale = 1f;

        public float Scale => scale;

        public void SetScale(float value) => scale = value;

        protected override void Apply(Palette palette, ThemeStyle style) =>
            KitShape.Round(GetComponent<Image>(), (style != null ? style.CornerRadius : 0f) * scale);
    }
}
