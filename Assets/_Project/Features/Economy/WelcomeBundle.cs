using System;
using VContainer.Unity;

namespace Kturker.UnityBase.Features.Economy
{
    // Premium's welcome bundle: paid once, on the first successful premium purchase. A restore or a launch that finds
    // premium on raises no purchase event, and the save remembers that it was given.
    public sealed class WelcomeBundle : IInitializable, IDisposable
    {
        private readonly Economy economy;
        private readonly IPremiumPurchaseEvents events;
        private bool listening;

        public WelcomeBundle(Economy economy, IPremiumPurchaseEvents events)
        {
            this.economy = economy;
            this.events = events;
        }

        public bool Given => economy.Wallet.State.WelcomeGiven;

        public void Initialize()
        {
            if (listening) return;
            events.PremiumPurchased += OnPremiumPurchased;
            listening = true;
        }

        public void Dispose()
        {
            if (!listening) return;
            events.PremiumPurchased -= OnPremiumPurchased;
            listening = false;
        }

        private void OnPremiumPurchased()
        {
            if (!economy.Enabled || Given) return;
            var welcome = economy.Config.Welcome;
            economy.Wallet.State.WelcomeGiven = true;
            foreach (var booster in welcome.Boosters)
            {
                if (booster.Value > 0) economy.Wallet.AddQuietly(booster.Key, booster.Value);
            }
            economy.Grant(welcome.Coins, null, 0);
        }
    }
}
