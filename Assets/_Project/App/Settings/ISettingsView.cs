using System;
using System.Collections.Generic;

namespace Kturker.UnityBase.App.Settings
{
    public interface ISettingsView
    {
        event Action<float> MusicChanged;
        event Action<float> SfxChanged;
        event Action<bool> VibrationChanged;
        event Action RateClicked;
        event Action AboutClicked;
        event Action CreditsClicked;
        event Action CreditsCloseClicked;
        event Action CloseClicked;
        event Action<int> SectionClicked;
        void Show(float music, float sfx, bool vibration);
        void SetRateVisible(bool visible);
        void SetAboutVisible(bool visible);
        void SetCreditsVisible(bool visible);
        void ShowCredits(string text);
        void HideCredits();
        void ShowSections(IReadOnlyList<SettingsRow> rows);
        void Hide();
    }
}
