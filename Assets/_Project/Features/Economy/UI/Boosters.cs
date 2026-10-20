using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Lifecycle;
using Kturker.UnityBase.Features.Shell.GameShell;

namespace Kturker.UnityBase.Features.Economy
{
    // What game code uses for boosters (the effect of each is the game's): the pre-run choice and the in-run uses
    public interface IBoosters
    {
        // With preRun boosters, the picker opens: the chosen ones are taken from the stock (or were just paid) and
        // returned. Without any, with the economy off or during an AutoPlay run: an empty list at once.
        UniTask<IReadOnlyList<string>> ChooseForRunAsync(CancellationToken ct);

        // An inRun booster was used (its HUD button pressed and paid): apply its effect now
        event Action<string> Used;
    }

    // IBoosters over the economy's stock and windows. A booster bought with a watched ad is never stocked: it is used
    // at once, and each booster can be bought with an ad once per run (a run starts with ChooseForRunAsync and ends
    // with the shell's RunEnded). The in-run offer pauses the game while it is open.
    public sealed class Boosters : IBoosters, IDisposable
    {
        private static readonly IReadOnlyList<string> None = Array.Empty<string>();

        private readonly IEconomy economy;
        private readonly IBoosterOffer offer;
        private readonly IBoosterPicker picker;
        private readonly ShellRunEvents runs;
        private readonly PauseService pause;
        private readonly Func<bool> autoPlay;
        private readonly HashSet<string> adUsed = new HashSet<string>(StringComparer.Ordinal);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

        public Boosters(IEconomy economy, IBoosterOffer offer, IBoosterPicker picker, ShellRunEvents runs, PauseService pause,
            Func<bool> autoPlay)
        {
            this.economy = economy;
            this.offer = offer;
            this.picker = picker;
            this.runs = runs;
            this.pause = pause;
            this.autoPlay = autoPlay;
            if (runs != null) runs.RunEnded += OnRunEnded;
        }

        public event Action<string> Used;

        private bool Open => economy.Enabled && !autoPlay();

        // The inRun boosters the HUD shows (the config's order)
        public IReadOnlyList<BoosterConfig> InRun => Offered(BoosterOffer.InRun);

        public async UniTask<IReadOnlyList<string>> ChooseForRunAsync(CancellationToken ct)
        {
            adUsed.Clear();
            if (!Open) return None;
            var boosters = Offered(BoosterOffer.PreRun);
            if (boosters.Count == 0) return None;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
            return await picker.ChooseAsync(boosters, adUsed, linked.Token);
        }

        // An inRun booster's HUD button: one from the stock, else its offer (coins: paid and used; a watched ad: used).
        // False when nothing was used.
        public async UniTask<bool> UseInRunAsync(string id, CancellationToken ct)
        {
            if (!Open) return false;
            var booster = economy.Config.Booster(id);
            if (booster == null || booster.Offer != BoosterOffer.InRun) return false;
            if (economy.TryUse(id))
            {
                Used?.Invoke(id);
                return true;
            }
            OfferResult result;
            pause.Pause(PauseReason.Window);
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
                result = await offer.OfferAsync(booster, !adUsed.Contains(id), linked.Token);
            }
            finally
            {
                pause.Resume(PauseReason.Window);
            }
            if (result == OfferResult.Closed) return false;
            if (result == OfferResult.Watched) adUsed.Add(id);
            Used?.Invoke(id);
            return true;
        }

        public void Dispose()
        {
            if (runs != null) runs.RunEnded -= OnRunEnded;
            lifetime.Cancel();
            lifetime.Dispose();
        }

        private IReadOnlyList<BoosterConfig> Offered(BoosterOffer kind)
        {
            var list = new List<BoosterConfig>();
            if (!economy.Enabled) return list;
            foreach (var booster in economy.Config.Boosters)
                if (booster.Offer == kind) list.Add(booster);
            return list;
        }

        private void OnRunEnded(bool won) => adUsed.Clear();
    }
}
