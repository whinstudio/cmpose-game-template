using System;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using VContainer.Unity;

namespace Kturker.UnityBase.Features.Economy
{
    // The menu's buttons for the game's custom extras (EconomyMenu, menu scope)
    public interface IEconomyExtraButtons
    {
        // A round button captioned economy.extra.<id>.name that plays the click feedback and calls onClick (open the
        // extra with IEconomyWindows.OpenExtra); set its badge with the returned button. It goes after the ready
        // extras' buttons: in the left column while that has room, else in the right one. Null with the economy off or
        // when the game's file has no custom extra with this id.
        IMenuSideButton AddExtraButton(string extraId, Sprite icon, Action onClick);
    }

    // The economy on the menu: the currency at the top bar's left (its mark, in its own colour), and a side button for
    // each ready extra the game has, in the file's extras order: the supply and the login bonus in the left column, the
    // wheel in the right one (whose other two slots are the leaderboard's and the achievements'). Custom extras add
    // theirs with IEconomyExtraButtons. A game with no extra shows only the counter. The badges follow the timers
    // (refreshed every second, unscaled): a red dot when something can be taken, the video mark when only an ad can,
    // the countdown while waiting. Nothing shows with the economy off.
    public sealed class EconomyMenu : IStartable, ITickable, IDisposable, IEconomyExtraButtons
    {
        private readonly IMenuSideButtons sides;
        private readonly IMenuCoins coins;
        private readonly Economy economy;
        private readonly Supply supply;
        private readonly Wheel wheel;
        private readonly LoginBonus login;
        private readonly IEconomyWindows windows;
        private readonly EconomyCurrency currency;
        private readonly IUiFeedback feedback;
        private IMenuSideButton supplyButton;
        private IMenuSideButton wheelButton;
        private IMenuSideButton loginButton;
        private int leftButtons;
        private bool placed;
        private bool started;
        private float sinceRefresh;

        public EconomyMenu(IMenuSideButtons sides, IMenuCoins coins, Economy economy, Supply supply, Wheel wheel, LoginBonus login,
            IEconomyWindows windows, EconomyCurrency currency, IUiFeedback feedback)
        {
            this.sides = sides;
            this.coins = coins;
            this.economy = economy;
            this.supply = supply;
            this.wheel = wheel;
            this.login = login;
            this.windows = windows;
            this.currency = currency;
            this.feedback = feedback;
        }

        public void Start()
        {
            if (!economy.Enabled)
            {
                coins.SetCoins(null, null);
                return;
            }
            PlaceReady();
            started = true;
            economy.Changed += Refresh;
            Refresh();
        }

        public IMenuSideButton AddExtraButton(string extraId, Sprite icon, Action onClick)
        {
            var extra = economy.Enabled ? economy.Config.Extra(extraId) : null;
            if (extra == null || extra.Kind != ExtraKind.Custom) return null;
            PlaceReady();
            var side = leftButtons < MenuSlots ? MenuSide.Left : MenuSide.Right;
            if (side == MenuSide.Left) leftButtons++;
            return sides.AddSideButton(side, icon, extra.NameKey, () =>
            {
                feedback.Click();
                onClick?.Invoke();
            });
        }

        public void Tick()
        {
            if (!started) return;
            sinceRefresh += Time.unscaledDeltaTime;
            if (sinceRefresh < 1f) return;
            sinceRefresh = 0f;
            Refresh();
        }

        public void Dispose()
        {
            if (started) economy.Changed -= Refresh;
            started = false;
        }

        public void Refresh()
        {
            if (!started) return;
            coins.SetCoins(economy.Coins, currency.Mark);
            if (supplyButton != null)
            {
                if (supply.FreeReady) supplyButton.SetBadge(MenuBadge.Dot);
                else if (supply.AdRowReady) supplyButton.SetBadge(MenuBadge.Ad);
                else supplyButton.SetBadge(MenuBadge.Timer, EconomyText.Timer(supply.FreeReadyIn));
            }
            if (wheelButton != null)
            {
                if (wheel.FreeSpinReady) wheelButton.SetBadge(MenuBadge.Dot);
                else if (wheel.AdSpinReady) wheelButton.SetBadge(MenuBadge.Ad);
                else wheelButton.SetBadge(MenuBadge.None);
            }
            if (loginButton != null)
            {
                if (login.ReadyToday) loginButton.SetBadge(MenuBadge.Dot);
                else loginButton.SetBadge(MenuBadge.Timer, EconomyText.Timer(login.NextDayIn));
            }
        }

        // The side columns' slots (MenuView.SideSlots)
        private const int MenuSlots = 3;

        // The ready extras' buttons once, in the file's order
        private void PlaceReady()
        {
            if (placed || !economy.Enabled) return;
            placed = true;
            foreach (var extra in economy.Config.Extras)
            {
                switch (extra.Kind)
                {
                    case ExtraKind.Supply when supply.Configured:
                        supplyButton = Left(KitSprites.Gift, EconomyKeys.Supply, windows.OpenSupply);
                        break;
                    case ExtraKind.Login when login.Configured:
                        loginButton = Left(KitSprites.Calendar, EconomyKeys.Login, windows.OpenLogin);
                        break;
                    case ExtraKind.Wheel when wheel.Configured:
                        wheelButton = sides.AddSideButton(MenuSide.Right, KitSprites.Wheel, EconomyKeys.Wheel, () => Open(windows.OpenWheel));
                        break;
                }
            }
        }

        private IMenuSideButton Left(Sprite icon, string key, Func<bool> open)
        {
            leftButtons++;
            return sides.AddSideButton(MenuSide.Left, icon, key, () => Open(open));
        }

        private void Open(Func<bool> open)
        {
            feedback.Click();
            open();
        }
    }
}
