using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.App.Settings
{
    // The settings card: three switches, the optional language row, the buttons (RemoveAds as the one primary, the rest
    // in a grid), the legal links and the version; the language list and the credits open inside it
    public interface ISettingsView
    {
        event Action<bool> MusicToggled;
        event Action<bool> SfxToggled;
        event Action<bool> VibrationToggled;
        event Action<SettingsButton> ButtonClicked;
        event Action LanguageClicked;
        event Action LanguagesBackClicked;
        event Action PrivacyClicked;
        event Action TermsClicked;
        event Action CloseClicked;
        event Action CreditsCloseClicked;

        // A language's code, or null for "device language"
        event Action<string> LanguageChosen;

        void Show(bool music, bool sfx, bool vibration);

        // The short code ("TR"); null hides the row
        void ShowLanguageRow(string code);

        // RemoveAds first when present, then the grid order
        void ShowButtons(IReadOnlyList<SettingsButton> buttons);

        void ShowLinks(bool privacy, bool terms);

        // "v1.0.0"
        void ShowVersion(string label);

        // A text key; null clears
        void ShowMessage(string key);

        // selected: the chosen code, null while the device language is followed; currentName: the language in use
        void ShowLanguages(IReadOnlyList<(string Code, string Name)> languages, string selected, string currentName);

        void HideLanguages();
        void ShowCredits(string text);
        void HideCredits();
        void Hide();
    }
}
