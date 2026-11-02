using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.Core.Ads
{
    // The settings card's "Ad choices" button: shown only when UMP says the privacy options are required here; a press
    // opens UMP's privacy options form
    public sealed class AdPrivacyAction : ISettingsAction
    {
        private readonly IAdsPrivacy privacy;

        public AdPrivacyAction(IAdsPrivacy privacy)
        {
            this.privacy = privacy;
            privacy.Changed += OnChanged;
        }

        public SettingsButton Button => SettingsButton.AdPrivacy;
        public bool Visible => privacy.OptionsRequired;

        public event Action Changed;

        public async UniTask<string> PressAsync(CancellationToken ct)
        {
            if (Visible) await privacy.ShowOptionsAsync();
            return null;
        }

        private void OnChanged() => Changed?.Invoke();
    }
}
