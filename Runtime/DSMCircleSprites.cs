#nullable enable

using UnityEngine;

namespace DataSaveManager
{
    /// <summary>Anti-aliased disc and ring sprites drawn once in code, so the Lock Screen needs no image assets.</summary>
    internal static class DSMCircleSprites
    {
        private const int Size = 128;

        private static Sprite? _disc;
        private static Sprite? _ring;

        public static Sprite Disc => _disc != null ? _disc : _disc = Create("DSM Disc", 0f);

        /// <summary>A ring whose stroke is about a tenth of its diameter.</summary>
        public static Sprite Ring => _ring != null ? _ring : _ring = Create("DSM Ring", 0.8f);

        private static Sprite Create(string name, float innerRatio)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            var outer = Size * 0.5f;
            var inner = outer * innerRatio;
            var pixels = new Color32[Size * Size];
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var dx = x + 0.5f - outer;
                var dy = y + 0.5f - outer;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                var alpha = Mathf.Clamp01(outer - d);
                if (innerRatio > 0f) alpha = Mathf.Min(alpha, Mathf.Clamp01(d - inner));
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
