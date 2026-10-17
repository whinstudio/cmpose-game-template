namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // The level flow's progress. Available is false without a save store (NoLevelProgress); the saveSystem flag
    // registers LevelProgress (slot 'levels').
    public interface ILevelProgress
    {
        bool Available { get; }

        // True once a game has begun the level flow (the menu then shows "Level N" on Play)
        bool Started { get; }

        // The first level not yet completed (1 first)
        int Current { get; }

        int StarsOf(int level);

        void Start();

        void Complete(int level, int stars);
    }
}
