using System;
using UnityEngine;

namespace Kturker.UnityBase.Core.Presentation.Editor
{
    // The blur MenuStillImporter gives the game's still behind the menu: the picture is scaled (area average) so its short
    // side is BlurShortSide, then blurred with a separable Gaussian of BlurSigma pixels at that scale, the kernel cut at 3
    // sigma and kept inside the picture at its edges (the taps that would fall outside are dropped and the rest weighed up
    // to 1, so an edge pixel is not pulled towards its own colour). A picture whose short side is BlurShortSide or less is
    // blurred at its own size. Every channel is blurred alike, alpha too.
    public static class MenuStillBlur
    {
        public const int BlurShortSide = 270;
        public const float BlurSigma = 6f;

        public static Color32[] Blur(Color32[] pixels, int width, int height, out int outWidth, out int outHeight)
        {
            Check(pixels, width, height);
            OutputSize(width, height, out outWidth, out outHeight);
            var values = ToFloats(pixels);
            values = Filter(values, width, height, AreaTaps(width, outWidth), true);
            values = Filter(values, outWidth, height, AreaTaps(height, outHeight), false);
            values = Filter(values, outWidth, outHeight, GaussianTaps(outWidth), true);
            values = Filter(values, outWidth, outHeight, GaussianTaps(outHeight), false);
            return ToColors(values);
        }

        // The picture scaled down to outWidth x outHeight by averaging the source area each output pixel covers, without
        // the blur
        public static Color32[] Scale(Color32[] pixels, int width, int height, int outWidth, int outHeight)
        {
            Check(pixels, width, height);
            if (outWidth < 1 || outHeight < 1 || outWidth > width || outHeight > height)
                throw new ArgumentException($"{width} x {height} cannot scale down to {outWidth} x {outHeight}");
            var values = Filter(ToFloats(pixels), width, height, AreaTaps(width, outWidth), true);
            return ToColors(Filter(values, outWidth, height, AreaTaps(height, outHeight), false));
        }

        private static void Check(Color32[] pixels, int width, int height)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (width < 1 || height < 1 || pixels.Length != width * height)
                throw new ArgumentException($"{pixels.Length} pixels for {width} x {height}", nameof(pixels));
        }

        // Short side BlurShortSide, the long side in proportion; a picture no larger keeps its size
        private static void OutputSize(int width, int height, out int outWidth, out int outHeight)
        {
            var shortSide = Math.Min(width, height);
            outWidth = width;
            outHeight = height;
            if (shortSide <= BlurShortSide) return;
            var scale = (double)BlurShortSide / shortSide;
            outWidth = width == shortSide ? BlurShortSide : Math.Max(BlurShortSide, (int)Math.Round(width * scale));
            outHeight = height == shortSide ? BlurShortSide : Math.Max(BlurShortSide, (int)Math.Round(height * scale));
        }

        // One output index's weights over consecutive source indices from Start
        private struct Tap
        {
            public int Start;
            public float[] Weights;
        }

        // Each output pixel the average of the source span it covers (a partly covered source pixel by its share)
        private static Tap[] AreaTaps(int source, int target)
        {
            var taps = new Tap[target];
            var span = (double)source / target;
            for (var i = 0; i < target; i++)
            {
                var left = i * span;
                var right = Math.Min(source, (i + 1) * span);
                var first = Math.Min(source - 1, (int)Math.Floor(left));
                var last = Math.Max(first, Math.Min(source - 1, (int)Math.Ceiling(right) - 1));
                var weights = new float[last - first + 1];
                for (var j = first; j <= last; j++)
                    weights[j - first] = (float)Math.Max(0.0, Math.Min(right, j + 1) - Math.Max(left, j));
                taps[i] = new Tap { Start = first, Weights = Normalised(weights) };
            }
            return taps;
        }

        // The Gaussian's taps cut at 3 sigma, those outside the picture dropped
        private static Tap[] GaussianTaps(int length)
        {
            var radius = Mathf.CeilToInt(3f * BlurSigma);
            var kernel = new float[2 * radius + 1];
            for (var k = -radius; k <= radius; k++) kernel[k + radius] = Mathf.Exp(-k * k / (2f * BlurSigma * BlurSigma));
            var taps = new Tap[length];
            for (var i = 0; i < length; i++)
            {
                var first = Math.Max(0, i - radius);
                var last = Math.Min(length - 1, i + radius);
                var weights = new float[last - first + 1];
                for (var j = first; j <= last; j++) weights[j - first] = kernel[j - i + radius];
                taps[i] = new Tap { Start = first, Weights = Normalised(weights) };
            }
            return taps;
        }

        private static float[] Normalised(float[] weights)
        {
            var sum = 0f;
            foreach (var w in weights) sum += w;
            if (sum <= 0f)
            {
                // A span that rounding left empty: its first pixel alone
                Array.Clear(weights, 0, weights.Length);
                weights[0] = 1f;
                return weights;
            }
            for (var i = 0; i < weights.Length; i++) weights[i] /= sum;
            return weights;
        }

        // RGBA floats, 4 per pixel, rows as Texture2D.GetPixels32 gives them; along X the taps give each row its new
        // width, along Y each column its new height
        private static float[] Filter(float[] values, int width, int height, Tap[] taps, bool alongX)
        {
            var outWidth = alongX ? taps.Length : width;
            var outHeight = alongX ? height : taps.Length;
            var result = new float[outWidth * outHeight * 4];
            for (var y = 0; y < outHeight; y++)
                for (var x = 0; x < outWidth; x++)
                {
                    var tap = alongX ? taps[x] : taps[y];
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (var k = 0; k < tap.Weights.Length; k++)
                    {
                        var at = alongX ? (y * width + tap.Start + k) * 4 : ((tap.Start + k) * width + x) * 4;
                        var w = tap.Weights[k];
                        r += values[at] * w;
                        g += values[at + 1] * w;
                        b += values[at + 2] * w;
                        a += values[at + 3] * w;
                    }
                    var to = (y * outWidth + x) * 4;
                    result[to] = r;
                    result[to + 1] = g;
                    result[to + 2] = b;
                    result[to + 3] = a;
                }
            return result;
        }

        private static float[] ToFloats(Color32[] pixels)
        {
            var values = new float[pixels.Length * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                values[i * 4] = pixels[i].r;
                values[i * 4 + 1] = pixels[i].g;
                values[i * 4 + 2] = pixels[i].b;
                values[i * 4 + 3] = pixels[i].a;
            }
            return values;
        }

        private static Color32[] ToColors(float[] values)
        {
            var pixels = new Color32[values.Length / 4];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(ToByte(values[i * 4]), ToByte(values[i * 4 + 1]), ToByte(values[i * 4 + 2]), ToByte(values[i * 4 + 3]));
            return pixels;
        }

        private static byte ToByte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
    }
}
