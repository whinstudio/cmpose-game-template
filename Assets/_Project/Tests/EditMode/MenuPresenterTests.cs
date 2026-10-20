using System.Collections.Generic;
using Kturker.UnityBase.App.Menu;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Features.Shell.GameShell;
using NUnit.Framework;

namespace Kturker.UnityBase.Tests.EditMode
{
    public sealed class MenuPresenterTests
    {
        private static readonly GameScene[] TwoGames = { TestGames.Game, new GameScene(SceneId.Menu, "other.play") };

        // The fake view ignores the binder, so tests pass none
        private static MenuPresenter Create(FakeMenuView view, FakeSceneLoader loader, FakeLog log,
            FakeSettingsPanel settings = null, FakeAboutPanel about = null, FakeUiFeedback feedback = null,
            IReadOnlyList<GameScene> games = null, ILevelProgress levels = null) =>
            new MenuPresenter(view, loader, games ?? TwoGames, null, new AppInfo("UnityBase", "1.0.0", "Android"),
                settings ?? new FakeSettingsPanel(), about ?? new FakeAboutPanel(), feedback ?? new FakeUiFeedback(), log,
                levels ?? new NoLevelProgress());

        [Test]
        public void Start_fills_the_view_and_marks_menu_ready()
        {
            var view = new FakeMenuView();
            var log = new FakeLog();
            Create(view, new FakeSceneLoader(), log).Start();

            Assert.That(view.Title, Is.EqualTo("UnityBase"));
            Assert.That(view.Version, Is.EqualTo("v1.0.0"));
            Assert.That(log.Lines, Does.Contain("MARKER CMPOSE_BOOT menu-ready"));
        }

        [Test]
        public void Start_shows_one_button_per_game_in_order()
        {
            var view = new FakeMenuView();
            Create(view, new FakeSceneLoader(), new FakeLog()).Start();

            Assert.That(view.GameKeys, Is.EqualTo(new[] { "menu.play", "other.play" }));
        }

        [Test]
        public void Menu_play_shows_current_level()
        {
            var view = new FakeMenuView();
            Create(view, new FakeSceneLoader(), new FakeLog(), levels: new FakeLevelProgress { Started = true, Current = 3 }).Start();

            Assert.That(view.LevelShown, Is.EqualTo((0, 3)), "the first game's Play reads Level 3");
        }

        [Test]
        public void Menu_play_stays_play_until_the_level_flow_starts()
        {
            var view = new FakeMenuView();
            Create(view, new FakeSceneLoader(), new FakeLog(), levels: new FakeLevelProgress { Current = 3 }).Start();

            Assert.That(view.LevelShown, Is.Null);
        }

        [Test]
        public void A_game_button_opens_its_scene_until_disposed()
        {
            var view = new FakeMenuView();
            var loader = new FakeSceneLoader();
            var feedback = new FakeUiFeedback();
            var presenter = Create(view, loader, new FakeLog(), feedback: feedback);
            presenter.Start();

            view.ClickGame(1);
            presenter.Dispose();
            view.ClickGame(0);

            Assert.That(loader.Requested, Is.EqualTo(new[] { SceneId.Menu }));
            Assert.That(feedback.Clicks, Is.EqualTo(1));
        }

        [Test]
        public void Settings_opens_its_panel_with_feedback_and_a_stray_index_does_nothing()
        {
            var view = new FakeMenuView();
            var settings = new FakeSettingsPanel();
            var feedback = new FakeUiFeedback();
            var loader = new FakeSceneLoader();
            Create(view, loader, new FakeLog(), settings, feedback: feedback).Start();

            view.ClickSettings();
            view.ClickGame(2);
            view.ClickGame(-1);

            Assert.That(settings.Opened, Is.EqualTo(1));
            Assert.That(feedback.Clicks, Is.EqualTo(1));
            Assert.That(loader.Requested, Is.Empty);
        }

        // About left the menu: the settings panel's last row opens it
        [Test]
        public void Start_offers_about_to_the_settings_panel()
        {
            var settings = new FakeSettingsPanel();
            var about = new FakeAboutPanel();
            Create(new FakeMenuView(), new FakeSceneLoader(), new FakeLog(), settings, about).Start();

            Assert.That(settings.Offered, Is.SameAs(about));
            Assert.That(about.Opened, Is.EqualTo(0), "not opened by itself");
        }
    }
}
