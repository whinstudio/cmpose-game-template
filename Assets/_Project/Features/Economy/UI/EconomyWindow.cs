using System;
using System.Threading;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.Features.Economy
{
    // A window the menu's side buttons open (supply, wheel, login bonus): never with the economy off, without its
    // feature in the game's file (Available) or during an AutoPlay run, the banner held hidden while it is open,
    // refreshed every second (Tick) for its timers
    public abstract class EconomyWindow : IDisposable
    {
        private readonly Func<bool> autoPlay;
        private CancellationTokenSource lifetime = new CancellationTokenSource();

        protected EconomyWindow(Economy economy, IEconomyAds ads, IUiFeedback feedback, Func<bool> autoPlay)
        {
            Economy = economy;
            Ads = ads;
            Feedback = feedback;
            this.autoPlay = autoPlay;
        }

        public bool IsOpen { get; private set; }

        protected Economy Economy { get; }
        protected IEconomyAds Ads { get; }
        protected IUiFeedback Feedback { get; }
        // Cancelled when the window closes (an ad or a spin in progress gives nothing to a closed window)
        protected CancellationToken Token => lifetime.Token;
        // An ad or a spin is in progress: Close waits
        protected bool Busy { get; set; }

        public bool Open()
        {
            if (IsOpen || !Economy.Enabled || !Available || autoPlay()) return false;
            IsOpen = true;
            OnOpen();
            Refresh();
            SetViewOpen(true);
            Ads.HoldBanner(true);
            return true;
        }

        public void Tick()
        {
            if (IsOpen && !Busy) Refresh();
        }

        public virtual void Dispose()
        {
            Close();
            lifetime.Dispose();
        }

        protected void OnCloseClicked()
        {
            if (!IsOpen || Busy) return;
            Feedback.Click();
            Close();
        }

        protected void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Busy = false;
            lifetime.Cancel();
            lifetime.Dispose();
            lifetime = new CancellationTokenSource();
            SetViewOpen(false);
            Ads.HoldBanner(false);
        }

        // The game's file has this window's feature
        protected abstract bool Available { get; }

        protected virtual void OnOpen()
        {
        }

        protected abstract void SetViewOpen(bool open);

        protected abstract void Refresh();
    }
}
