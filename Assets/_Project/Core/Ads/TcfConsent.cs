namespace Kturker.UnityBase.Core.Ads
{
    // The IAB TCF answer UMP stores on the device (IABTCF_gdprApplies, IABTCF_PurposeConsents). A player who refuses
    // in the UMP form can still get non-personalised ads (CanRequestAds stays true), so analytics reads purpose 1
    // (store and access information on a device) instead. null when GDPR does not apply (no TCF answer).
    public static class TcfConsent
    {
        public const string GdprAppliesKey = "IABTCF_gdprApplies";
        public const string PurposeConsentsKey = "IABTCF_PurposeConsents";

        public static bool? StorageConsent(int gdprApplies, string purposeConsents)
        {
            if (gdprApplies != 1) return null;
            return !string.IsNullOrEmpty(purposeConsents) && purposeConsents[0] == '1';
        }
    }
}
