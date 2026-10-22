using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // The shell's HUD in the safe area (SceneBuilder builds it under the pause button): the counter pill top left (a
    // plain Accent dot as its icon, the game skin's coin when it has one, or the economy's currency), "Level n" top
    // centre, the score in the display font under them, "Best: n" under the score, the toast in the middle. Its content
    // stays hidden until Show; touches go through to the game.
    public sealed class ShellHudView : MonoBehaviour, IShellHudView
    {
        // A HUD count is quick: points arrive one after another
        private const float CountSeconds = 0.3f;

        [SerializeField] private GameObject content;
        [SerializeField] private KitFlowRow items;
        [SerializeField] private CountUpText score;
        [SerializeField] private LocalizedText best;
        [SerializeField] private GameObject levelPill;
        [SerializeField] private LocalizedText level;
        [SerializeField] private GameObject counterPill;
        [SerializeField] private CountUpText counter;
        [SerializeField] private FallbackIcon counterIcon;
        [SerializeField] private ShellToast toast;
        private bool counterShown;
        // The economy gave the counter its currency (else Show puts the plain icon there: the skin's coin or the dot)
        private bool counterIconSet;
        private bool shown;

        private void Awake()
        {
            content.SetActive(false);
            toast.gameObject.SetActive(false);
        }

        // The row at the top left the counter pill starts: a game adds its own HUD items here (hearts, keys), and the row
        // keeps them KitLayout.Gap apart, shrinking and then wrapping them before they reach "Level n"
        public KitFlowRow Items => items;

        public void Show(int? levelNumber)
        {
            shown = true;
            content.SetActive(true);
            score.SetValue(0);
            best.gameObject.SetActive(false);
            counterPill.SetActive(false);
            counterShown = false;
            if (!counterIconSet) counterIcon.ShowPlain();
            levelPill.SetActive(levelNumber.HasValue);
            if (levelNumber.HasValue) level.SetKey(ShellKeys.Level, levelNumber.Value);
        }

        public void SetVisible(bool visible) => content.SetActive(visible && shown);

        public void CountScore(int value) => score.CountTo(value, CountSeconds);

        public void ShowBest(int value)
        {
            best.gameObject.SetActive(true);
            best.SetKey(ShellKeys.Best, value);
        }

        public void ShowCounter(int value)
        {
            counterPill.SetActive(true);
            if (counterShown)
            {
                counter.CountTo(value, CountSeconds);
                return;
            }
            counterShown = true;
            counter.SetValue(value);
        }

        public void ShowCounterIcon(CurrencyMark mark)
        {
            counterIconSet = true;
            counterIcon.ShowCurrency(mark);
        }

        public void Toast(string key) => toast.Play(key);
    }
}
