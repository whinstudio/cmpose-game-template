using UnityEngine;

namespace Kturker.UnityBase.Core.About
{
    // The game's credits: Resources/Credits.txt (Compass writes Assets/_Project/Resources/Credits.txt, the attributions
    // its CC BY assets ask for), less the blank lines around it. The template ships without one; without the file, or
    // with a blank one, there is nothing to show and Settings has no Credits row.
    public sealed class GameCredits
    {
        public const string ResourcePath = "Credits";
        public static readonly GameCredits None = new GameCredits(null);

        public GameCredits(string text) => Text = (text ?? string.Empty).Trim();

        public string Text { get; }
        public bool HasText => Text.Length > 0;

        public static GameCredits Read(TextAsset file) => new GameCredits(file != null ? file.text : null);

        public static GameCredits FromResources() => Read(Resources.Load<TextAsset>(ResourcePath));
    }
}
