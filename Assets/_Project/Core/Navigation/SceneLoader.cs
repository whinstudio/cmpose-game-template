using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kturker.UnityBase.Core.Domain;

namespace Kturker.UnityBase.Core.Navigation
{
    // One scene load at a time behind the loading overlay; a request during a load is ignored. Revealing runs while the
    // overlay still covers the loaded scene, Loaded once it is gone.
    public sealed class SceneLoader : ISceneLoader
    {
        private readonly ISceneGateway gateway;
        private readonly ILoadingOverlay overlay;
        private readonly ILog log;

        public SceneLoader(ISceneGateway gateway, ILoadingOverlay overlay, ILog log)
        {
            this.gateway = gateway;
            this.overlay = overlay;
            this.log = log;
        }

        public bool IsLoading { get; private set; }

        public event Action<SceneId> Revealing;

        public event Action<SceneId> Loaded;

        public async UniTask LoadAsync(SceneId id, CancellationToken cancellation = default)
        {
            if (IsLoading)
            {
                log.Warning($"Scene load ignored while another is running: {id}");
                return;
            }
            IsLoading = true;
            overlay.Show();
            try
            {
                await gateway.LoadAsync(id.ToString(), cancellation);
                log.Marker($"CMPOSE_SCENE {id}");
                Revealing?.Invoke(id);
            }
            finally
            {
                overlay.Hide();
                IsLoading = false;
            }
            Loaded?.Invoke(id);
        }
    }
}
