using System;
using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using VContainer.Unity;

namespace Kturker.UnityBase.Core.NetworkInspector
{
    // Root entry point: in development builds (and the editor) puts the overlay above every scene; release builds
    // and Compass's capture runs never create it. Logs CMPOSE_NETINSPECTOR shown=<true|false> either way (the gate
    // checks the release APK).
    public sealed class InspectorOverlayHost : IStartable, IDisposable
    {
        public const string ResourcePath = "NetworkInspector/InspectorOverlay";

        private readonly InspectorObserver observer;
        private readonly UiBinder binder;
        private readonly ILog log;
        private readonly bool show;
        private InspectorView view;
        private InspectorPresenter presenter;

        public InspectorOverlayHost(InspectorObserver observer, UiBinder binder, ILog log, bool show)
        {
            this.observer = observer;
            this.binder = binder;
            this.log = log;
            this.show = show;
        }

        public void Start()
        {
            log.Marker($"CMPOSE_NETINSPECTOR shown={(show ? "true" : "false")}");
            if (!show) return;
            var prefab = Resources.Load<InspectorView>(ResourcePath);
            if (prefab == null)
            {
                log.Error($"Network inspector missing: Resources/{ResourcePath}");
                return;
            }
            view = UnityEngine.Object.Instantiate(prefab);
            UnityEngine.Object.DontDestroyOnLoad(view.gameObject);
            binder.BindTree(view.gameObject);
            presenter = new InspectorPresenter(view, observer);
            presenter.Start();
        }

        public void Dispose()
        {
            if (presenter != null) presenter.Dispose();
            if (view != null) UnityEngine.Object.Destroy(view.gameObject);
        }
    }
}
