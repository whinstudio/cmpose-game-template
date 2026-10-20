using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;

namespace Kturker.UnityBase.Features.Economy
{
    public enum LoginDayState
    {
        Upcoming,
        Today,
        Claimed
    }

    // One tile of the 7-day bonus: its prize ("15 acorns"), icon (null: the fallback letter of letterOf) and state; a
    // prize with an amount draws the currency's mark instead (currency; null: a booster's, in the tile's colours)
    public readonly struct LoginDay
    {
        public LoginDay(int number, string prize, Sprite icon, string letterOf, LoginDayState state, CurrencyMark currency = null)
        {
            Number = number;
            Prize = prize;
            Icon = icon;
            LetterOf = letterOf;
            State = state;
            Currency = currency;
        }

        public int Number { get; }
        public string Prize { get; }
        public Sprite Icon { get; }
        public string LetterOf { get; }
        public LoginDayState State { get; }
        public CurrencyMark Currency { get; }
    }

    public interface ILoginBonusView
    {
        event Action ClaimClicked;
        event Action CloseClicked;
        void SetOpen(bool open);
        void ShowDays(IReadOnlyList<LoginDay> days);

        // Claim while today's prize is ready, else "Next reward in <timer>"
        void ShowClaim(bool ready, string next);
    }

    // The 7-day login bonus window (E18): the streak's claimed days, today's tile and the days to come; day 7 is the
    // big one. A missed day starts the streak again at day 1, so the tiles before it read as to come.
    public sealed class LoginBonusWindow : EconomyWindow
    {
        private readonly ILoginBonusView view;
        private readonly LoginBonus login;
        private readonly ILocalizer localizer;
        private readonly EconomyCurrency currency;

        public LoginBonusWindow(ILoginBonusView view, Economy economy, LoginBonus login, IEconomyAds ads, ILocalizer localizer,
            IUiFeedback feedback, Func<bool> autoPlay) : base(economy, ads, feedback, autoPlay)
        {
            this.view = view;
            this.login = login;
            this.localizer = localizer;
            currency = new EconomyCurrency(economy.Config, localizer);
            view.ClaimClicked += OnClaim;
            view.CloseClicked += OnCloseClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            view.ClaimClicked -= OnClaim;
            view.CloseClicked -= OnCloseClicked;
        }

        protected override bool Available => login.Configured;

        protected override void SetViewOpen(bool open) => view.SetOpen(open);

        protected override void Refresh()
        {
            var today = login.Day;
            var ready = login.ReadyToday;
            var days = new List<LoginDay>();
            var rewards = login.Rewards;
            var mark = currency.Mark;
            for (var i = 0; i < rewards.Count; i++)
            {
                var number = i + 1;
                var state = number < today ? LoginDayState.Claimed
                    : number > today ? LoginDayState.Upcoming
                    : ready ? LoginDayState.Today : LoginDayState.Claimed;
                var reward = rewards[i];
                // An amount draws the currency; a prize of a booster alone draws the booster
                var booster = reward.Coins == 0 && reward.Booster != null ? Economy.Config.Booster(reward.Booster) : null;
                var icon = booster != null ? EconomyIcons.Booster(booster) : null;
                var letterOf = booster != null ? localizer.Get(booster.NameKey) : null;
                days.Add(new LoginDay(number, EconomyText.Reward(localizer, Economy.Config, reward), icon, letterOf, state,
                    booster != null ? null : mark));
            }
            view.ShowDays(days);
            view.ShowClaim(ready, ready ? null : localizer.Get(EconomyKeys.LoginNext, EconomyText.Timer(login.NextDayIn)));
        }

        private void OnClaim()
        {
            if (!IsOpen) return;
            if (login.Claim() != null) Feedback.Success();
            else Feedback.Click();
            Refresh();
        }
    }
}
