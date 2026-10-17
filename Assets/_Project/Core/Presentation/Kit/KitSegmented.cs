using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A segmented picker (KitFactory.Segmented builds it): equal options on a pill Track, a Primary pill indicator
    // that slides under the chosen one. The chosen label is OnPrimary, the others Text.
    public sealed class KitSegmented : KitElement
    {
        [SerializeField] private Image track;
        [SerializeField] private Image indicator;
        [SerializeField] private Button[] options = Array.Empty<Button>();
        [SerializeField] private TMP_Text[] labels = Array.Empty<TMP_Text>();
        [SerializeField] private int selected;
        private KitTween slide;
        private bool listening;

        public int Selected => selected;
        public int Count => options.Length;
        public Image Indicator => indicator;
        public Button Option(int index) => options[index];
        public TMP_Text Label(int index) => labels[index];

        // Raised when the player taps another option (not by Select)
        public event Action<int> Changed;

        internal void Init(Image trackImage, Image indicatorImage, Button[] buttons, TMP_Text[] texts)
        {
            track = trackImage;
            indicator = indicatorImage;
            options = buttons;
            labels = texts;
            selected = 0;
            // Built at run time, Awake ran before the options were here
            if (Application.isPlaying) Listen();
        }

        private void Awake() => Listen();

        private void Listen()
        {
            if (listening || options.Length == 0) return;
            listening = true;
            for (var i = 0; i < options.Length; i++)
            {
                var index = i;
                options[i].onClick.AddListener(() => Tap(index));
            }
        }

        private void OnEnable()
        {
            slide.Snap(selected);
            Place();
        }

        private void Tap(int index)
        {
            if (index == selected) return;
            Select(index, true);
            Changed?.Invoke(index);
        }

        public void Select(int index, bool animate)
        {
            if (index < 0 || index >= options.Length) throw new ArgumentOutOfRangeException(nameof(index), index, null);
            selected = index;
            var timings = Timings;
            if (animate && isActiveAndEnabled) slide.Start(slide.Value, index, timings.Release, timings.ReleaseEase);
            else slide.Snap(index);
            Paint();
            Place();
        }

        private void Update()
        {
            if (slide.Step(Time.unscaledDeltaTime)) Place();
        }

        // The indicator covers option slide.Value (fractional while it slides)
        private void Place()
        {
            if (indicator == null || options.Length == 0) return;
            var width = 1f / options.Length;
            var rect = indicator.rectTransform;
            rect.anchorMin = new Vector2(slide.Value * width, 0f);
            rect.anchorMax = new Vector2((slide.Value + 1f) * width, 1f);
        }

        private void Paint()
        {
            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                var role = i == selected ? ThemeRole.OnPrimary : ThemeRole.Text;
                var themed = Themed(labels[i]);
                if (themed.Role != role) themed.SetRole(role);
            }
        }

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            if (track == null || indicator == null) return;
            KitShape.Round(track, KitSprites.Pill);
            KitShape.Round(indicator, KitSprites.Pill);
            Paint();
            Place();
        }
    }
}
