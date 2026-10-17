using Kturker.UnityBase.Core.Audio;
using Kturker.UnityBase.Core.Haptics;
using Kturker.UnityBase.Core.Presentation;
using NUnit.Framework;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class HapticsTests
    {
        [Test]
        public void Pattern_maps_light_medium_heavy_to_10_40_80_ms_and_80_160_255_amplitude()
        {
            Assert.That(HapticPattern.DurationMs(HapticStrength.Light), Is.EqualTo(10L));
            Assert.That(HapticPattern.DurationMs(HapticStrength.Medium), Is.EqualTo(40L));
            Assert.That(HapticPattern.DurationMs(HapticStrength.Heavy), Is.EqualTo(80L));
            Assert.That(HapticPattern.Amplitude(HapticStrength.Light), Is.EqualTo(80));
            Assert.That(HapticPattern.Amplitude(HapticStrength.Medium), Is.EqualTo(160));
            Assert.That(HapticPattern.Amplitude(HapticStrength.Heavy), Is.EqualTo(255));
        }

        [Test]
        public void A_disabled_service_never_vibrates()
        {
            var prefs = new FakePreferences();
            var vibrator = new FakeVibrator();
            var haptics = new HapticsService(prefs, vibrator) { Enabled = false };

            haptics.Play(HapticStrength.Heavy);

            Assert.That(vibrator.Played, Is.Empty);
            Assert.That(prefs.Values[HapticsService.EnabledKey], Is.EqualTo(false));
        }

        [Test]
        public void Enabled_by_default_persisted_and_plays_the_requested_strength()
        {
            var prefs = new FakePreferences();
            var vibrator = new FakeVibrator();
            var haptics = new HapticsService(prefs, vibrator);
            Assert.That(haptics.Enabled, Is.True);

            haptics.Play(HapticStrength.Medium);
            haptics.Enabled = false;
            Assert.That(new HapticsService(prefs, vibrator).Enabled, Is.False);
            haptics.Enabled = true;
            haptics.Play(HapticStrength.Light);

            Assert.That(vibrator.Played, Is.EqualTo(new[] { HapticStrength.Medium, HapticStrength.Light }));
        }

        [Test]
        public void Ui_click_plays_the_click_sound_and_a_light_haptic()
        {
            var audio = new FakeAudioPlayer();
            var vibrator = new FakeVibrator();
            var prefs = new FakePreferences();
            var feedback = new UiFeedback(audio, new UiSounds(null), new HapticsService(prefs, vibrator));

            feedback.Click();
            prefs.SetBool(HapticsService.EnabledKey, false);
            feedback.Click();

            Assert.That(audio.SfxPlayed, Is.EqualTo(2));
            Assert.That(vibrator.Played, Is.EqualTo(new[] { HapticStrength.Light }));
        }

        [Test]
        public void Event_waveforms_pair_every_step_with_an_amplitude()
        {
            foreach (var e in new[] { HapticEvent.Success, HapticEvent.Failure })
            {
                var timings = HapticPattern.Timings(e);
                var amplitudes = HapticPattern.Amplitudes(e);
                Assert.That(amplitudes.Length, Is.EqualTo(timings.Length), $"{e}");
                Assert.That(timings, Has.All.GreaterThanOrEqualTo(0L), $"{e}");
                Assert.That(amplitudes, Has.All.InRange(0, 255), $"{e}");
                Assert.That(amplitudes[0], Is.Zero, $"{e} starts with a pause");
            }
            Assert.That(HapticPattern.Timings(HapticEvent.Failure).Length, Is.GreaterThan(HapticPattern.Timings(HapticEvent.Success).Length),
                "failure buzzes more often than success");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => HapticPattern.Timings((HapticEvent)99));
        }

        [Test]
        public void Ui_success_failure_and_impact_play_their_haptics_without_a_click()
        {
            var audio = new FakeAudioPlayer();
            var vibrator = new FakeVibrator();
            var prefs = new FakePreferences();
            var feedback = new UiFeedback(audio, new UiSounds(null), new HapticsService(prefs, vibrator));

            feedback.Success();
            feedback.Failure();
            feedback.Impact(HapticStrength.Heavy);

            Assert.That(vibrator.Events, Is.EqualTo(new[] { HapticEvent.Success, HapticEvent.Failure }));
            Assert.That(vibrator.Played, Is.EqualTo(new[] { HapticStrength.Heavy }));
            Assert.That(audio.SfxPlayed, Is.Zero);
        }

        [Test]
        public void A_disabled_service_plays_no_events()
        {
            var vibrator = new FakeVibrator();
            var haptics = new HapticsService(new FakePreferences(), vibrator) { Enabled = false };

            haptics.Play(HapticEvent.Success);
            haptics.Play(HapticEvent.Failure);

            Assert.That(vibrator.Events, Is.Empty);
        }
    }
}
