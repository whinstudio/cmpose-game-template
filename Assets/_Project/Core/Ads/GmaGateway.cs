using System;
using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using Kturker.UnityBase.Core.Domain;

namespace Kturker.UnityBase.Core.Ads
{
    // The only class that sees Google Mobile Ads and UMP. Answers are moved to Unity's main thread before they are
    // returned (banner events through MobileAdsEventExecutor). Nothing throws: a failure is a warning plus a Failed,
    // false or error-message result. GmaAds keeps the banner's room on screen (SafeArea) from ExpectedBannerHeight.
    public sealed class GmaGateway : IGmaGateway
    {
        private readonly ILog log;
        private InterstitialAd interstitial;
        private RewardedAd rewarded;
        private BannerView banner;
        private int bannerHeight;
        // The last loaded banner's height: the next one on this screen is as tall
        private int loadedHeight;
        // The device's size could not be read: warned once per run
        private bool sizeWarned;

        public GmaGateway(ILog log) => this.log = log;

        public ConsentState Consent
        {
            get
            {
                switch (ConsentInformation.ConsentStatus)
                {
                    case ConsentStatus.NotRequired: return ConsentState.NotRequired;
                    case ConsentStatus.Required: return ConsentState.Required;
                    case ConsentStatus.Obtained: return ConsentState.Obtained;
                    default: return ConsentState.Unknown;
                }
            }
        }

        public int BannerHeight => banner == null ? 0 : bannerHeight;

        public int ExpectedBannerHeight
        {
            get
            {
                if (loadedHeight > 0) return loadedHeight;
                try
                {
                    var scale = MobileAds.Utils.GetDeviceScale();
                    if (scale <= 0f) return 0;
                    return AnchoredBanner.HeightPixels(MobileAds.Utils.GetDeviceSafeWidth(), UnityEngine.Screen.height / scale, scale);
                }
                catch (Exception e)
                {
                    if (!sizeWarned) log.Warning($"Banner height unknown before it loads: {e.Message}");
                    sizeWarned = true;
                    return 0;
                }
            }
        }

        public bool CanRequestAds => ConsentInformation.CanRequestAds();

        public bool PrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public async UniTask<string> RequestConsentInfoAsync(bool debugEea)
        {
            var parameters = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
            if (debugEea) parameters.ConsentDebugSettings = new ConsentDebugSettings { DebugGeography = DebugGeography.EEA };
            var source = new UniTaskCompletionSource<string>();
            ConsentInformation.Update(parameters, error => source.TrySetResult(Message(error)));
            return await OnMainThread(source.Task);
        }

        public async UniTask<string> ShowConsentFormIfRequiredAsync()
        {
            var source = new UniTaskCompletionSource<string>();
            ConsentForm.LoadAndShowConsentFormIfRequired(error => source.TrySetResult(Message(error)));
            return await OnMainThread(source.Task);
        }

        public async UniTask<string> ShowPrivacyOptionsAsync()
        {
            var source = new UniTaskCompletionSource<string>();
            ConsentForm.ShowPrivacyOptionsForm(error => source.TrySetResult(Message(error)));
            return await OnMainThread(source.Task);
        }

        public async UniTask InitializeAsync()
        {
            var source = new UniTaskCompletionSource<bool>();
            MobileAds.Initialize(_ => source.TrySetResult(true));
            await OnMainThread(source.Task);
        }

        public async UniTask<bool> LoadInterstitialAsync(string adUnitId)
        {
            if (interstitial != null) interstitial.Destroy();
            interstitial = null;
            var source = new UniTaskCompletionSource<(InterstitialAd Ad, string Error)>();
            InterstitialAd.Load(adUnitId, new AdRequest(), (ad, error) =>
            {
                if (error == null && ad != null) source.TrySetResult((ad, null));
                else source.TrySetResult((null, error == null ? "no ad" : error.GetMessage()));
            });
            var (loaded, message) = await OnMainThread(source.Task);
            if (message != null) log.Warning($"Interstitial did not load: {message}");
            interstitial = loaded;
            return loaded != null;
        }

        public async UniTask<AdResult> ShowInterstitialAsync()
        {
            var ad = interstitial;
            interstitial = null;
            if (ad == null || !ad.CanShowAd())
            {
                if (ad != null) ad.Destroy();
                return AdResult.Failed;
            }
            var source = new UniTaskCompletionSource<AdResult>();
            ad.OnAdFullScreenContentClosed += () => source.TrySetResult(AdResult.Shown);
            ad.OnAdFullScreenContentFailed += error =>
            {
                log.Warning($"Interstitial did not show: {error.GetMessage()}");
                source.TrySetResult(AdResult.Failed);
            };
            ad.Show();
            var result = await OnMainThread(source.Task);
            ad.Destroy();
            return result;
        }

        public async UniTask<bool> LoadRewardedAsync(string adUnitId)
        {
            if (rewarded != null) rewarded.Destroy();
            rewarded = null;
            var source = new UniTaskCompletionSource<(RewardedAd Ad, string Error)>();
            RewardedAd.Load(adUnitId, new AdRequest(), (ad, error) =>
            {
                if (error == null && ad != null) source.TrySetResult((ad, null));
                else source.TrySetResult((null, error == null ? "no ad" : error.GetMessage()));
            });
            var (loaded, message) = await OnMainThread(source.Task);
            if (message != null) log.Warning($"Rewarded ad did not load: {message}");
            rewarded = loaded;
            return loaded != null;
        }

        // The reward callback can arrive just after the close event (iOS raises it on the next main-thread update),
        // so a close without a reward waits two frames before it counts as Dismissed
        public async UniTask<RewardResult> ShowRewardedAsync()
        {
            var ad = rewarded;
            rewarded = null;
            if (ad == null || !ad.CanShowAd())
            {
                if (ad != null) ad.Destroy();
                return RewardResult.Failed;
            }
            var earned = false;
            var source = new UniTaskCompletionSource<RewardResult>();
            ad.OnAdFullScreenContentClosed += () => source.TrySetResult(RewardResult.Dismissed);
            ad.OnAdFullScreenContentFailed += error =>
            {
                log.Warning($"Rewarded ad did not show: {error.GetMessage()}");
                source.TrySetResult(RewardResult.Failed);
            };
            ad.Show(_ => earned = true);
            var result = await OnMainThread(source.Task);
            if (result == RewardResult.Dismissed && !earned) await UniTask.DelayFrame(2);
            ad.Destroy();
            return result == RewardResult.Dismissed && earned ? RewardResult.Rewarded : result;
        }

        public void ShowBanner(string adUnitId, BannerPosition position, Action<bool> loaded)
        {
            HideBanner();
            var size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(MobileAds.Utils.GetDeviceSafeWidth());
            var view = new BannerView(adUnitId, size, position == BannerPosition.Top ? AdPosition.Top : AdPosition.Bottom);
            var reported = false;
            banner = view;
            view.OnBannerAdLoaded += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (banner != view) return;
                bannerHeight = (int)view.GetHeightInPixels();
                if (bannerHeight > 0) loadedHeight = bannerHeight;
                if (reported) return;
                reported = true;
                loaded(true);
            });
            view.OnBannerAdLoadFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (banner != view || reported) return;
                reported = true;
                log.Warning($"Banner did not load: {error.GetMessage()}");
                loaded(false);
            });
            view.LoadAd(new AdRequest());
        }

        public void HideBanner()
        {
            if (banner == null) return;
            banner.Destroy();
            banner = null;
            bannerHeight = 0;
        }

        private static string Message(FormError error) => error == null ? null : error.Message ?? "unknown error";

        private static async UniTask<T> OnMainThread<T>(UniTask<T> task)
        {
            var result = await task;
            await UniTask.SwitchToMainThread();
            return result;
        }
    }
}
