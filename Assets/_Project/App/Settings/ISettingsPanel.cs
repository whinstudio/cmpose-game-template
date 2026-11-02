namespace Kturker.UnityBase.App.Settings
{
    // Opens the scene's settings panel (Menu, or the Game's pause panel)
    public interface ISettingsPanel
    {
        bool IsOpen { get; }
        void Open();
    }
}
