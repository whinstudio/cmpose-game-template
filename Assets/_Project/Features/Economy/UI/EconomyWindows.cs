using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using VContainer.Unity;

namespace Kturker.UnityBase.Features.Economy
{
    // What the menu's side buttons open
    public interface IEconomyWindows
    {
        bool OpenSupply();
        bool OpenWheel();
        bool OpenLogin();

        // A custom extra's content in the economy's window frame; false when the economy is off, during an AutoPlay run,
        // while another window is open or when content.ExtraId is not a custom extra of GameEconomy.json
        bool OpenExtra(IEconomyExtraContent content);
    }

    // The economy's windows for the root scope: makes the overlay from Resources the first time one is needed and
    // keeps it across scenes (canvas sorting 700: above Settings, below the shop), and ticks the open window once a
    // second (unscaled). Nothing is made with the economy off or during an AutoPlay run, and an extra the game does not
    // have (no supply, wheel or login bonus in its file, an id that is not one of its custom extras) never opens.
    public sealed class EconomyWindows : IEconomyWindows, IBoosterOffer, IBoosterPicker, ITickable, IDisposable
    {
        public const string ResourcePath = "Economy/EconomyWindows";

        private readonly Economy economy;
        private readonly Supply supply;
        private readonly Wheel wheel;
        private readonly LoginBonus login;
        private readonly IEconomyAds ads;
        private readonly ILocalizer localizer;
        private readonly EconomyCurrency currency;
        private readonly IUiFeedback feedback;
        private readonly UiBinder binder;
        private readonly ILog log;
        private readonly IEconomyShop shop;
        private readonly Func<bool> autoPlay;
        private EconomyWindowsView view;
        private SupplyWindow supplyWindow;
        private WheelWindow wheelWindow;
        private LoginBonusWindow loginWindow;
        private ExtraWindow extraWindow;
        private BoosterOfferDialog offer;
        private BoosterPicker picker;
        private float sinceTick;

        public EconomyWindows(Economy economy, Supply supply, Wheel wheel, LoginBonus login, IEconomyAds ads, ILocalizer localizer,
            IUiFeedback feedback, UiBinder binder, ILog log, IEconomyShop shop, Func<bool> autoPlay)
        {
            this.economy = economy;
            this.supply = supply;
            this.wheel = wheel;
            this.login = login;
            this.ads = ads;
            this.localizer = localizer;
            currency = new EconomyCurrency(economy.Config, localizer);
            this.feedback = feedback;
            this.binder = binder;
            this.log = log;
            this.shop = shop;
            this.autoPlay = autoPlay;
        }

        public bool OpenSupply() => supply.Configured && Ready() && supplyWindow.Open();

        public bool OpenWheel() => wheel.Configured && Ready() && wheelWindow.Open();

        public bool OpenLogin() => login.Configured && Ready() && loginWindow.Open();

        public bool OpenExtra(IEconomyExtraContent content) => content != null && Ready() && extraWindow.Open(content);

        public UniTask<OfferResult> OfferAsync(BoosterConfig booster, bool adAllowed, CancellationToken ct) =>
            Ready() ? offer.OfferAsync(booster, adAllowed, ct) : UniTask.FromResult(OfferResult.Closed);

        public UniTask<IReadOnlyList<string>> ChooseAsync(IReadOnlyList<BoosterConfig> boosters, ISet<string> adUsed, CancellationToken ct) =>
            Ready() ? picker.ChooseAsync(boosters, adUsed, ct) : UniTask.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public void Tick()
        {
            if (view == null) return;
            sinceTick += Time.unscaledDeltaTime;
            if (sinceTick < 1f) return;
            sinceTick = 0f;
            supplyWindow.Tick();
            wheelWindow.Tick();
            loginWindow.Tick();
            extraWindow.Tick();
        }

        public void Dispose()
        {
            supplyWindow?.Dispose();
            wheelWindow?.Dispose();
            loginWindow?.Dispose();
            extraWindow?.Dispose();
            picker?.Dispose();
            offer?.Dispose();
            if (view != null) UnityEngine.Object.Destroy(view.gameObject);
        }

        private bool Ready()
        {
            if (!economy.Enabled || autoPlay()) return false;
            return view != null || Create();
        }

        private bool Create()
        {
            var prefab = Resources.Load<EconomyWindowsView>(ResourcePath);
            if (prefab == null)
            {
                log.Error($"Economy windows missing: Resources/{ResourcePath}");
                return false;
            }
            view = UnityEngine.Object.Instantiate(prefab);
            UnityEngine.Object.DontDestroyOnLoad(view.gameObject);
            binder.BindTree(view.gameObject);
            supplyWindow = new SupplyWindow(view.Supply, economy, supply, ads, currency, feedback, autoPlay);
            wheelWindow = new WheelWindow(view.Wheel, economy, wheel, ads, localizer, feedback, autoPlay, new System.Random());
            loginWindow = new LoginBonusWindow(view.Login, economy, login, ads, localizer, feedback, autoPlay);
            extraWindow = new ExtraWindow(view.Extra, economy, ads, feedback, autoPlay, binder.BindTree);
            offer = new BoosterOfferDialog(view.Offer, economy, ads, currency, feedback, shop, autoPlay);
            picker = new BoosterPicker(view.Picker, economy, offer, currency, feedback);
            return true;
        }
    }
}
