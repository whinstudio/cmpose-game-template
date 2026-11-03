using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // How views open and close their panels: a KitSheet rises or pops in, a ScreenTransition slides and fades in, any
    // other object just turns on. Closing is immediate (callers and presenters read activeSelf straight after).
    public static class KitScreens
    {
        public static void Show(GameObject panel)
        {
            var sheet = panel.GetComponent<KitSheet>();
            if (sheet != null)
            {
                sheet.SetOpen(true, true);
                return;
            }
            var transition = panel.GetComponent<ScreenTransition>();
            if (transition != null)
            {
                transition.Play(true, true);
                return;
            }
            panel.SetActive(true);
        }

        // A destroyed panel (its scene is unloading while the scope disposes a view that closes it) is left alone
        public static void Hide(GameObject panel)
        {
            if (panel == null) return;
            var sheet = panel.GetComponent<KitSheet>();
            if (sheet != null)
            {
                sheet.SetOpen(false, false);
                return;
            }
            var transition = panel.GetComponent<ScreenTransition>();
            if (transition != null)
            {
                transition.Play(false, false);
                return;
            }
            panel.SetActive(false);
        }

        public static void Set(GameObject panel, bool shown)
        {
            if (shown) Show(panel);
            else Hide(panel);
        }
    }
}
