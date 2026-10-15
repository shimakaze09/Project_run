using UnityEngine;
using Run.Common;
using Run.Core;
using Run.Effects;
using Run.Hazards;

namespace Run.EnvironmentMode
{
    /// <summary>The traps the environment player can place.</summary>
    public enum TrapKind
    {
        /// <summary>A solid block: harmless, but the runner must jump it or get held up.</summary>
        Crate,

        /// <summary>A row of crystal spikes; touching them costs a heart.</summary>
        Spikes,

        /// <summary>A patrolling drone that paces back and forth over a short stretch.</summary>
        Drone
    }

    /// <summary>
    /// Builds trap objects in code, drawn in the same twilight style as the authored level pieces
    /// (see <c>VisualArtSetup</c>), so no prefabs or art assets are needed.
    /// </summary>
    public static class TrapFactory
    {
        // Palette shared with VisualArtSetup.
        private static readonly Color Ink = new Color(0.045f, 0.1f, 0.17f);
        private static readonly Color Mint = new Color(0.43f, 0.94f, 0.78f);
        private static readonly Color Gold = new Color(1f, 0.8f, 0.3f);
        private static readonly Color CrateWood = new Color(0.8f, 0.5f, 0.22f);
        private static readonly Color CrateBand = new Color(0.52f, 0.3f, 0.13f);
        private static readonly Color SpikeBase = new Color(0.36f, 0.12f, 0.29f);
        private static readonly Color SpikeCore = new Color(0.98f, 0.35f, 0.52f);
        private static readonly Color SpikeFacet = new Color(1f, 0.73f, 0.77f);
        private static readonly Color DroneShell = new Color(0.69f, 0.26f, 0.49f);

        // Same High-Contrast pair LevelChunk uses, so every mode speaks one visual language:
        // one bright colour for anything lethal, one dark one for anything merely standable.
        private static readonly Color SafeHC = new Color(0.2f, 0.42f, 0.58f);
        private static readonly Color HazardHC = new Color(1f, 0.15f, 0.45f);

        /// <summary>How far a drone paces either side of where it was placed.</summary>
        public const float DronePatrolHalfRange = 1.1f;

        public const float DroneSpeed = 2.5f;

        /// <summary>The trap's body size in world units (its base sits on the ground).</summary>
        public static Vector2 BodySize(TrapKind kind)
        {
            switch (kind)
            {
                case TrapKind.Crate: return new Vector2(1f, 1f);
                case TrapKind.Spikes: return new Vector2(1.5f, 0.6f);
                default: return new Vector2(0.9f, 0.9f);
            }
        }

        /// <summary>Width of flat ground the trap needs, including a drone's whole patrol.</summary>
        public static float FootprintWidth(TrapKind kind) =>
            BodySize(kind).x + (kind == TrapKind.Drone ? DronePatrolHalfRange * 2f : 0f);

        /// <summary>Builds an inactive, ready-to-place trap.</summary>
        public static PlacedTrap Build(TrapKind kind, Transform parent)
        {
            var root = new GameObject(kind + " Trap");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            DrawArt(kind, root.transform);

            Vector2 size = BodySize(kind);
            switch (kind)
            {
                case TrapKind.Crate:
                    root.AddComponent<BoxCollider2D>().size = size;
                    int ground = LayerMask.NameToLayer("Ground");
                    if (ground >= 0) root.layer = ground;
                    break;

                case TrapKind.Spikes:
                    // Inset like the authored spike tooth, so only the crystals themselves hurt.
                    var spikes = root.AddComponent<BoxCollider2D>();
                    spikes.isTrigger = true;
                    spikes.size = new Vector2(size.x - 0.2f, 0.42f);
                    spikes.offset = new Vector2(0f, -0.06f);
                    root.AddComponent<Hazard>();
                    AddSparkle(root, 0.9f);
                    break;

                case TrapKind.Drone:
                    var body = root.AddComponent<CircleCollider2D>();
                    body.isTrigger = true;
                    body.radius = 0.4f;
                    root.AddComponent<PatrolEnemy>();
                    AddSparkle(root, 0.4f);
                    break;
            }

            var trap = root.AddComponent<PlacedTrap>();
            trap.Init(kind, size);
            return trap;
        }

        /// <summary>Builds just the artwork (no colliders or behaviour), for the cursor preview.</summary>
        public static GameObject BuildPreview(TrapKind kind, Transform parent)
        {
            var root = new GameObject(kind + " Preview");
            root.transform.SetParent(parent, false);
            DrawArt(kind, root.transform);
            return root;
        }

        private static void AddSparkle(GameObject root, float interval)
        {
            var sparkle = root.AddComponent<ItemSparkle>();
            sparkle.Kind = BurstKind.Ember;
            sparkle.Interval = interval;
        }

        private static void DrawArt(TrapKind kind, Transform parent)
        {
            var art = new GameObject("Artwork").transform;
            art.SetParent(parent, false);

            // Dark outline/accent shapes (Ink) stay as-is in both modes for silhouette; only the
            // colour that actually signals "safe to stand on" vs. "lethal" gets flattened to the
            // shared High-Contrast pair, so shape still reads but colour reads unambiguously too.
            bool hc = AccessibilityPreferences.HighContrast;

            switch (kind)
            {
                case TrapKind.Crate:
                    Color body = hc ? SafeHC : CrateWood;
                    Color band = hc ? SafeHC : CrateBand;
                    Draw(art, "Crate outline", ShapeType.RoundedSquare, 0f, 0f, 1f, 1f, Ink, 5);
                    Draw(art, "Crate body", ShapeType.RoundedSquare, 0f, 0f, 0.84f, 0.84f, body, 6);
                    Draw(art, "Crate band top", ShapeType.Rectangle, 0f, 0.29f, 0.84f, 0.1f, band, 7);
                    Draw(art, "Crate band bottom", ShapeType.Rectangle, 0f, -0.29f, 0.84f, 0.1f, band, 7);
                    Draw(art, "Crate brace", ShapeType.Rectangle, 0f, 0f, 0.1f, 0.78f, band, 7)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    Draw(art, "Crate rune", ShapeType.Circle, 0f, 0f, 0.22f, 0.22f, hc ? Ink : Mint, 8);
                    break;

                case TrapKind.Spikes:
                    Color core = hc ? HazardHC : SpikeCore;
                    Color facet = hc ? HazardHC : SpikeFacet;
                    for (int i = -1; i <= 1; i++)
                    {
                        float x = i * 0.5f;
                        Draw(art, "Spike base", ShapeType.Triangle, x, 0f, 0.5f, 0.6f, SpikeBase, 5);
                        Draw(art, "Crystal core", ShapeType.Triangle, x, 0.005f, 0.375f, 0.54f, core, 6);
                        Draw(art, "Crystal facet", ShapeType.Triangle, x - 0.04f, 0.03f, 0.13f, 0.41f, facet, 7);
                    }
                    break;

                case TrapKind.Drone:
                    Color shell = hc ? HazardHC : DroneShell;
                    Draw(art, "Drone shell", ShapeType.Circle, 0f, 0f, 0.9f, 0.9f, Ink, 6);
                    Draw(art, "Drone carapace", ShapeType.Circle, 0f, 0.04f, 0.82f, 0.74f, shell, 7);
                    Draw(art, "Drone visor", ShapeType.RoundedSquare, -0.06f, 0.02f, 0.71f, 0.26f, Ink, 8);
                    Draw(art, "Left eye", ShapeType.Circle, -0.2f, 0.04f, 0.1f, 0.1f, hc ? Ink : Gold, 9);
                    Draw(art, "Right eye", ShapeType.Circle, 0.03f, 0.04f, 0.1f, 0.1f, hc ? Ink : Gold, 9);
                    Draw(art, "Left foot", ShapeType.RoundedSquare, -0.22f, -0.34f, 0.22f, 0.13f, Ink, 9);
                    Draw(art, "Right foot", ShapeType.RoundedSquare, 0.22f, -0.34f, 0.22f, 0.13f, Ink, 9);
                    break;
            }
        }

        private static SpriteRenderer Draw(Transform parent, string name, ShapeType shape,
            float x, float y, float width, float height, Color color, int order)
        {
            var sprite = new GameObject(name).AddComponent<SpriteRenderer>();
            sprite.transform.SetParent(parent, false);
            sprite.transform.localPosition = new Vector3(x, y, 0f);
            sprite.transform.localScale = new Vector3(width, height, 1f);
            sprite.sprite = Shapes.Get(shape);
            sprite.color = color;
            sprite.sortingOrder = order;
            return sprite;
        }
    }
}
