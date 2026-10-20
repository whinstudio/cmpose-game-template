using UnityEngine;

namespace Kturker.UnityBase.Features.Economy
{
    // The economy windows' overlay (SceneBuilder: Resources/Economy/EconomyWindows, a canvas sorting 700): the supply,
    // wheel and login bonus windows the menu opens, the custom extras' frame, the pre-run picker and, over it, the
    // booster offer
    public sealed class EconomyWindowsView : MonoBehaviour
    {
        [SerializeField] private SupplyWindowView supply;
        [SerializeField] private WheelWindowView wheel;
        [SerializeField] private LoginBonusView login;
        [SerializeField] private ExtraWindowView extra;
        [SerializeField] private BoosterPickerView picker;
        [SerializeField] private BoosterOfferView offer;

        public SupplyWindowView Supply => supply;
        public WheelWindowView Wheel => wheel;
        public LoginBonusView Login => login;
        public ExtraWindowView Extra => extra;
        public BoosterPickerView Picker => picker;
        public BoosterOfferView Offer => offer;
    }
}
