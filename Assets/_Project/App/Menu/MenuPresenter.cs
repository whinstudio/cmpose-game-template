using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.App.About;
using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Navigation;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Shell.GameShell;

namespace Kturker.UnityBase.App.Menu
{
    // The menu: one Play button per game (the first reads "Level n" once the shell's level flow has begun), Settings and
    // About
    public sealed class MenuPresenter : Presenter<IMenuView>
    {
        private readonly ISceneLoader loader;
        private readonly IReadOnlyList<GameScene> games;
        private readonly UiBinder binder;
        private readonly AppInfo app;
        private readonly ISettingsPanel settings;
        private readonly IAboutPanel about;
        private readonly IUiFeedback feedback;
        private readonly ILog log;
        private readonly ILevelProgress levels;

        public MenuPresenter(IMenuView view, ISceneLoader loader, IReadOnlyList<GameScene> games, UiBinder binder, AppInfo app,
            ISettingsPanel settings, IAboutPanel about, IUiFeedback feedback, ILog log, ILevelProgress levels) : base(view)
        {
            this.loader = loader;
            this.games = games;
            this.binder = binder;
            this.app = app;
            this.settings = settings;
            this.about = about;
            this.feedback = feedback;
            this.log = log;
            this.levels = levels;
        }

        public override void Start()
        {
            View.SetTitle(app.ProductName);
            View.SetVersion($"v{app.Version}");
            View.ShowGames(games.Select(g => g.LabelKey).ToList(), binder);
            if (games.Count > 0 && levels.Started) View.ShowLevel(0, levels.Current);
            View.GameClicked += OnGame;
            View.SettingsClicked += OnSettings;
            View.AboutClicked += OnAbout;
            log.Marker("CMPOSE_BOOT menu-ready");
        }

        public override void Dispose()
        {
            View.GameClicked -= OnGame;
            View.SettingsClicked -= OnSettings;
            View.AboutClicked -= OnAbout;
        }

        private void OnGame(int index)
        {
            if (index < 0 || index >= games.Count) return;
            feedback.Click();
            loader.LoadAsync(games[index].Scene).Forget();
        }

        private void OnSettings()
        {
            feedback.Click();
            settings.Open();
        }

        private void OnAbout()
        {
            feedback.Click();
            about.Open();
        }
    }
}
