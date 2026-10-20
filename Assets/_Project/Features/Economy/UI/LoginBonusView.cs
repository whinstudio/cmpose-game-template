using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Features.Economy
{
    // SceneBuilder builds it into the economy windows: a centred modal with seven day tiles (six in two rows of three,
    // day 7 wide under them: "Day n", the icon, the prize), Claim or "Next reward in 5:12:00", and Close. Today's tile
    // is Accent; a claimed one fades and reads "Claimed". The currency's icon keeps its own colour and rim on every
    // tile (a booster's icon follows the tile).
    public sealed class LoginBonusView : MonoBehaviour, ILoginBonusView
    {
        public const int Days = 7;
        public const float ClaimedOpacity = 0.55f;

        [SerializeField] private GameObject panel;
        [SerializeField] private Image[] tiles;
        [SerializeField] private LocalizedText[] dayLabels;
        [SerializeField] private FallbackIcon[] icons;
        [SerializeField] private LocalizedText[] prizes;
        [SerializeField] private Button claimButton;
        [SerializeField] private LocalizedText next;
        [SerializeField] private Button closeButton;

        public event Action ClaimClicked;
        public event Action CloseClicked;

        public Image Tile(int index) => tiles[index];

        private void Awake()
        {
            KitScreens.Hide(panel);
            claimButton.onClick.AddListener(OnClaim);
            closeButton.onClick.AddListener(OnClose);
        }

        private void OnDestroy()
        {
            claimButton.onClick.RemoveListener(OnClaim);
            closeButton.onClick.RemoveListener(OnClose);
        }

        public void SetOpen(bool open) => KitScreens.Set(panel, open);

        public void ShowDays(IReadOnlyList<LoginDay> days)
        {
            for (var i = 0; i < tiles.Length; i++)
            {
                var visible = i < days.Count;
                tiles[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var day = days[i];
                var today = day.State == LoginDayState.Today;
                var claimed = day.State == LoginDayState.Claimed;
                var tile = tiles[i].GetComponent<ThemedGraphic>();
                tile.SetRole(today ? ThemeRole.Accent : ThemeRole.Track);
                tile.SetTone(0f, claimed ? ClaimedOpacity : 1f);
                if (claimed) dayLabels[i].SetKey(EconomyKeys.LoginClaimed);
                else dayLabels[i].SetKey(EconomyKeys.LoginDay, day.Number);
                Role(dayLabels[i], claimed ? ThemeRole.Success : today ? ThemeRole.OnAccent : ThemeRole.TextMuted);
                prizes[i].SetLiteral(day.Prize);
                Role(prizes[i], today ? ThemeRole.OnAccent : ThemeRole.Text);
                icons[i].Glyph.GetComponent<ThemedGraphic>().SetRole(today ? ThemeRole.OnAccent : ThemeRole.Accent);
                if (day.Currency != null) icons[i].ShowCurrency(day.Currency);
                else
                {
                    icons[i].ShowName(day.Icon, day.LetterOf);
                    KitGlyph.Paint(icons[i].Glyph, null);
                }
            }
        }

        public void ShowClaim(bool ready, string nextText)
        {
            claimButton.gameObject.SetActive(ready);
            next.gameObject.SetActive(!ready);
            if (!ready) next.SetLiteral(nextText ?? string.Empty);
        }

        private static void Role(Component text, ThemeRole role) => text.GetComponent<ThemedGraphic>().SetRole(role);

        private void OnClaim() => ClaimClicked?.Invoke();

        private void OnClose() => CloseClicked?.Invoke();
    }
}
