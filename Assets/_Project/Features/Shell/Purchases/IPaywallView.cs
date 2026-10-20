using System;
using System.Collections.Generic;

namespace Kturker.UnityBase.Features.Shell.Purchases
{
    public interface IPaywallView
    {
        event Action CloseClicked;
        event Action BuyClicked;
        event Action RestoreClicked;
        event Action PrivacyClicked;
        event Action TermsClicked;
        event Action<int> PackageClicked;
        int MaxPackages { get; }
        void SetOpen(bool open);
        void SetLegal(bool privacy, bool terms);
        void ShowLoading();
        void ShowPackages(IReadOnlyList<PaywallRow> rows, int selected);
        void Select(int index);
        void ShowUnavailable();
        void ShowMessage(string key);

        // The welcome bundle's line under the packages (null hides it)
        void ShowBonus(string text);
        void SetBusy(bool busy);
    }
}
