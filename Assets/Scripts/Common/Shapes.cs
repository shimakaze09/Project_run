using System.Collections.Generic;
using UnityEngine;

namespace Run.Common
{
    /// <summary>The distinct silhouettes used to make entities readable at a glance.</summary>
    public enum ShapeType
    {
        Rectangle,      // ground, platforms, steps
        RoundedSquare,  // crates / walls
        Square,         // coins
        Capsule,        // the player
        Circle,         // enemies
        Triangle        // spikes
    }

    /// <summary>
    /// Generates and caches simple placeholder sprites — one per <see cref="ShapeType"/> —
    /// entirely at runtime, so the project needs no imported art yet every entity has a
    /// recognisable silhouette rather than being another tinted square.
    /// </summary>
    /// <remarks>
    /// Each shape is rasterised into a small anti-aliased alpha texture inside the unit
    /// square, with <c>pixelsPerUnit == resolution</c> so the resulting sprite measures
    /// exactly 1×1 world unit. Callers then tint via <see cref="SpriteRenderer.color"/>
    /// and size via transform scale — identical to how a plain square would be used.
    /// </remarks>
    public static class Shapes
    {
        private const int Resolution = 64;     // texture size for curved/pointed shapes
        private const int SuperSamples = 3;    // NxN coverage sampling for smooth edges

        private static readonly Dictionary<ShapeType, Sprite> Cache = new Dictionary<ShapeType, Sprite>();

        public static Sprite Rectangle => Get(ShapeType.Rectangle);
        public static Sprite RoundedSquare => Get(ShapeType.RoundedSquare);
        public static Sprite Square => Get(ShapeType.Square);
        public static Sprite Capsule => Get(ShapeType.Capsule);
        public static Sprite Circle => Get(ShapeType.Circle);
        public static Sprite Triangle => Get(ShapeType.Triangle);

        public static Sprite Get(ShapeType type)
        {
            if (Cache.TryGetValue(type, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = Build(type);
            Cache[type] = sprite;
            return sprite;
        }

        private static Sprite Build(ShapeType type)
        {
            // Plain rectangles/squares need no anti-aliasing — a single white texel is enough.
            bool flat = type == ShapeType.Rectangle || type == ShapeType.Square;
            int resolution = flat ? 1 : Resolution;

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, mipChain: false)
            {
                name = "Shape_" + type,
                filterMode = flat ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (flat)
            {
                texture.SetPixel(0, 0, Color.white);
            }
            else
            {
                var pixels = new Color32[resolution * resolution];
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        byte alpha = (byte)Mathf.RoundToInt(Coverage(type, x, y, resolution) * 255f);
                        pixels[y * resolution + x] = new Color32(255, 255, 255, alpha);
                    }
                }
                texture.SetPixels32(pixels);
            }

            texture.Apply();

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution,                    // pixelsPerUnit → sprite is 1×1 world unit
                0,
                SpriteMeshType.FullRect);
            sprite.name = "Shape_" + type;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>Fraction of a pixel covered by the shape, via NxN super-sampling.</summary>
        private static float Coverage(ShapeType type, int x, int y, int resolution)
        {
            int hits = 0;
            for (int sy = 0; sy < SuperSamples; sy++)
            {
                for (int sx = 0; sx < SuperSamples; sx++)
                {
                    float u = (x + (sx + 0.5f) / SuperSamples) / resolution;
                    float v = (y + (sy + 0.5f) / SuperSamples) / resolution;
                    if (IsInside(type, u, v))
                    {
                        hits++;
                    }
                }
            }

            return hits / (float)(SuperSamples * SuperSamples);
        }

        /// <summary>Point-in-shape test in unit space (u, v ∈ [0, 1]).</summary>
        private static bool IsInside(ShapeType type, float u, float v)
        {
            switch (type)
            {
                case ShapeType.Circle:
                    return new Vector2(u - 0.5f, v - 0.5f).sqrMagnitude <= 0.25f;

                case ShapeType.Capsule:
                {
                    const float radius = 0.34f;
                    if (Mathf.Abs(u - 0.5f) > radius)
                    {
                        return false;
                    }
                    if (v >= radius && v <= 1f - radius)
                    {
                        return true; // straight body
                    }
                    float capCenterY = v < radius ? radius : 1f - radius;
                    return new Vector2(u - 0.5f, v - capCenterY).sqrMagnitude <= radius * radius;
                }

                case ShapeType.Triangle:
                {
                    // Point-up isoceles triangle (a spike tooth).
                    var apex = new Vector2(0.5f, 0.95f);
                    var left = new Vector2(0.05f, 0.06f);
                    var right = new Vector2(0.95f, 0.06f);
                    return PointInTriangle(new Vector2(u, v), apex, left, right);
                }

                case ShapeType.RoundedSquare:
                {
                    // Signed distance to a rounded box centred in the unit square.
                    const float pad = 0.05f;
                    const float corner = 0.18f;
                    var half = new Vector2(0.5f - pad, 0.5f - pad);
                    var q = new Vector2(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) - (half - new Vector2(corner, corner));
                    float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
                                    + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - corner;
                    return outside <= 0f;
                }

                default:
                    return true;
            }
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = EdgeSign(p, a, b);
            float d2 = EdgeSign(p, b, c);
            float d3 = EdgeSign(p, c, a);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float EdgeSign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }
    }
}
