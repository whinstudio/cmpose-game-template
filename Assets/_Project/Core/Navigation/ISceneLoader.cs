using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;

namespace Kturker.UnityBase.Core.Navigation
{
    public interface ISceneLoader
    {
        bool IsLoading { get; }

        // Raised after a successful load while the loading overlay still covers the screen (IsLoading is still true):
        // set up what the scene's first visible frame needs (the banner's room), so nothing moves once it shows
        event Action<SceneId> Revealing;

        // Raised after a successful load, once IsLoading is false again, so a handler may start the next load
        event Action<SceneId> Loaded;

        UniTask LoadAsync(SceneId id, CancellationToken cancellation = default);
    }
}
