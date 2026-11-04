using System;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // The menu's one row of round feature buttons right above Play (gift, wheel, leaderboard), centred: features and
    // game code add theirs here instead of placing their own buttons on the menu
    public interface IMenuSideButtons
    {
        // Up to three per side; the Left ones come first in the row, then the Right ones, each side in the order added;
        // a round button in the game's skin with the white glyph icon and the labelKey caption under it. A hidden button
        // (SetActive(false)) leaves no gap. A further one on a full side throws.
        IMenuSideButton AddSideButton(MenuSide side, Sprite icon, string labelKey, Action onClick);
    }
}
