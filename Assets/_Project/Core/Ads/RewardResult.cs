namespace Kturker.UnityBase.Core.Ads
{
    // Rewarded: watched to the end. Granted: the reward without an ad (GmaAds no longer
    // gives it: premium players watch the opt-in ads too). Dismissed: closed early, no reward.
    // NotReady / Failed as in AdResult; only Rewarded and Granted give the reward.
    public enum RewardResult
    {
        Rewarded,
        Granted,
        Dismissed,
        NotReady,
        Failed
    }
}
