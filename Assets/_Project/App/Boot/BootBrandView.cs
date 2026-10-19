using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.App.Boot
{
    // The boot screen of a game Compass branded: its logo in the middle, over the brand colour when the skin has one
    // (else the theme's background). Without a logo the boot screen stays as the theme draws it.
    public sealed class BootBrandView : MonoBehaviour
    {
        [SerializeField] private GameObject title;
        [SerializeField] private Image logo;
        [SerializeField] private Image brandFill;

        private void Awake() => ShowBranding(GameBranding.TitleLogo, GameSkin.Current?.BrandBackground);

        public void ShowBranding(Sprite logoSprite, Color? background)
        {
            var hasLogo = logoSprite != null;
            logo.sprite = logoSprite;
            logo.preserveAspect = true;
            logo.gameObject.SetActive(hasLogo);
            title.SetActive(!hasLogo);
            var fill = hasLogo ? background : null;
            if (fill.HasValue)
            {
                // Fixed, so the theme's binding does not repaint it
                brandFill.color = fill.Value;
                brandFill.GetComponent<ThemedGraphic>().SetFixed(fill);
            }
            brandFill.gameObject.SetActive(fill.HasValue);
        }
    }
}
