namespace Kturker.UnityBase.Features.Shell.GameShell
{
    // Without the saveSystem flag there is no level flow
    public sealed class NoLevelProgress : ILevelProgress
    {
        public bool Available => false;
        public bool Started => false;
        public int Current => 1;

        public int StarsOf(int level) => 0;

        public void Start()
        {
        }

        public void Complete(int level, int stars)
        {
        }
    }
}
