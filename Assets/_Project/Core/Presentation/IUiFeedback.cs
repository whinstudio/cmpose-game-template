using Kturker.UnityBase.Core.Haptics;

namespace Kturker.UnityBase.Core.Presentation
{
    // What every button press does (Click: the click sound and a light haptic) and the named game events: a win or a
    // gain (Success), a loss (Failure), a hit of some weight (Impact)
    public interface IUiFeedback
    {
        void Click();
        void Success();
        void Failure();
        void Impact(HapticStrength strength);
    }
}
