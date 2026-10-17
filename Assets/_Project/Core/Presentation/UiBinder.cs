using Kturker.UnityBase.Core.Domain;
using Kturker.UnityBase.Core.Localization;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation
{
    // Binds the localized texts, themed graphics, themed texts, themed cameras and kit elements of every loaded scene
    // (SceneUiBinding) or of one tree (the root prefab, built rows) to the app's localizer and theme. Binding twice is
    // harmless.
    public sealed class UiBinder
    {
        private readonly ILocalizer localizer;
        private readonly IThemeSource theme;

        public UiBinder(ILocalizer localizer, IThemeSource theme)
        {
            this.localizer = localizer;
            this.theme = theme;
        }

        public void BindScene()
        {
            foreach (var text in Object.FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None)) text.Bind(localizer);
            foreach (var graphic in Object.FindObjectsByType<ThemedGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None)) graphic.Bind(theme);
            foreach (var text in Object.FindObjectsByType<ThemedText>(FindObjectsInactive.Include, FindObjectsSortMode.None)) text.Bind(theme);
            foreach (var camera in Object.FindObjectsByType<ThemedCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) camera.Bind(theme);
            foreach (var element in Object.FindObjectsByType<KitElement>(FindObjectsInactive.Include, FindObjectsSortMode.None)) element.Bind(theme);
        }

        public void BindTree(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<LocalizedText>(true)) text.Bind(localizer);
            foreach (var graphic in root.GetComponentsInChildren<ThemedGraphic>(true)) graphic.Bind(theme);
            foreach (var text in root.GetComponentsInChildren<ThemedText>(true)) text.Bind(theme);
            foreach (var camera in root.GetComponentsInChildren<ThemedCamera>(true)) camera.Bind(theme);
            foreach (var element in root.GetComponentsInChildren<KitElement>(true)) element.Bind(theme);
        }
    }
}
