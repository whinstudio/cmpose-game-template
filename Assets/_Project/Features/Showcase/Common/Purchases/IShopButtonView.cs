using System;

namespace Kturker.UnityBase.Features.Showcase.Common.Purchases
{
    public interface IShopButtonView
    {
        event Action Clicked;
        void SetVisible(bool visible);
    }
}
