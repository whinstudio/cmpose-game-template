using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using VContainer.Unity;

namespace Kturker.UnityBase.Features.Economy
{
    // One HUD button of an inRun booster: its icon (null: the fallback letter of the name)
    public readonly struct BoosterSlot
    {
        public BoosterSlot(string nameKey, Sprite icon)
        {
            NameKey = nameKey;
            Icon = icon;
        }

        public string NameKey { get; }
        public Sprite Icon { get; }
    }

    // The Game scene's row of inRun booster buttons in the shell's HUD (hidden with it under the end cards)
    public interface IBoosterBarView
    {
        int Slots { get; }
        event Action<int> Clicked;

        // An empty list hides the row
        void Show(IReadOnlyList<BoosterSlot> slots);

        // The stock on the button's badge; 0 shows "+" (the offer opens)
        void SetCount(int index, int count);
    }

    // The inRun boosters' HUD buttons: a press uses one (Boosters.UseInRunAsync), the badges follow the stock. Hidden
    // with the economy off, without inRun boosters and during an AutoPlay run.
    public sealed class BoosterBar : IStartable, IDisposable
    {
        private readonly IBoosterBarView view;
        private readonly IEconomy economy;
        private readonly Boosters boosters;
        private readonly IUiFeedback feedback;
        private readonly Func<bool> autoPlay;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private IReadOnlyList<BoosterConfig> shown = Array.Empty<BoosterConfig>();
        private bool busy;

        public BoosterBar(IBoosterBarView view, IEconomy economy, Boosters boosters, IUiFeedback feedback, Func<bool> autoPlay)
        {
            this.view = view;
            this.economy = economy;
            this.boosters = boosters;
            this.feedback = feedback;
            this.autoPlay = autoPlay;
        }

        public void Start()
        {
            var list = new List<BoosterConfig>();
            if (economy.Enabled && !autoPlay())
            {
                foreach (var booster in boosters.InRun)
                    if (list.Count < view.Slots) list.Add(booster);
            }
            shown = list;
            var slots = new List<BoosterSlot>();
            foreach (var booster in list) slots.Add(new BoosterSlot(booster.NameKey, EconomyIcons.Booster(booster)));
            view.Show(slots);
            if (list.Count == 0) return;
            view.Clicked += OnClicked;
            economy.Changed += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            view.Clicked -= OnClicked;
            economy.Changed -= Refresh;
            lifetime.Cancel();
            lifetime.Dispose();
        }

        private void Refresh()
        {
            for (var i = 0; i < shown.Count; i++) view.SetCount(i, economy.Count(shown[i].Id));
        }

        private void OnClicked(int index)
        {
            if (busy || index < 0 || index >= shown.Count) return;
            feedback.Click();
            UseAsync(shown[index].Id).Forget();
        }

        private async UniTaskVoid UseAsync(string id)
        {
            busy = true;
            try
            {
                await boosters.UseInRunAsync(id, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                busy = false;
            }
        }
    }
}
