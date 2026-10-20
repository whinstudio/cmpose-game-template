using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;

namespace Kturker.UnityBase.Core.Ads.Tests
{
    // Completes synchronously; Calls is shared with FakeTracking so tests can check the order of the steps
    public sealed class FakeGmaGateway : IGmaGateway
    {
        public readonly List<string> Calls;
        public string ConsentInfoError;
        public string ConsentFormError;
        public string PrivacyFormError;
        public Action OnConsentForm;
        public Action OnPrivacyForm;
        public bool? DebugEea;
        public bool InterstitialLoadSucceeds = true;
        public bool RewardedLoadSucceeds = true;
        public AdResult InterstitialReply = AdResult.Shown;
        public RewardResult RewardedReply = RewardResult.Rewarded;
        // When set, ShowInterstitialAsync waits for it (an ad is on screen)
        public UniTaskCompletionSource<AdResult> PendingInterstitial;
        public int InterstitialLoads;
        public int RewardedLoads;
        public int InterstitialShows;
        public int RewardedShows;
        public int BannerHides;
        public readonly List<string> LoadedUnits = new List<string>();
        public readonly List<string> Banners = new List<string>();
        public readonly List<BannerPosition> BannerPositions = new List<BannerPosition>();
        private Action<bool> bannerLoaded;

        public FakeGmaGateway(List<string> calls = null) => Calls = calls ?? new List<string>();

        public ConsentState Consent { get; set; } = ConsentState.NotRequired;
        public bool CanRequestAds { get; set; } = true;
        public bool PrivacyOptionsRequired { get; set; }

        public UniTask<string> RequestConsentInfoAsync(bool debugEea)
        {
            Calls.Add("consentInfo");
            DebugEea = debugEea;
            return UniTask.FromResult(ConsentInfoError);
        }

        public UniTask<string> ShowConsentFormIfRequiredAsync()
        {
            Calls.Add("consentForm");
            OnConsentForm?.Invoke();
            return UniTask.FromResult(ConsentFormError);
        }

        public UniTask<string> ShowPrivacyOptionsAsync()
        {
            Calls.Add("privacyForm");
            OnPrivacyForm?.Invoke();
            return UniTask.FromResult(PrivacyFormError);
        }

        public UniTask InitializeAsync()
        {
            Calls.Add("initialize");
            return UniTask.CompletedTask;
        }

        public UniTask<bool> LoadInterstitialAsync(string adUnitId)
        {
            InterstitialLoads++;
            LoadedUnits.Add(adUnitId);
            return UniTask.FromResult(InterstitialLoadSucceeds);
        }

        public UniTask<AdResult> ShowInterstitialAsync()
        {
            InterstitialShows++;
            return PendingInterstitial != null ? PendingInterstitial.Task : UniTask.FromResult(InterstitialReply);
        }

        public UniTask<bool> LoadRewardedAsync(string adUnitId)
        {
            RewardedLoads++;
            LoadedUnits.Add(adUnitId);
            return UniTask.FromResult(RewardedLoadSucceeds);
        }

        public UniTask<RewardResult> ShowRewardedAsync()
        {
            RewardedShows++;
            return UniTask.FromResult(RewardedReply);
        }

        public int BannerHeight { get; set; }

        public void ShowBanner(string adUnitId, BannerPosition position, Action<bool> loaded)
        {
            Banners.Add(adUnitId);
            BannerPositions.Add(position);
            bannerLoaded = loaded;
        }

        public void HideBanner() => BannerHides++;

        public void FinishBanner(bool loaded) => bannerLoaded(loaded);
    }

    public sealed class FakePremium : IPremiumStatus
    {
        public bool IsPremium { get; set; }
        public event Action Changed;
        public void Raise() => Changed?.Invoke();
    }

    public sealed class FakeTracking : ITrackingAuthorization
    {
        private readonly List<string> calls;
        public TrackingStatus Answer = TrackingStatus.Authorized;
        public int Requests;

        public FakeTracking(List<string> calls) => this.calls = calls;

        public TrackingStatus Status { get; set; } = TrackingStatus.NotDetermined;

        public UniTask<TrackingStatus> RequestAsync()
        {
            calls.Add("att");
            Requests++;
            Status = Answer;
            return UniTask.FromResult(Answer);
        }
    }

    public sealed class FakeAdsPrivacy : IAdsPrivacy
    {
        public bool OptionsRequired { get; set; }
        public event Action Changed;
        public int Shown;
        public void RaiseChanged() => Changed?.Invoke();

        public UniTask ShowOptionsAsync()
        {
            Shown++;
            return UniTask.CompletedTask;
        }
    }

    public sealed class FakeAds : IAds
    {
        public bool IsAvailable { get; set; } = true;
        public bool CapReached;
        public bool RewardedAvailable => IsAvailable && !CapReached;
        public int BannerHeight => BannerVisible ? 120 : 0;
        public event Action Changed;
        public readonly List<string> BannerCalls = new List<string>();
        public bool BannerVisible;
        public int Interstitials;
        public int Rewardeds;
        public RewardResult RewardedReply = RewardResult.Rewarded;
        // When set, ShowRewardedAsync waits for it (the ad is on screen)
        public UniTaskCompletionSource<RewardResult> PendingRewarded;

        public void RaiseChanged() => Changed?.Invoke();

        public UniTask<AdResult> ShowInterstitialAsync(CancellationToken cancellation)
        {
            Interstitials++;
            return UniTask.FromResult(AdResult.Shown);
        }

        public UniTask<RewardResult> ShowRewardedAsync(CancellationToken cancellation)
        {
            Rewardeds++;
            return PendingRewarded != null ? PendingRewarded.Task : UniTask.FromResult(RewardedReply);
        }

        public void ShowBanner()
        {
            BannerCalls.Add("show");
            BannerVisible = true;
        }

        public void HideBanner()
        {
            BannerCalls.Add("hide");
            BannerVisible = false;
        }
    }
}
