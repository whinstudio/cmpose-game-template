using UnityEngine;

namespace Kturker.UnityBase.Core.About
{
    // Per-game settings the generator edits: the legal URLs under the settings card (blank hides the link), the support
    // address (an https or mailto URL; blank hides the Support button), the minimum-version file (blank: no forced
    // update) and the numeric App Store ID (blank hides "Rate" on iOS). The third-party notices follow the game's
    // credits on the Credits panel.
    // The asset is Assets/_Project/Core/About/AppConfig.asset, set on the root scope prefab.
    public sealed class AppConfig : ScriptableObject
    {
        [SerializeField] private string privacyUrl = "";
        [SerializeField] private string termsUrl = "";
        [SerializeField] private string supportUrl = "";
        [SerializeField] private string minVersionUrl = "";
        [SerializeField] private string appStoreId = "";
        [SerializeField] private TextAsset thirdPartyNotices;

        public string PrivacyUrl => privacyUrl;
        public string TermsUrl => termsUrl;
        public string SupportUrl => supportUrl;
        public string MinVersionUrl => minVersionUrl;
        public string AppStoreId => appStoreId;
        public TextAsset ThirdPartyNotices => thirdPartyNotices;
    }
}
