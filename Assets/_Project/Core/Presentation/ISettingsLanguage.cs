using System;
using System.Collections.Generic;

namespace Kturker.UnityBase.Core.Presentation
{
    // The settings card's language choice (multiLang flag): the supported languages by their own names, the one in use,
    // and whether it follows the device (no stored choice). Changed fires after the language in use may have changed.
    public interface ISettingsLanguage
    {
        IReadOnlyList<(string Code, string Name)> Languages { get; }
        string Current { get; }
        bool FollowsDevice { get; }

        // The language the device asks for: its code when supported, English otherwise (shown under "Device language")
        string Device { get; }

        // Stores the choice and switches to it; an unsupported code does nothing
        void Choose(string code);

        // Forgets the stored choice and switches to the device language (English when it is not supported)
        void FollowDevice();

        event Action Changed;
    }
}
