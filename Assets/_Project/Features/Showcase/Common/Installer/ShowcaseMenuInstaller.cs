using System;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using Kturker.UnityBase.Features.Showcase.Common;
using UnityEngine;
using VContainer;
using VContainer.Unity;
// cmpose:purchases
using Kturker.UnityBase.Features.Showcase.Common.Purchases;
// /cmpose:purchases
// cmpose:leaderboards
using Kturker.UnityBase.Features.Showcase.Common.Leaderboards;
// /cmpose:leaderboards
// cmpose:saveSystem
using Kturker.UnityBase.Features.Showcase.Common.Save;
// /cmpose:saveSystem

namespace Kturker.UnityBase.Features.Showcase.Installer
{
    // The showcase's rows in the menu: the best score, then each flag's part (leaderboards: round buttons of the menu's
    // side columns, IMenuSideButtons). slot finds the menu's extra slot; it is null when a test registers fake views
    // instead.
    public sealed class ShowcaseMenuInstaller : IInstaller
    {
        private readonly Func<Transform> slot;

        public ShowcaseMenuInstaller(Func<Transform> slot) => this.slot = slot;

        public void Install(IContainerBuilder builder)
        {
            builder.Register<BestScore>(Lifetime.Scoped);
            if (slot != null)
            {
                builder.Register<IShowcaseMenuView>(resolver => ShowcaseMenuView.Spawn(slot(), resolver.Resolve<UiBinder>()),
                    Lifetime.Scoped);
            }
            builder.RegisterEntryPoint<ShowcaseMenuPresenter>();
            // cmpose:purchases
            if (slot != null)
            {
                builder.Register<IPremiumButtonView>(resolver => PremiumButtonView.Spawn(slot(), resolver.Resolve<UiBinder>()),
                    Lifetime.Scoped);
            }
            builder.RegisterEntryPoint<ShowcasePremiumPresenter>();
            // /cmpose:purchases
            // cmpose:saveSystem
            builder.Register<RunStats>(Lifetime.Scoped);
            if (slot != null)
            {
                builder.Register<IRunStatsView>(resolver => RunStatsView.Spawn(slot(), resolver.Resolve<UiBinder>()), Lifetime.Scoped);
            }
            builder.RegisterEntryPoint<RunStatsPresenter>();
            // /cmpose:saveSystem
            // cmpose:leaderboards
            if (slot != null)
            {
                builder.Register<IShowcaseScoresView>(resolver => ShowcaseScoresView.Spawn(slot(), resolver.Resolve<IMenuSideButtons>(),
                    resolver.Resolve<UiBinder>()), Lifetime.Scoped);
            }
            builder.RegisterEntryPoint<ShowcaseScoresPresenter>();
            // /cmpose:leaderboards
        }
    }
}
