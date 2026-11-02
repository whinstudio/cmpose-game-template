using Kturker.UnityBase.Core.Presentation;
using VContainer;
using VContainer.Unity;

namespace Kturker.UnityBase.Core.Language
{
    // multiLang flag: picks the language when the root scope is built and gives the settings card its language choice
    public sealed class LanguageInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<ISystemLanguage, UnitySystemLanguage>(Lifetime.Singleton);
            builder.Register<LanguageSelector>(Lifetime.Singleton).As<ISettingsLanguage>().AsSelf();
            builder.RegisterBuildCallback(resolver => resolver.Resolve<LanguageSelector>().Apply());
        }
    }
}
