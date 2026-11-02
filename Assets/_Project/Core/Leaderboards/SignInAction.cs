using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.Core.Leaderboards
{
    // The settings card's "Sign in" button (game services): shown while the service is set up and the player is
    // signed out; a press asks to sign in. Hidden in the editor and with placeholder IDs, and once signed in.
    public sealed class SignInAction : ISettingsAction
    {
        private readonly IGameServices services;

        public SignInAction(IGameServices services)
        {
            this.services = services;
            services.Changed += OnChanged;
        }

        public SettingsButton Button => SettingsButton.SignIn;
        public bool Visible => services.IsAvailable && !services.IsSignedIn;

        public event Action Changed;

        public async UniTask<string> PressAsync(CancellationToken ct)
        {
            if (Visible) await services.SignInAsync(ct);
            return null;
        }

        private void OnChanged() => Changed?.Invoke();
    }
}
