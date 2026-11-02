using Kturker.UnityBase.Core.Audio;
using NUnit.Framework;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class AudioPreferencesTests
    {
        [Test]
        public void Defaults_are_music_0_8_sfx_1_and_not_muted()
        {
            var audio = new AudioPreferences(new FakePreferences());
            Assert.That(audio.Music, Is.EqualTo(0.8f));
            Assert.That(audio.Sfx, Is.EqualTo(1f));
            Assert.That(audio.Muted, Is.False);
            Assert.That(audio.EffectiveMusic, Is.EqualTo(0.8f));

            audio.Muted = true;

            Assert.That(audio.EffectiveMusic, Is.EqualTo(0f));
            Assert.That(audio.EffectiveSfx, Is.EqualTo(0f));
            Assert.That(audio.Music, Is.EqualTo(0.8f));
        }

        [Test]
        public void Levels_are_clamped_to_0_1()
        {
            var audio = new AudioPreferences(new FakePreferences()) { Music = 1.7f, Sfx = -0.2f };
            Assert.That(audio.Music, Is.EqualTo(1f));
            Assert.That(audio.Sfx, Is.EqualTo(0f));
        }

        [Test]
        public void A_new_instance_reads_the_written_values()
        {
            var prefs = new FakePreferences();
            var audio = new AudioPreferences(prefs) { Music = 0.25f, Sfx = 0.5f, Muted = true };

            var again = new AudioPreferences(prefs);

            Assert.That(again.Music, Is.EqualTo(0.25f));
            Assert.That(again.Sfx, Is.EqualTo(0.5f));
            Assert.That(again.Muted, Is.True);
            Assert.That(prefs.Values[AudioPreferences.MusicKey], Is.EqualTo(0.25f));
            Assert.That(prefs.Saves, Is.EqualTo(0));
            Assert.That(audio.Sfx, Is.EqualTo(0.5f));
        }

        [Test]
        public void Changed_fires_once_per_real_change()
        {
            var audio = new AudioPreferences(new FakePreferences());
            var changes = 0;
            audio.Changed += () => changes++;

            audio.Music = 0.5f;
            audio.Music = 0.5f;
            audio.Sfx = 1f;
            audio.Sfx = 3f;
            audio.Muted = false;
            audio.Muted = true;

            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void A_switched_off_channel_is_silent_but_keeps_its_level()
        {
            var prefs = new FakePreferences();
            var audio = new AudioPreferences(prefs) { Music = 0.5f, Sfx = 0.7f };
            var changes = 0;
            audio.Changed += () => changes++;
            Assert.That(audio.MusicOn, Is.True);
            Assert.That(audio.SfxOn, Is.True);

            audio.MusicOn = false;
            audio.MusicOn = false;
            Assert.That(changes, Is.EqualTo(1), "a real change only");
            Assert.That(audio.EffectiveMusic, Is.EqualTo(0f));
            Assert.That(audio.Music, Is.EqualTo(0.5f));
            Assert.That(audio.EffectiveSfx, Is.EqualTo(0.7f));
            Assert.That(prefs.Values[AudioPreferences.MusicOnKey], Is.EqualTo(false));

            audio.SfxOn = false;
            Assert.That(audio.EffectiveSfx, Is.EqualTo(0f));
            Assert.That(audio.Sfx, Is.EqualTo(0.7f));

            var again = new AudioPreferences(prefs);
            Assert.That(again.MusicOn, Is.False);
            Assert.That(again.SfxOn, Is.False);

            audio.MusicOn = true;
            Assert.That(audio.EffectiveMusic, Is.EqualTo(0.5f), "back at its stored level");
            Assert.That(changes, Is.EqualTo(3));
            Assert.That(AudioPreferences.MusicOnKey, Is.EqualTo("audio.music.on"));
            Assert.That(AudioPreferences.SfxOnKey, Is.EqualTo("audio.sfx.on"));
        }

        [Test]
        public void Corrupt_stored_values_read_as_defaults_or_clamped()
        {
            var prefs = new FakePreferences();
            prefs.SetFloat(AudioPreferences.MusicKey, float.NaN);
            prefs.SetFloat(AudioPreferences.SfxKey, 7f);
            var audio = new AudioPreferences(prefs);

            Assert.That(audio.Music, Is.EqualTo(0.8f));
            Assert.That(audio.Sfx, Is.EqualTo(1f));

            prefs.SetFloat(AudioPreferences.SfxKey, float.NegativeInfinity);
            Assert.That(audio.Sfx, Is.EqualTo(0f));

            audio.Music = float.NaN;
            Assert.That(audio.Music, Is.EqualTo(0.8f));
        }
    }
}
