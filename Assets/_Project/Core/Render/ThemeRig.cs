using Kturker.UnityBase.Core.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kturker.UnityBase.Core.Render
{
    // GameRig's link to the theme (UiBinder binds it, a KitElement): the global Volume gets the style's post profile
    // (none: the volume is off), so the family the generator applies to Style.asset reaches the rig without touching
    // the prefab. 3D rig: tints the key (warm, soft shadows), fill (towards Primary) and rim (towards Accent) lights and
    // sets a three-colour ambient in the palette Background's hue at fixed levels, so dark palettes never make a murky world
    // and light ones never wash the shading out.
    // The 2D rig has no lights here (its Global Light 2D stays white).
    public sealed class ThemeRig : KitElement
    {
        // How much of the palette the lights take
        public const float FillTint = 0.25f;
        public const float RimTint = 0.35f;
        // The ambient (linear): the Background's hue, half washed to white, at these levels from above, the sides and
        // below. With the rig's key (0.9) a white top face lands just under 1, so shading and soft shadows stay visible
        // instead of clipping to one flat colour.
        private const float SkyLevel = 0.36f;
        private const float EquatorLevel = 0.26f;
        private const float GroundLevel = 0.14f;
        private static readonly Color Warm = new Color(1f, 0.96f, 0.9f);

        [SerializeField] private Volume volume;
        [SerializeField] private Light key;
        [SerializeField] private Light fill;
        [SerializeField] private Light rim;

        public Volume Volume => volume;
        public Light Key => key;
        public Light Fill => fill;
        public Light Rim => rim;

        protected override void Apply(Palette palette, ThemeStyle style)
        {
            if (volume != null)
            {
                var post = style != null ? style.Post : null;
                volume.sharedProfile = post;
                volume.enabled = post != null;
            }
            if (key == null) return;
            var primary = palette.Get(ThemeRole.Primary);
            var accent = palette.Get(ThemeRole.Accent);
            key.color = Warm;
            if (fill != null) fill.color = Color.Lerp(Color.white, primary, FillTint);
            if (rim != null) rim.color = Color.Lerp(Color.white, accent, RimTint);
            var tint = Color.Lerp(Color.white, palette.Get(ThemeRole.Background), 0.5f).linear;
            var sky = tint * SkyLevel;
            var equator = tint * EquatorLevel;
            var ground = tint * GroundLevel;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky.gamma;
            RenderSettings.ambientEquatorColor = equator.gamma;
            RenderSettings.ambientGroundColor = ground.gamma;
            // URP lights from the ambient probe, which a run-time colour change does not rebuild by itself
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(equator);
            probe.AddDirectionalLight(Vector3.up, (sky - equator) * 0.5f, 1f);
            probe.AddDirectionalLight(Vector3.down, (ground - equator) * 0.5f, 1f);
            RenderSettings.ambientProbe = probe;
        }
    }
}
