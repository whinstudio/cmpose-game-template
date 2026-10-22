using UnityEngine;
using UnityEngine.UI;

namespace Kturker.UnityBase.Core.Presentation
{
    // The layers of a kit box, back to front: Shadow (Soft: a blurred drop shadow), Edge (Raised: the darker base the
    // box stands on; or an outline), Body (the box), Shine (Raised: a faint gloss on the upper half)
    public static class KitShape
    {
        // Corners of radius units (0: square; KitSprites.Pill: a pill or circle)
        public static void Round(Image image, float radius)
        {
            var sprite = KitSprites.Rounded;
            var pill = image.GetComponent<KitPill>();
            var wantsPill = radius >= KitSprites.Pill && sprite != null;
            if (pill != null && !wantsPill) pill.enabled = false;
            if (radius < 0.5f || sprite == null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.pixelsPerUnitMultiplier = 1f;
                return;
            }
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = KitSprites.RoundedRadius / radius;
            if (!wantsPill) return;
            // Half the box's short side, kept as the box resizes
            if (pill == null) pill = image.gameObject.AddComponent<KitPill>();
            pill.enabled = true;
            pill.Fit();
        }

        // The corner radius Round gave the image, in units (KitSprites.Pill for a pill; before slice borders shrink to
        // fit a small box)
        public static float RadiusOf(Image image)
        {
            if (image.sprite == null || image.type != Image.Type.Sliced) return 0f;
            var pill = image.GetComponent<KitPill>();
            return pill != null && pill.enabled ? KitSprites.Pill : KitSprites.RoundedRadius / image.pixelsPerUnitMultiplier;
        }

        // A skin sprite (GameSkin) on the body in its own colours, sliceScale units to a source pixel, tiled (a pixel
        // pack: edges and centre repeat) or sliced; the other layers off
        public static void Dress(Image shadow, Image edge, Image body, Image shine, Sprite sprite, float sliceScale, bool tiled)
        {
            var pill = body.GetComponent<KitPill>();
            if (pill != null) pill.enabled = false;
            body.sprite = sprite;
            body.type = tiled ? Image.Type.Tiled : Image.Type.Sliced;
            body.fillCenter = true;
            body.pixelsPerUnitMultiplier = 1f / sliceScale;
            Fix(body, Color.white);
            foreach (var layer in new[] { shadow, edge, shine })
                if (layer != null) layer.gameObject.SetActive(false);
        }

        // A skin sprite (GameSkin) on a single image in its own colours, sliceScale units to a source pixel: tiled (a
        // pixel pack) or sliced when it has a border, else whole at its own aspect
        public static void DressImage(Image image, Sprite sprite, float sliceScale, bool pixel)
        {
            var pill = image.GetComponent<KitPill>();
            if (pill != null) pill.enabled = false;
            var bordered = sprite.border != Vector4.zero;
            image.sprite = sprite;
            image.type = !bordered ? Image.Type.Simple : pixel ? Image.Type.Tiled : Image.Type.Sliced;
            image.preserveAspect = !bordered;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = 1f / sliceScale;
            Fix(image, Color.white);
        }

        // Back to the kit's shape of radius units in its role (DressImage undone)
        public static void UndressImage(Image image, float radius)
        {
            image.preserveAspect = false;
            Round(image, radius);
            Fix(image, null);
        }

        // A fixed colour on a themed graphic (null: its role's again)
        public static void Fix(Graphic graphic, Color? colour)
        {
            var themed = graphic.GetComponent<ThemedGraphic>();
            if (themed == null) themed = graphic.gameObject.AddComponent<ThemedGraphic>();
            themed.SetFixed(colour);
            if (colour.HasValue) graphic.color = colour.Value;
        }

        public static void Layers(Image shadow, Image edge, Image body, Image shine, float radius, ThemeStyle style,
            ThemeRole bodyRole, ThemeRole? outline = null, float depthScale = 1f)
        {
            var surface = style != null ? style.Surface : SurfaceStyle.Flat;
            var strength = style != null ? style.ShadowStrength : 0f;
            var raised = surface == SurfaceStyle.Raised;
            Round(body, radius);
            SetRole(body, bodyRole, 0f, 1f);
            if (shadow != null)
            {
                var soft = surface == SurfaceStyle.Soft && strength > 0f;
                shadow.gameObject.SetActive(soft);
                if (soft) SoftShadow(shadow, radius, strength);
            }
            if (edge != null)
            {
                var outlined = outline.HasValue;
                edge.gameObject.SetActive(raised || outlined);
                var depth = raised ? (6f + 10f * strength) * depthScale : 0f;
                var grow = outlined ? 4f : 0f;
                Round(edge, radius + grow);
                // An outline also rims the top (a light body on a light card would read as cut off); a raised base
                // only shows below
                Offsets(edge, -grow, -grow - depth, grow, outlined ? grow : grow - depth);
                if (outlined) SetRole(edge, outline.Value, raised ? 0.12f : 0f, 1f);
                else SetRole(edge, bodyRole, 0.28f, 1f);
            }
            if (shine != null)
            {
                shine.gameObject.SetActive(raised);
                if (raised)
                {
                    Round(shine, Mathf.Max(radius - 6f, 0f));
                    var rect = shine.rectTransform;
                    rect.anchorMin = new Vector2(0f, 0.52f);
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = new Vector2(8f, 0f);
                    rect.offsetMax = new Vector2(-8f, -6f);
                    SetRole(shine, ThemeRole.Picture, 0f, 0.16f);
                }
            }
        }

        // A blurred box under one of corner radius units, dropped a little; strength is its opacity
        public static void SoftShadow(Image shadow, float radius, float strength)
        {
            var r = Mathf.Clamp(radius, 6f, 36f);
            var multiplier = KitSprites.ShadowRadius / r;
            var blur = KitSprites.ShadowBlur / multiplier;
            var drop = 4f + r * 0.4f;
            shadow.sprite = KitSprites.Shadow;
            shadow.type = Image.Type.Sliced;
            shadow.fillCenter = true;
            shadow.pixelsPerUnitMultiplier = multiplier;
            shadow.raycastTarget = false;
            Offsets(shadow, -blur, -blur - drop, blur, blur - drop);
            SetRole(shadow, ThemeRole.Scrim, 0f, strength);
        }

        // Stretches the image over its parent, moved out by the given units (left, bottom, right, top)
        public static void Offsets(Image image, float left, float bottom, float right, float top)
        {
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private static void SetRole(Graphic graphic, ThemeRole role, float shade, float opacity)
        {
            var themed = graphic.GetComponent<ThemedGraphic>();
            if (themed == null) themed = graphic.gameObject.AddComponent<ThemedGraphic>();
            themed.SetFixed(null);
            if (themed.Role != role) themed.SetRole(role);
            themed.SetTone(shade, opacity);
        }
    }
}
