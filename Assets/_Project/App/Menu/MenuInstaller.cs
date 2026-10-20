using Kturker.UnityBase.App.About;
using Kturker.UnityBase.App.Settings;
using Kturker.UnityBase.App.Update;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Economy;
using VContainer;
using VContainer.Unity;
// cmpose:showcase
using System;
using Kturker.UnityBase.Features.Showcase.Installer;
using UnityEngine;
// /cmpose:showcase

namespace Kturker.UnityBase.App.Menu
{
    public sealed class MenuInstaller : IInstaller
    {
        private readonly bool registerViews;

        public MenuInstaller(bool registerViews) => this.registerViews = registerViews;

        public void Install(IContainerBuilder builder)
        {
            if (registerViews)
            {
                builder.RegisterComponentInHierarchy<MenuView>().As<IMenuView>().As<IMenuSideButtons>().As<IMenuCoins>();
                builder.RegisterComponentInHierarchy<SettingsView>().As<ISettingsView>();
                builder.RegisterComponentInHierarchy<AboutView>().As<IAboutView>();
                builder.RegisterComponentInHierarchy<UpdateView>().As<IUpdateView>();
            }
            builder.RegisterEntryPoint<SceneUiBinding>();
            // RegisterEntryPoint registers every implemented interface, so ISettingsPanel/IAboutPanel resolve to these
            builder.RegisterEntryPoint<SettingsPresenter>();
            builder.RegisterEntryPoint<AboutPresenter>();
            builder.RegisterEntryPoint<UpdatePresenter>();
            builder.RegisterEntryPoint<MenuPresenter>();
            // The coins and the supply, wheel and login bonus buttons (nothing shows without GameEconomy.json)
            new EconomyMenuInstaller().Install(builder);
            // cmpose:showcase
            new ShowcaseMenuInstaller(registerViews
                ? () => UnityEngine.Object.FindAnyObjectByType<MenuView>().ExtraSlot
                : (Func<Transform>)null).Install(builder);
            // /cmpose:showcase
        }
    }
}
