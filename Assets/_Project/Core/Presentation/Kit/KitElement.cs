using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // A kit component: reads its shape (radius, surface, shadow) and motion from the theme's style and sets the roles
    // of its parts; repaints on IThemeSource.Changed. Bind also binds the themed graphics and texts below it, so a kit
    // tree built by game code needs only this call (UiBinder.BindTree does the same).
    public abstract class KitElement : MonoBehaviour
    {
        private IThemeSource source;

        protected IThemeSource Theme => source;
        protected MotionTimings Timings => UiMotion.Timings(UiMotion.ProfileOf(source));

        public void Bind(IThemeSource value)
        {
            if (source != value)
            {
                if (source != null) source.Changed -= Repaint;
                source = value;
                if (source != null) source.Changed += Repaint;
            }
            foreach (var graphic in GetComponentsInChildren<ThemedGraphic>(true)) graphic.Bind(value);
            foreach (var text in GetComponentsInChildren<ThemedText>(true)) text.Bind(value);
            Repaint();
            // An element already on this source was bound above it in this walk (or before): skipping it ends the walk
            foreach (var element in GetComponentsInChildren<KitElement>(true))
                if (element.source != value) element.Bind(value);
        }

        protected virtual void OnDestroy()
        {
            if (source != null) source.Changed -= Repaint;
        }

        private void Repaint()
        {
            if (!this || source == null || source.Current == null) return;
            Apply(source.Current, source.Style);
        }

        // style may be null (a theme without one): flat, square, Snappy
        protected abstract void Apply(Palette palette, ThemeStyle style);

        protected static ThemedGraphic Themed(Graphic graphic)
        {
            var themed = graphic.GetComponent<ThemedGraphic>();
            return themed != null ? themed : graphic.gameObject.AddComponent<ThemedGraphic>();
        }
    }
}
