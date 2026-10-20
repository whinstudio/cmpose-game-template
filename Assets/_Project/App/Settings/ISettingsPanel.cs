using Kturker.UnityBase.App.About;

namespace Kturker.UnityBase.App.Settings
{
    // Opens the scene's settings panel (Menu, or the Game's pause panel)
    public interface ISettingsPanel
    {
        bool IsOpen { get; }
        void Open();

        // Shows the panel's last row, About, which closes the panel and opens about (the menu's; game scenes have none,
        // so the row stays hidden there)
        void OfferAbout(IAboutPanel about);
    }
}
