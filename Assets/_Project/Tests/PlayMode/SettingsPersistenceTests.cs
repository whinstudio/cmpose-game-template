using System.Collections;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.App.Menu;
using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.Core.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Kturker.UnityBase.Tests.PlayMode
{
    // Writes the editor's real PlayerPrefs; the previous SFX switch is put back afterwards
    public sealed class SettingsPersistenceTests
    {
        private bool hadSfx;
        private int previousSfx;

        [SetUp]
        public void Remember()
        {
            hadSfx = PlayerPrefs.HasKey(AudioPreferences.SfxOnKey);
            previousSfx = PlayerPrefs.GetInt(AudioPreferences.SfxOnKey);
        }

        [TearDown]
        public void Restore()
        {
            if (hadSfx) PlayerPrefs.SetInt(AudioPreferences.SfxOnKey, previousSfx);
            else PlayerPrefs.DeleteKey(AudioPreferences.SfxOnKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator Sfx_switch_survives_reloading_the_menu() => UniTask.ToCoroutine(async () =>
        {
            await SceneManager.LoadSceneAsync("Boot").ToUniTask();
            var menu = await WaitFor<MenuView>(10f);
            Assert.That(menu, Is.Not.Null, "Menu did not open within 10 s");
            await Frames(2);

            PlayerPrefs.SetInt(AudioPreferences.SfxOnKey, 1);
            menu.SettingsButton.onClick.Invoke();
            var settings = Object.FindFirstObjectByType<SettingsView>();
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(settings.SfxSwitch.isOn, Is.True);
            settings.SfxSwitch.isOn = false;
            settings.CloseButton.onClick.Invoke();
            Assert.That(settings.IsOpen, Is.False);

            await SceneManager.LoadSceneAsync("Menu").ToUniTask();
            var reloaded = await WaitFor<MenuView>(10f);
            Assert.That(reloaded, Is.Not.SameAs(menu));
            await Frames(2);

            reloaded.SettingsButton.onClick.Invoke();
            var reopened = Object.FindFirstObjectByType<SettingsView>();
            Assert.That(reopened, Is.Not.SameAs(settings));
            Assert.That(reopened.IsOpen, Is.True);
            Assert.That(reopened.SfxSwitch.isOn, Is.False);
            Assert.That(PlayerPrefs.GetInt(AudioPreferences.SfxOnKey), Is.EqualTo(0));
        });

        // Presenters start in the player loop after the scene's Awake; give them a frame or two
        private static async UniTask Frames(int count)
        {
            for (var i = 0; i < count; i++) await UniTask.Yield();
        }

        private static async UniTask<T> WaitFor<T>(float seconds) where T : Object
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                var found = Object.FindFirstObjectByType<T>();
                if (found != null) return found;
                await UniTask.Yield();
            }
            return null;
        }
    }
}
