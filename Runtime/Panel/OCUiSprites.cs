#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace OperatorConsole
{
    /// <summary>The sprites the Runtime Panel's controls use, drawn once in code because Unity's built-in UI skin sprites can only be loaded in the Editor.</summary>
    internal static class OCUiSprites
    {
        private const int RoundedSize = 32;
        private const float RoundedRadius = 6f;
        private const int IconSize = 64;

        private static Sprite? _rounded;
        private static Sprite? _checkmark;
        private static Sprite? _arrow;

        /// <summary>A 9-sliced rounded rectangle, for buttons, fields and backgrounds.</summary>
        public static Sprite Rounded => _rounded != null ? _rounded : _rounded = CreateRounded();

        public static Sprite Checkmark => _checkmark != null ? _checkmark : _checkmark = CreateIcon("OC Checkmark", CheckmarkAlpha);

        /// <summary>A downward triangle, for the dropdown.</summary>
        public static Sprite Arrow => _arrow != null ? _arrow : _arrow = CreateIcon("OC Arrow", ArrowAlpha);

        public static Sprite Knob => OCCircleSprites.Disc;

        public static DefaultControls.Resources UiResources() => new()
        {
            standard = Rounded, background = Rounded, inputField = Rounded,
            checkmark = Checkmark, knob = Knob, dropdown = Arrow, mask = Rounded,
        };

        public static TMPro.TMP_DefaultControls.Resources TmpUiResources() => new()
        {
            standard = Rounded, background = Rounded, inputField = Rounded,
            checkmark = Checkmark, knob = Knob, dropdown = Arrow, mask = Rounded,
        };

        private static Sprite CreateRounded()
        {
            var pixels = new Color32[RoundedSize * RoundedSize];
            var half = RoundedSize * 0.5f;
            var inner = half - RoundedRadius;
            for (var y = 0; y < RoundedSize; y++)
            for (var x = 0; x < RoundedSize; x++)
            {
                var qx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                var qy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                var alpha = Mathf.Clamp01(RoundedRadius - Mathf.Sqrt(qx * qx + qy * qy) + 0.5f);
                pixels[y * RoundedSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }

            var border = new Vector4(RoundedRadius + 1f, RoundedRadius + 1f, RoundedRadius + 1f, RoundedRadius + 1f);
            return CreateSprite("OC Rounded", RoundedSize, pixels, border);
        }

        private static float CheckmarkAlpha(float x, float y)
        {
            const float thickness = 0.09f;
            var d = Mathf.Min(
                SegmentDistance(x, y, 0.2f, 0.52f, 0.42f, 0.3f),
                SegmentDistance(x, y, 0.42f, 0.3f, 0.8f, 0.72f));
            return Mathf.Clamp01((thickness - d) * IconSize + 0.5f);
        }

        private static float ArrowAlpha(float x, float y)
        {
            if (y < 0.32f || y > 0.68f) return 0f;
            var halfWidth = (y - 0.32f) / 0.36f * 0.28f;
            return Mathf.Clamp01((halfWidth - Mathf.Abs(x - 0.5f)) * IconSize + 0.5f);
        }

        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            var abx = bx - ax;
            var aby = by - ay;
            var t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            var dx = px - (ax + abx * t);
            var dy = py - (ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static Sprite CreateIcon(string name, System.Func<float, float, float> alphaAt)
        {
            var pixels = new Color32[IconSize * IconSize];
            for (var y = 0; y < IconSize; y++)
            for (var x = 0; x < IconSize; x++)
            {
                var alpha = alphaAt((x + 0.5f) / IconSize, (y + 0.5f) / IconSize);
                pixels[y * IconSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            return CreateSprite(name, IconSize, pixels, Vector4.zero);
        }

        private static Sprite CreateSprite(string name, int size, Color32[] pixels, Vector4 border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
