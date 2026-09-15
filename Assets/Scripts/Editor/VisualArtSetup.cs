#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Run.Common;
using Run.Core;
using Run.Player;
using Run.Collectibles;
using Run.Hazards;
using Run.Effects;

namespace Run.EditorTools
{
    /// <summary>Bakes editable sprite artwork into existing prefabs; never changes layout or colliders.</summary>
    public static class VisualArtSetup
    {
        private static readonly Color Ink = new Color(0.045f, 0.1f, 0.17f);
        private static readonly Color Mint = new Color(0.43f, 0.94f, 0.78f);
        private static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        [MenuItem("Tools/Endless Runner/Apply Twilight Art")]
        public static void Apply()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Levels" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // Only direct children are gameplay pieces. Their transforms/colliders stay intact.
                    for (int i = 0; i < root.transform.childCount; i++) DecoratePiece(root.transform.GetChild(i));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            BakeScenery();
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                DecoratePlayer(player.transform);
                if (player.GetComponent<RunnerVisuals>() == null) player.gameObject.AddComponent<RunnerVisuals>();
            }
            var game = Object.FindFirstObjectByType<GameManager>();
            if (game != null)
            {
                if (game.GetComponent<GameFeel>() == null) game.gameObject.AddComponent<GameFeel>();
                if (game.GetComponent<VisualAtmosphere>() == null) game.gameObject.AddComponent<VisualAtmosphere>();
            }
            if (Camera.main != null) Camera.main.backgroundColor = new Color(0.04f, 0.09f, 0.16f);
            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[VisualArtSetup] Twilight artwork saved. Physics and level layouts unchanged.");
        }

        private static Sprite Shape(ShapeType kind)
        {
            string path = "Assets/Art/Shape_" + kind + ".asset";
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) if (asset is Sprite sprite) return sprite;
            var source = Shapes.Get(kind);
            var texture = Object.Instantiate(source.texture); texture.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(texture, path);
            var saved = Sprite.Create(texture, source.rect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            saved.name = "Shape_" + kind; saved.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(saved, texture);
            return saved;
        }

        private static Transform ArtRoot(Transform parent)
        {
            var old = parent.Find("Artwork");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Artwork").transform;
            root.SetParent(parent, false);
            root.localScale = new Vector3(1f / parent.localScale.x, 1f / parent.localScale.y, 1f);
            return root;
        }

        private static SpriteRenderer Draw(Transform parent, string name, ShapeType kind,
            float x, float y, float width, float height, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = new Vector3(x, y, 0f);
            sr.transform.localScale = new Vector3(width, height, 1f);
            sr.sprite = Shape(kind); sr.color = color; sr.sortingOrder = order;
            return sr;
        }

        private static void DecoratePiece(Transform piece)
        {
            var sr = piece.GetComponent<SpriteRenderer>();
            if (sr == null) return;
            var art = ArtRoot(piece);
            float w = piece.localScale.x, h = piece.localScale.y;
            if (piece.GetComponent<Coin>() != null)
            {
                sr.enabled = false;
                Draw(art, "Coin rim", ShapeType.Circle, 0, 0, w, h, new Color(0.72f, 0.39f, 0.13f), 6);
                Draw(art, "Coin face", ShapeType.Circle, 0, 0.02f, w * 0.83f, h * 0.83f, Gold, 7);
                var gem = Draw(art, "Coin stamp", ShapeType.Square, 0, 0.02f, w * 0.26f, h * 0.26f, new Color(1f, 0.97f, 0.7f), 8);
                gem.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                Draw(art, "Coin shine", ShapeType.Circle, -w * 0.2f, h * 0.23f, w * 0.12f, h * 0.12f, Color.white, 9);
                var sparkle = piece.GetComponent<ItemSparkle>() ?? piece.gameObject.AddComponent<ItemSparkle>();
                sparkle.Kind = BurstKind.Spark; sparkle.Interval = 0.8f;
            }
            else if (piece.GetComponent<PatrolEnemy>() != null)
            {
                sr.enabled = false;
                Draw(art, "Drone shell", ShapeType.Circle, 0, 0, w, h, Ink, 6);
                Draw(art, "Drone carapace", ShapeType.Circle, 0, 0.04f, w * 0.91f, h * 0.82f, new Color(0.69f, 0.26f, 0.49f), 7);
                Draw(art, "Drone visor", ShapeType.RoundedSquare, -0.06f, 0.02f, w * 0.79f, h * 0.29f, Ink, 8);
                Draw(art, "Left eye", ShapeType.Circle, -w * 0.22f, 0.04f, 0.1f, 0.1f, Gold, 9);
                Draw(art, "Right eye", ShapeType.Circle, w * 0.03f, 0.04f, 0.1f, 0.1f, Gold, 9);
                Draw(art, "Left foot", ShapeType.RoundedSquare, -w * 0.24f, -h * 0.38f, 0.22f, 0.13f, Ink, 9);
                Draw(art, "Right foot", ShapeType.RoundedSquare, w * 0.24f, -h * 0.38f, 0.22f, 0.13f, Ink, 9);
                var sparkle = piece.GetComponent<ItemSparkle>() ?? piece.gameObject.AddComponent<ItemSparkle>();
                sparkle.Kind = BurstKind.Ember; sparkle.Interval = 0.4f;
            }
            else if (piece.GetComponent<Hazard>() != null)
            {
                sr.color = new Color(0.36f, 0.12f, 0.29f);
                Draw(art, "Crystal core", ShapeType.Triangle, 0, 0.01f, w * 0.75f, h * 0.9f, new Color(0.98f, 0.35f, 0.52f), 6);
                Draw(art, "Crystal facet", ShapeType.Triangle, -w * 0.08f, 0.05f, w * 0.26f, h * 0.68f, new Color(1f, 0.73f, 0.77f), 7);
                var sparkle = piece.GetComponent<ItemSparkle>() ?? piece.gameObject.AddComponent<ItemSparkle>();
                sparkle.Kind = BurstKind.Ember; sparkle.Interval = 0.9f;
            }
            else
            {
                sr.color = new Color(0.075f, 0.17f, 0.21f);
                Draw(art, "Surface shadow", ShapeType.Rectangle, 0, h * 0.5f - 0.12f, w, 0.24f, new Color(0.1f, 0.29f, 0.3f), 1);
                Draw(art, "Moss edge", ShapeType.Rectangle, 0, h * 0.5f - 0.035f, w, 0.07f, Mint, 2);
                int blades = Mathf.Max(1, Mathf.FloorToInt(w / 0.65f));
                for (int i = 0; i < blades; i++)
                {
                    float x = -w * 0.5f + (i + 0.5f) * w / blades;
                    Draw(art, "Grass tuft", ShapeType.Triangle, x, h * 0.5f + 0.045f, 0.12f, 0.12f + (i % 3) * 0.04f, Mint * new Color(0.75f, 0.9f, 0.8f, 1f), 2);
                    if (h > 1f)
                        Draw(art, "Stone fleck", ShapeType.RoundedSquare, x, h * 0.5f - 0.5f - (i % 4) * 0.45f, 0.28f, 0.09f,
                            new Color(0.14f, 0.26f, 0.3f), 1);
                }
            }
        }

        private static void DecoratePlayer(Transform player)
        {
            var visual = player.Find("Visual");
            if (visual == null) return;
            var original = visual.GetComponent<SpriteRenderer>();
            if (original != null) original.enabled = false;
            var art = ArtRoot(visual);
            art.localScale = Vector3.one;
            Draw(art, "Suit outline", ShapeType.Capsule, 0, 0, 0.96f, 1f, Ink, 10);
            Draw(art, "Suit", ShapeType.Capsule, 0, 0.03f, 0.8f, 0.85f, new Color(0.22f, 0.72f, 0.86f), 11);
            Draw(art, "Helmet light", ShapeType.Capsule, -0.17f, 0.14f, 0.26f, 0.56f, new Color(0.6f, 0.96f, 0.96f), 12);
            Draw(art, "Visor", ShapeType.RoundedSquare, 0.17f, 0.14f, 0.67f, 0.3f, Ink, 13);
            Draw(art, "Visor glow", ShapeType.RoundedSquare, 0.28f, 0.15f, 0.31f, 0.1f, new Color(0.8f, 1f, 0.88f), 14);
            Draw(art, "Scarf", ShapeType.Rectangle, -0.15f, -0.17f, 0.7f, 0.13f, new Color(1f, 0.43f, 0.45f), 14);
            var tail = Draw(art, "Scarf tail", ShapeType.Triangle, -0.6f, -0.12f, 0.22f, 0.42f, new Color(1f, 0.43f, 0.45f), 9);
            tail.transform.localRotation = Quaternion.Euler(0, 0, 85f);
            Draw(art, "Left boot", ShapeType.RoundedSquare, -0.18f, -0.4f, 0.29f, 0.18f, Ink, 15);
            Draw(art, "Right boot", ShapeType.RoundedSquare, 0.19f, -0.4f, 0.29f, 0.18f, Ink, 15);
        }

        private static void BakeScenery()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Scenery")) AssetDatabase.CreateFolder("Assets/Resources", "Scenery");
            var root = new GameObject("Twilight");
            try
            {
                for (int i = 0; i < 64; i++)
                    Draw(root.transform, "Sky gradient", ShapeType.Rectangle, 0, -10f + i * 0.3125f, 160f, 0.32f,
                        Color.Lerp(new Color(0.22f, 0.48f, 0.5f), new Color(0.025f, 0.065f, 0.15f), i / 63f), -100);
                Draw(root.transform, "Moon halo", ShapeType.Circle, 5.2f, 2.7f, 4.4f, 4.4f, new Color(0.9f, 0.83f, 0.65f, 0.035f), -99);
                Draw(root.transform, "Moon halo inner", ShapeType.Circle, 5.2f, 2.7f, 3.5f, 3.5f, new Color(0.9f, 0.83f, 0.65f, 0.06f), -98);
                Draw(root.transform, "Moon", ShapeType.Circle, 5.2f, 2.7f, 2.5f, 2.5f, new Color(1f, 0.89f, 0.67f), -97);
                for (int i = 0; i < 48; i++)
                {
                    float x = -18f + ((i * 7.39f) % 36f), y = 0.4f + (i * 1.17f) % 8f;
                    float size = i % 7 == 0 ? 0.08f : 0.035f;
                    Draw(root.transform, "Star", ShapeType.Circle, x, y, size, size, new Color(0.85f, 0.94f, 0.93f, 0.35f + (i % 3) * 0.2f), -96);
                }
                var far = new GameObject("Far Mountains").transform; far.SetParent(root.transform, false);
                var near = new GameObject("Near Mountains").transform; near.SetParent(root.transform, false);
                for (int tile = -1; tile <= 2; tile++)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        float height = 4f + (i * 1.37f) % 3f;
                        Draw(far, "Distant ridge", ShapeType.Triangle, tile * 40f - 20f + i * 5f, -4.5f + height * 0.5f, 11f, height,
                            new Color(0.14f, 0.28f, 0.35f), -85);
                        Draw(near, "Forest ridge", ShapeType.Triangle, tile * 40f - 20f + i * 5f, -5f + height * 0.32f, 8f, height * 0.75f,
                            new Color(0.075f, 0.2f, 0.26f), -75);
                        float x = tile * 40f - 19f + i * 5f;
                        Draw(near, "Pine trunk", ShapeType.Rectangle, x, -1.6f, 0.1f, 2.7f, new Color(0.07f, 0.17f, 0.22f), -74);
                        for (int b = 0; b < 3; b++)
                            Draw(near, "Pine crown", ShapeType.Triangle, x, -1.3f + b * 0.42f, 1.5f - b * 0.3f, 1.5f, new Color(0.065f, 0.18f, 0.23f), -73);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/Scenery/Twilight.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
