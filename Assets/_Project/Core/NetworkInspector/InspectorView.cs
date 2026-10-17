using System;
using System.Collections.Generic;
using Kturker.UnityBase.Core.Localization;
using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.NetworkInspector
{
    // SceneBuilder builds the prefab (Resources/NetworkInspector/InspectorOverlay): the NET button, and a panel with
    // Rows row buttons, a scrolling detail text, Clear and Close
    public sealed class InspectorView : MonoBehaviour, IInspectorView
    {
        public const int Rows = 8;

        [SerializeField] private Button openButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button[] rows;
        [SerializeField] private LocalizedText[] rowLabels;
        [SerializeField] private LocalizedText detail;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button closeButton;

        public event Action OpenClicked;
        public event Action CloseClicked;
        public event Action ClearClicked;
        public event Action<int> RowClicked;

        public int RowCount => rows.Length;

        private void Awake()
        {
            KitScreens.Hide(panel);
            openButton.onClick.AddListener(OnOpen);
            closeButton.onClick.AddListener(OnClose);
            clearButton.onClick.AddListener(OnClear);
            for (var i = 0; i < rows.Length; i++)
            {
                var index = i;
                rows[i].onClick.AddListener(() => RowClicked?.Invoke(index));
            }
        }

        private void OnDestroy()
        {
            openButton.onClick.RemoveListener(OnOpen);
            closeButton.onClick.RemoveListener(OnClose);
            clearButton.onClick.RemoveListener(OnClear);
            foreach (var row in rows) row.onClick.RemoveAllListeners();
        }

        public void SetOpen(bool open) => KitScreens.Set(panel, open);

        public void ShowRows(IReadOnlyList<string> summaries)
        {
            for (var i = 0; i < rows.Length; i++)
            {
                var visible = i < summaries.Count;
                rows[i].gameObject.SetActive(visible);
                if (visible) rowLabels[i].SetLiteral(summaries[i]);
            }
        }

        public void ShowDetail(string text) => detail.SetLiteral(text);

        public void ShowEmpty() => detail.SetKey(InspectorKeys.Empty);

        private void OnOpen() => OpenClicked?.Invoke();

        private void OnClose() => CloseClicked?.Invoke();

        private void OnClear() => ClearClicked?.Invoke();
    }
}
