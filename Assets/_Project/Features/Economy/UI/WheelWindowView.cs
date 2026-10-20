using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Economy
{
    // SceneBuilder builds it into the economy windows: a centred modal with the title and the "i" button, the drawn
    // wheel (WheelDisc + WheelPaint) under a pointer with up to Slots labels (icon and amount) on its slices, the
    // status and ad lines, Spin or the ad's Spin, Close, and the odds panel over the wheel. The wheel turns on
    // unscaled time: SpinDuration seconds, SpinTurns whole turns clockwise, easing out. The labels ride on the slices
    // but stay upright (a Ferris wheel's cabins), so no amount ever reads upside down.
    public sealed class WheelWindowView : MonoBehaviour, IWheelWindowView
    {
        public const int Slots = 8;
        public const float SpinDuration = 3.2f;
        public const int SpinTurns = 5;
        // The labels' distance from the centre, a share of the wheel's radius
        public const float LabelRadius = 0.64f;

        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform disc;
        [SerializeField] private WheelDisc wheel;
        [SerializeField] private RectTransform[] labels;
        [SerializeField] private FallbackIcon[] labelIcons;
        [SerializeField] private LocalizedText[] labelAmounts;
        [SerializeField] private LocalizedText status;
        [SerializeField] private LocalizedText note;
        [SerializeField] private Button spinButton;
        [SerializeField] private Button spinAdButton;
        [SerializeField] private Button infoButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject odds;
        [SerializeField] private LocalizedText[] oddsLines;
        private UniTaskCompletionSource spinning;
        private CancellationTokenRegistration cancelled;
        private float from;
        private float to;
        private float elapsed;
        private bool canSpin;
        private bool busy;

        public event Action SpinClicked;
        public event Action SpinAdClicked;
        public event Action InfoClicked;
        public event Action CloseClicked;

        public int MaxSlices => labels.Length;
        public RectTransform Disc => disc;

        private void Awake()
        {
            KitScreens.Hide(panel);
            spinButton.onClick.AddListener(OnSpin);
            spinAdButton.onClick.AddListener(OnSpinAd);
            infoButton.onClick.AddListener(OnInfo);
            closeButton.onClick.AddListener(OnClose);
        }

        private void OnDestroy()
        {
            spinButton.onClick.RemoveListener(OnSpin);
            spinAdButton.onClick.RemoveListener(OnSpinAd);
            infoButton.onClick.RemoveListener(OnInfo);
            closeButton.onClick.RemoveListener(OnClose);
            Finish();
        }

        private void Update()
        {
            if (spinning == null) return;
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / SpinDuration);
            var eased = 1f - Mathf.Pow(1f - t, 3f);
            disc.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(from, to, eased));
            KeepLabelsUpright();
            if (t >= 1f) Finish();
        }

        public void SetOpen(bool open) => KitScreens.Set(panel, open);

        public void ShowSlices(IReadOnlyList<WheelSliceLabel> slices)
        {
            var count = Mathf.Max(1, slices.Count);
            wheel.SetSlices(count);
            var radius = disc.rect.width / 2f * LabelRadius;
            for (var i = 0; i < labels.Length; i++)
            {
                var visible = i < slices.Count;
                labels[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var angle = WheelDisc.Middle(i, count);
                var radians = angle * Mathf.Deg2Rad;
                labels[i].anchoredPosition = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * radius;
                var slice = slices[i];
                labelAmounts[i].SetLiteral(slice.Amount);
                var role = WheelPaint.LabelRole(i, count);
                labelAmounts[i].GetComponent<ThemedGraphic>().SetRole(role);
                labelIcons[i].Glyph.GetComponent<ThemedGraphic>().SetRole(role);
                if (slice.Currency != null) labelIcons[i].ShowCurrency(slice.Currency);
                else
                {
                    labelIcons[i].ShowName(slice.Icon, slice.LetterOf);
                    KitGlyph.Paint(labelIcons[i].Glyph, null);
                }
            }
            // Slice 0 under the pointer
            disc.localEulerAngles = new Vector3(0f, 0f, WheelDisc.Middle(0, count));
            KeepLabelsUpright();
        }

        // Each label turned back by the disc's angle: upright on screen wherever its slice is
        private void KeepLabelsUpright()
        {
            var upright = Quaternion.Inverse(disc.localRotation);
            foreach (var label in labels)
                if (label != null) label.localRotation = upright;
        }

        public void ShowControls(bool freeReady, bool adShown, string statusText, string noteText)
        {
            canSpin = freeReady;
            spinButton.gameObject.SetActive(freeReady || !adShown);
            spinAdButton.gameObject.SetActive(adShown);
            status.SetLiteral(statusText ?? string.Empty);
            note.gameObject.SetActive(noteText != null);
            if (noteText != null) note.SetLiteral(noteText);
            Interactable();
        }

        public void ShowOdds(IReadOnlyList<string> lines)
        {
            odds.SetActive(lines != null);
            if (lines == null) return;
            for (var i = 0; i < oddsLines.Length; i++)
            {
                var visible = i < lines.Count;
                oddsLines[i].gameObject.SetActive(visible);
                if (visible) oddsLines[i].SetLiteral(lines[i]);
            }
        }

        public UniTask SpinTo(int index, CancellationToken ct)
        {
            Finish();
            var count = Mathf.Max(1, wheel.Slices);
            from = disc.localEulerAngles.z;
            // Clockwise is a falling angle: SpinTurns turns down to the slice's middle
            to = WheelDisc.Middle(index, count);
            while (to > from - 360f * SpinTurns) to -= 360f;
            elapsed = 0f;
            spinning = new UniTaskCompletionSource();
            var task = spinning.Task;
            cancelled = ct.Register(Finish);
            return task;
        }

        public void SetBusy(bool value)
        {
            busy = value;
            Interactable();
        }

        // Stops the wheel on its slice
        private void Finish()
        {
            if (spinning == null) return;
            var done = spinning;
            spinning = null;
            cancelled.Dispose();
            if (disc != null)
            {
                disc.localEulerAngles = new Vector3(0f, 0f, to);
                KeepLabelsUpright();
            }
            done.TrySetResult();
        }

        private void Interactable()
        {
            spinButton.interactable = canSpin && !busy;
            spinAdButton.interactable = !busy;
            closeButton.interactable = !busy;
        }

        private void OnSpin() => SpinClicked?.Invoke();

        private void OnSpinAd() => SpinAdClicked?.Invoke();

        private void OnInfo() => InfoClicked?.Invoke();

        private void OnClose() => CloseClicked?.Invoke();
    }
}
