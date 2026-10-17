using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A small pill with a short text (KitFactory.Badge builds it); Pop swells it once when its value changes
    public sealed class KitBadge : KitElement
    {
        private const float PopAmount = 0.25f;
        private const float PopSeconds = 0.35f;

        [SerializeField] private KitBadgeTone tone;
        [SerializeField] private Image body;
        [SerializeField] private TMP_Text label;
        private float popElapsed = -1f;

        public KitBadgeTone Tone => tone;
        public Image Body => body;
        public TMP_Text Label => label;

        internal void Init(KitBadgeTone value, Image bodyImage, TMP_Text labelText)
        {
            tone = value;
            body = bodyImage;
            label = labelText;
        }

        public void SetText(string text)
        {
            if (label != null) label.text = text;
        }

        public void Pop() => popElapsed = 0f;

        private void Update()
        {
            if (popElapsed < 0f) return;
            popElapsed += Time.unscaledDeltaTime;
            var t = popElapsed / PopSeconds;
            if (t >= 1f) popElapsed = -1f;
            var value = 1f + PopAmount * UiMotion.Punch(t);
            transform.localScale = new Vector3(value, value, 1f);
        }

        private void OnDisable()
        {
            popElapsed = -1f;
            transform.localScale = Vector3.one;
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            if (body == null) return;
            KitShape.Round(body, KitSprites.Pill);
            var bodyRole = tone == KitBadgeTone.Accent ? ThemeRole.Accent : ThemeRole.Primary;
            var labelRole = tone == KitBadgeTone.Accent ? ThemeRole.OnAccent : ThemeRole.OnPrimary;
            var themedBody = Themed(body);
            if (themedBody.Role != bodyRole) themedBody.SetRole(bodyRole);
            if (label == null) return;
            var themedLabel = Themed(label);
            if (themedLabel.Role != labelRole) themedLabel.SetRole(labelRole);
        }
    }
}
