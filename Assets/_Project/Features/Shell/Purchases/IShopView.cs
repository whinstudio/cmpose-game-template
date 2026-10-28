using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.Features.Shell.Purchases
{
    public interface IShopView
    {
        event Action CloseClicked;
        event Action RestoreClicked;
        event Action RetryClicked;

        // A row's price button, with its product id
        event Action<string> BuyClicked;

        // The rows' slots: the list of packs (MaxRows) and the grid of coin packs (MaxPacks)
        int MaxRows { get; }
        int MaxPacks { get; }

        void SetOpen(bool open);

        // The wallet at the top, and the coin packs' icon (null count hides the counter: no economy)
        void ShowWallet(int? coins, CurrencyMark mark);

        void ShowLoading();

        // The list rows, then the coin packs in a grid of packColumns columns; hidden slots leave no gap
        void ShowRows(IReadOnlyList<ShopRow> rows, IReadOnlyList<ShopRow> packs, int packColumns);

        // The rows give way to a short error and Try again
        void ShowFailed();

        // A line under the rows (null hides it)
        void ShowMessage(string key);
        void SetBusy(bool busy);
    }
}
