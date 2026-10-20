using System;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // The menu's two side columns of round feature buttons (gift, wheel, leaderboard): features and game code add
    // theirs here instead of placing their own buttons on the menu
    public interface IMenuSideButtons
    {
        // Up to three per side, stacked top down in the order added; a round button in the game's skin with the
        // white glyph icon and the labelKey caption under it. A further one on a full side throws.
        IMenuSideButton AddSideButton(MenuSide side, Sprite icon, string labelKey, Action onClick);
    }
}
