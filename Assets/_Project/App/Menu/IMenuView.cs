using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Presentation;

namespace Kturker.UnityBase.App.Menu
{
    public interface IMenuView
    {
        // The index of the game whose Play button was pressed
        event Action<int> GameClicked;
        event Action SettingsClicked;
        event Action AboutClicked;
        void SetTitle(string text);
        void SetVersion(string text);
        void ShowGames(IReadOnlyList<string> labelKeys, UiBinder binder);

        // The game's Play button reads "Level n" (the shell's level flow has progress)
        void ShowLevel(int index, int level);
    }
}
