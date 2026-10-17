using System.Collections;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Kturker.UnityBase.Tests.PlayMode
{
    // The kit's motion at Time.timeScale = 0 (the pause panel): everything runs on unscaled time
    public sealed class KitMotionTests
    {
        // Frames in a batchmode editor can be long; a tween ends on the first frame past its duration
        private const float Slack = 0.15f;

        private GameObject canvas;
        private Palette palette;
        private IThemeSource theme;

        [SetUp]
        public void Pause()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            palette = ScriptableObject.CreateInstance<Palette>();
            theme = new FixedThemeSource(palette, ThemeStyle.Load());
            Time.timeScale = 0f;
        }

        [TearDown]
        public void Resume()
        {
            Time.timeScale = 1f;
            Object.Destroy(canvas);
            Object.Destroy(palette);
        }

        private MotionTimings Timings => UiMotion.Timings(theme.Style.Motion);

        private static async UniTask Realtime(float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) await UniTask.Yield();
        }

        [UnityTest]
        public IEnumerator PressFeedbackPlaysWhilePaused() => UniTask.ToCoroutine(async () =>
        {
            var button = KitFactory.Button(canvas.transform, "Resume", KitButtonVariant.Primary);
            button.Bind(theme);
            var press = button.GetComponent<PressFeedback>();
            var timings = Timings;

            press.OnPointerDown(new PointerEventData(EventSystem.current));
            await Realtime(timings.Press + Slack);
            Assert.That(button.transform.localScale.x, Is.EqualTo(timings.PressScale).Within(0.001f), "pressed down at timeScale 0");
            Assert.That(press.Busy, Is.True);

            press.OnPointerUp(new PointerEventData(EventSystem.current));
            await Realtime(timings.Release + Slack);
            Assert.That(button.transform.localScale, Is.EqualTo(Vector3.one), "back to 1 within the release time");
            Assert.That(press.Busy, Is.False);
            Assert.That(Time.timeScale, Is.Zero);
        });

        [UnityTest]
        public IEnumerator SheetOpensWhilePaused() => UniTask.ToCoroutine(async () =>
        {
            var sheet = KitFactory.Sheet(canvas.transform, "Sheet", SheetPlacement.Bottom, 900f);
            sheet.Bind(theme);
            var timings = Timings;

            sheet.Open();
            Assert.That(sheet.gameObject.activeSelf, Is.True);
            await UniTask.Yield();
            Assert.That(sheet.Panel.anchoredPosition.y, Is.LessThan(-KitFactory.SheetOverhang - 100f), "starts below the screen");
            await Realtime(timings.Sheet + Slack);
            Assert.That(sheet.Animating, Is.False);
            Assert.That(sheet.Progress, Is.EqualTo(1f));
            Assert.That(sheet.Dim.alpha, Is.EqualTo(1f));
            Assert.That(sheet.Panel.anchoredPosition.y, Is.EqualTo(-KitFactory.SheetOverhang).Within(0.01f));

            sheet.Close();
            await Realtime(timings.Transition + Slack);
            Assert.That(sheet.gameObject.activeSelf, Is.False);
            Assert.That(sheet.IsOpen, Is.False);
        });

        [UnityTest]
        public IEnumerator Switch_slides_and_count_up_counts_while_paused() => UniTask.ToCoroutine(async () =>
        {
            var toggle = KitFactory.Switch(canvas.transform, "Music");
            var score = KitFactory.CountUp(canvas.transform, "Score", TextRole.Display, 96f);
            toggle.Bind(theme);
            score.Bind(theme);
            var timings = Timings;
            await UniTask.Yield();

            toggle.Toggle.isOn = false;
            score.CountTo(120);
            await Realtime(Mathf.Max(timings.Release, timings.CountUp) + Slack);
            Assert.That(toggle.Position, Is.EqualTo(0f));
            Assert.That(score.Value, Is.EqualTo(120));
        });

        [UnityTest]
        public IEnumerator A_segmented_picker_built_at_run_time_answers_taps() => UniTask.ToCoroutine(async () =>
        {
            var picker = KitFactory.Segmented(canvas.transform, "Difficulty", 3);
            picker.Bind(theme);
            var changes = new System.Collections.Generic.List<int>();
            picker.Changed += changes.Add;
            await UniTask.Yield();

            picker.Option(2).onClick.Invoke();
            picker.Option(2).onClick.Invoke();
            Assert.That(picker.Selected, Is.EqualTo(2));
            Assert.That(changes, Is.EqualTo(new[] { 2 }), "a tap on the chosen option changes nothing");
            await Realtime(Timings.Release + Slack);
            Assert.That(picker.Indicator.rectTransform.anchorMin.x, Is.EqualTo(2f / 3f).Within(0.001f));
        });

        [UnityTest]
        public IEnumerator Decor_drifts_while_paused() => UniTask.ToCoroutine(async () =>
        {
            var decor = KitFactory.Decor(canvas.transform, "Circle", KitDecorShape.Circle, ThemeRole.Primary, 0.1f, 200f, 0f, 20f, 0f);
            decor.Bind(theme);
            var rect = (RectTransform)decor.transform;
            await UniTask.Yield();
            var start = rect.anchoredPosition.y;
            // A quarter of the cycle (KitDecor.Breaths breaths) at timeScale 0 lifts it most of its drift
            await Realtime(Timings.Breath * KitDecor.Breaths / 4f);
            Assert.That(rect.anchoredPosition.y - start, Is.GreaterThan(10f), "moved at timeScale 0");
            Assert.That(Time.timeScale, Is.Zero);
        });
    }
}
