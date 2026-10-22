using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A plain pill with texts on it (the HUD's counter and level, the menu's coins): a game skin's pill (GameSkin.Pill)
    // draws the body with its sprite and the texts in its text colour; without one the pill keeps the role and round
    // ends it was built with
    public sealed class KitChip : KitElement
    {
        [SerializeField] private Image body;
        [SerializeField] private TMP_Text[] labels = new TMP_Text[0];

        public Image Body => body;
        public IReadOnlyList<TMP_Text> Labels => labels;

        internal void Init(Image bodyImage, TMP_Text[] labelTexts)
        {
            body = bodyImage;
            labels = labelTexts ?? new TMP_Text[0];
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            if (body == null) return;
            var skin = GameSkin.Current;
            var pill = skin?.Pill;
            if (pill?.Sprite != null) KitShape.DressImage(body, pill.Sprite, skin.SliceScale, skin.Pixel);
            else KitShape.UndressImage(body, KitSprites.Pill);
            foreach (var label in labels)
                if (label != null) KitShape.Fix(label, pill?.Text);
        }
    }
}
