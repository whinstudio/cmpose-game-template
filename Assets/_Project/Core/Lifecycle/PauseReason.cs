namespace Kturker.UnityBase.Core.Lifecycle
{
    public enum PauseReason
    {
        // The pause button; only the pause panel adds or removes it
        User,

        // The app went to the background or lost focus
        Focus,

        // A window over the running game (the economy's booster offer)
        Window
    }
}
