using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.Core.Ads
{
    // IAds over Google Mobile Ads (through IGmaGateway). Off until AdsConsentFlow activates it; then it keeps one
    // interstitial and one rewarded ad loaded (LoadRetries more tries after 2, 4 and 8 seconds, then again on the
    // next request), refuses a second full-screen ad while one is open and asks the pacer before an interstitial.
    // Rewarded ads stop for the day once RewardedCap is reached; only one watched to the end counts. Premium players
    // see no forced ad (interstitials are Skipped, the banner hides at once) but still get the opt-in rewarded ads.
    // The banner's room (SafeArea.ReservedTop) is kept from the moment a banner is asked for while one may still come:
    // ads on or AdsConsentFlow not done yet, not premium. Content then never moves when the banner loads, fails or
    // refreshes, nor while an economy window holds the banner (HoldBanner: no banner, the room stays); it moves only
    // when the player turns premium, the scene stops asking (another scene) or the run turns out to have no ads
    // (Deactivate). An empty strip is the price; a banner that fails to load leaves it for the rest of that visit.
    public sealed class GmaAds : IAds, IDisposable
    {
        public const int LoadRetries = 3;
        private const string Interstitial = "interstitial";
        private const string Rewarded = "rewarded";
        private const string Banner = "banner";
        public const BannerPosition BannerAt = BannerPosition.Top;

        private readonly IGmaGateway gateway;
        private readonly IPremiumStatus premium;
        private readonly InterstitialPacer pacer;
        private readonly RewardedCap cap;
        private readonly AdUnits units;
        private readonly ILog log;
        private readonly Func<int, CancellationToken, UniTask> wait;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool interstitialReady;
        private bool interstitialLoading;
        private bool rewardedReady;
        private bool rewardedLoading;
        private bool showing;
        private bool bannerWanted;
        // An economy window over the menu: no banner, its room stays
        private bool bannerHeld;
        private bool bannerShown;
        // AdsConsentFlow has answered: Activate or Deactivate was called
        private bool settled;
        private int reserved;

        public GmaAds(IGmaGateway gateway, IPremiumStatus premium, InterstitialPacer pacer, RewardedCap cap, AdUnits units, ILog log,
            Func<int, CancellationToken, UniTask> wait)
        {
            this.gateway = gateway;
            this.premium = premium;
            this.pacer = pacer;
            this.cap = cap;
            this.units = units;
            this.log = log;
            this.wait = wait;
            if (premium != null) premium.Changed += OnPremiumChanged;
        }

        public bool IsAvailable { get; private set; }

        public bool RewardedAvailable => IsAvailable && !cap.Reached;

        public int BannerHeight => reserved;

        public event Action Changed;

        private bool IsPremium => premium != null && premium.IsPremium;

        // Seconds of real time: pausing the game sets the time scale to 0
        public static UniTask RealDelay(int seconds, CancellationToken cancellation) =>
            UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: cancellation);

        // AdsConsentFlow calls this once Google Mobile Ads has started and consent allows ads
        public void Activate()
        {
            settled = true;
            if (IsAvailable) return;
            IsAvailable = true;
            UpdateBanner();
            Refill();
            Changed?.Invoke();
        }

        // AdsConsentFlow calls this when consent no longer allows ads, or when its run ends without ads (the editor, a
        // capture run, a consent error in a release build): the banner's room goes
        public void Deactivate()
        {
            settled = true;
            var wasAvailable = IsAvailable;
            IsAvailable = false;
            UpdateBanner();
            if (wasAvailable) Changed?.Invoke();
        }

        public UniTask<AdResult> ShowInterstitialAsync(CancellationToken cancellation) =>
            InterstitialAsync().AttachExternalCancellation(cancellation);

        public UniTask<RewardResult> ShowRewardedAsync(CancellationToken cancellation) =>
            RewardedAsync().AttachExternalCancellation(cancellation);

        public void ShowBanner()
        {
            bannerWanted = true;
            UpdateBanner();
        }

        public void HideBanner()
        {
            bannerWanted = false;
            UpdateBanner();
        }

        public void HoldBanner(bool held)
        {
            bannerHeld = held;
            UpdateBanner();
        }

        public void Dispose()
        {
            if (premium != null) premium.Changed -= OnPremiumChanged;
            lifetime.Cancel();
        }

        private async UniTask<AdResult> InterstitialAsync()
        {
            if (IsPremium) return Mark(AdResult.Skipped);
            if (!IsAvailable || showing) return Mark(AdResult.NotReady);
            if (!pacer.Trigger()) return Mark(AdResult.Skipped);
            if (!interstitialReady)
            {
                LoadInterstitialAsync().Forget();
                return Mark(AdResult.NotReady);
            }
            interstitialReady = false;
            showing = true;
            AdResult result;
            try
            {
                result = await gateway.ShowInterstitialAsync();
            }
            finally
            {
                showing = false;
            }
            if (result == AdResult.Shown) pacer.Shown();
            LoadInterstitialAsync().Forget();
            return Mark(result);
        }

        private async UniTask<RewardResult> RewardedAsync()
        {
            if (!IsAvailable || showing || cap.Reached) return Mark(RewardResult.NotReady);
            if (!rewardedReady)
            {
                LoadRewardedAsync().Forget();
                return Mark(RewardResult.NotReady);
            }
            rewardedReady = false;
            showing = true;
            RewardResult result;
            try
            {
                result = await gateway.ShowRewardedAsync();
            }
            finally
            {
                showing = false;
            }
            LoadRewardedAsync().Forget();
            if (result == RewardResult.Rewarded)
            {
                cap.Count();
                if (cap.Reached) Changed?.Invoke();
            }
            return Mark(result);
        }

        private void Refill()
        {
            LoadInterstitialAsync().Forget();
            LoadRewardedAsync().Forget();
        }

        private async UniTaskVoid LoadInterstitialAsync()
        {
            if (interstitialLoading || interstitialReady || !IsAvailable || IsPremium) return;
            interstitialLoading = true;
            try
            {
                interstitialReady = await LoadWithRetriesAsync(Interstitial, () => gateway.LoadInterstitialAsync(units.Interstitial),
                    () => IsAvailable && !IsPremium);
            }
            catch (OperationCanceledException)
            {
                // disposed while waiting for the next try
            }
            finally
            {
                interstitialLoading = false;
            }
        }

        private async UniTaskVoid LoadRewardedAsync()
        {
            if (rewardedLoading || rewardedReady || !IsAvailable) return;
            rewardedLoading = true;
            try
            {
                rewardedReady = await LoadWithRetriesAsync(Rewarded, () => gateway.LoadRewardedAsync(units.Rewarded), () => IsAvailable);
            }
            catch (OperationCanceledException)
            {
                // disposed while waiting for the next try
            }
            finally
            {
                rewardedLoading = false;
            }
        }

        // One try and LoadRetries more after 2, 4, 8 seconds while the format is still wanted; then the next show request
        // starts again
        private async UniTask<bool> LoadWithRetriesAsync(string format, Func<UniTask<bool>> load, Func<bool> wanted)
        {
            for (var attempt = 0; ; attempt++)
            {
                if (await load()) return true;
                if (attempt == LoadRetries)
                {
                    log.Warning($"No {format} ad after {LoadRetries + 1} tries: the next request tries again");
                    return false;
                }
                await wait(2 << attempt, lifetime.Token);
                if (!wanted()) return false;
            }
        }

        private void UpdateBanner()
        {
            var show = bannerWanted && !bannerHeld && IsAvailable && !IsPremium;
            if (show != bannerShown)
            {
                bannerShown = show;
                if (show) gateway.ShowBanner(units.Banner, BannerAt, OnBannerLoaded);
                else gateway.HideBanner();
            }
            var keep = bannerWanted && !IsPremium && (IsAvailable || !settled);
            Reserve(!keep ? 0 : reserved > 0 ? reserved : Math.Max(0, gateway.ExpectedBannerHeight));
        }

        // The kept room stays through a failed load; a banner of another height than expected takes its own
        private void OnBannerLoaded(bool loaded)
        {
            Marker(Banner, loaded ? "Shown" : "Failed");
            if (!loaded || !bannerShown) return;
            var height = gateway.BannerHeight;
            if (height <= 0 || height == reserved) return;
            log.Info($"The banner is {height} px tall, {reserved} px were kept for it");
            Reserve(height);
        }

        private void Reserve(int pixels)
        {
            if (pixels == reserved) return;
            reserved = pixels;
            SafeArea.ReservedTop = BannerAt == BannerPosition.Top ? pixels : 0f;
            SafeArea.ReservedBottom = BannerAt == BannerPosition.Top ? 0f : pixels;
        }

        private void OnPremiumChanged()
        {
            UpdateBanner();
            Refill();
        }

        private AdResult Mark(AdResult result)
        {
            Marker(Interstitial, result.ToString());
            return result;
        }

        private RewardResult Mark(RewardResult result)
        {
            Marker(Rewarded, result.ToString());
            return result;
        }

        private void Marker(string format, string result) => log.Marker($"CMPOSE_AD format={format} result={result}");
    }
}
