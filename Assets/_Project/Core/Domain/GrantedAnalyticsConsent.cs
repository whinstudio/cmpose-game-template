using System;

namespace Kturker.UnityBase.Core.Domain
{
    // No consent form in the game: analytics is allowed and the answer never changes
    public sealed class GrantedAnalyticsConsent : IAnalyticsConsent
    {
        public bool? Granted => true;

        public event Action Changed
        {
            add { }
            remove { }
        }
    }
}
