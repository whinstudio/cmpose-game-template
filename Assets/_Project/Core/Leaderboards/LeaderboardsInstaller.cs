using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Preferences;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.Core.Leaderboards
{
    // leaderboards flag: IGameServices over Google Play Games (Android) or Game Center (iOS), started at boot with a
    // silent sign-in. The editor and placeholder IDs register the no-op gateway: the service reports available=false.
    public sealed class LeaderboardsInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            var ios = Application.platform == RuntimePlatform.IPhonePlayer;
            var ids = LeaderboardsConfig.Ids(ios);
            var configured = !Application.isEditor && LeaderboardsConfig.IsConfigured(ids);
            var warnPlaceholders = !Application.isEditor && !Debug.isDebugBuild && !configured;
            builder.Register<IGameServicesGateway>(resolver => GameServicesGateways.Create(configured, ids, resolver.Resolve<ILog>()),
                Lifetime.Singleton);
            builder.RegisterEntryPoint<GameServices>(resolver => new GameServices(resolver.Resolve<IGameServicesGateway>(),
                    resolver.Resolve<IPreferences>(), resolver.Resolve<ILog>(), configured, warnPlaceholders), Lifetime.Singleton)
                .AsSelf();
            builder.Register<ISettingsAction, SignInAction>(Lifetime.Singleton);
        }
    }
}
