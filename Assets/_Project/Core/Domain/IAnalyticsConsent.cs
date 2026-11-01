using System;

namespace Kturker.UnityBase.Core.Domain
{
    // Whether the player allows analytics. The ads flag registers one that follows the consent form; without it
    // GrantedAnalyticsConsent is registered and analytics is allowed from the start.
    public interface IAnalyticsConsent
    {
        // Null while the player has not answered yet
        bool? Granted { get; }

        // After Granted changes
        event Action Changed;
    }
}
