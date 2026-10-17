using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kturker.UnityBase.Core.Presentation
{
    // Everything of a style family but its colours: fonts, corner radius, surfaces, motion, post-processing, the
    // menu background and the colours the game world draws with. One per app at Resources/Theme/Style.asset (light
    // and dark share it); SceneBuilder writes it.
    [CreateAssetMenu(menuName = "UnityBase/Theme Style", fileName = "Style")]
    public sealed class ThemeStyle : ScriptableObject
    {
        public const string ResourcePath = "Theme/Style";

        [SerializeField] private string familyId = "clean";
        [SerializeField] private TMP_FontAsset displayFont;
        [SerializeField] private TMP_FontAsset bodyFont;
        // Reference units (the canvas scaler's 1080 x 1920)
        [SerializeField, Range(0f, 48f)] private float cornerRadius;
        [SerializeField] private SurfaceStyle surface;
        [SerializeField, Range(0f, 1f)] private float shadowStrength;
        [SerializeField] private MotionProfile motion = MotionProfile.Snappy;
        // May be empty: 2D games often run without post-processing
        [SerializeField] private VolumeProfile post;
        [SerializeField] private BackgroundStyle background;
        // Three or four colours for game objects (pieces, lanes, obstacles), apart from the UI palette
        [SerializeField] private Color[] worldColors = { Color.red, Color.green, Color.blue };

        public string FamilyId => familyId;
        public TMP_FontAsset DisplayFont => displayFont;
        public TMP_FontAsset BodyFont => bodyFont;
        public float CornerRadius => cornerRadius;
        public SurfaceStyle Surface => surface;
        public float ShadowStrength => shadowStrength;
        public MotionProfile Motion => motion;
        public VolumeProfile Post => post;
        public BackgroundStyle Background => background;
        public IReadOnlyList<Color> WorldColors => worldColors;

        public TMP_FontAsset FontFor(TextRole role) => role == TextRole.Display || role == TextRole.Title ? displayFont : bodyFont;

        public static ThemeStyle Load()
        {
            var style = Resources.Load<ThemeStyle>(ResourcePath);
            if (style == null) throw new InvalidOperationException($"Resources/{ResourcePath}.asset is missing");
            return style;
        }
    }
}
