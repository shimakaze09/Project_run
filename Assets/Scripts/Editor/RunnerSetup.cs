#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Run.CameraRig;
using Run.Common;
using Run.Core;
using Run.Generation;
using Run.Player;
using Run.UI;

namespace Run.EditorTools
{
    /// <summary>
    /// One-click scene assembly for the endless runner. Run
    /// <c>Tools ▸ Endless Runner ▸ Build Scene</c> to create (or repair) every object
    /// the game needs, then press Play.
    /// </summary>
    /// <remarks>
    /// This is the single, version-controlled source of truth for how the scene is
    /// wired. It is idempotent — running it again updates the existing objects rather
    /// than duplicating them — and it creates the "Ground" layer that solid pieces rely
    /// on, which only editor code can do.
    /// </remarks>
    public static class RunnerSetup
    {
        private const string GroundLayerName = "Ground";
        private const string PlayerTag = "Player";

        [MenuItem("Tools/Endless Runner/Build Scene")]
        public static void BuildScene()
        {
            EnsureLayerExists(GroundLayerName);

            ConfigureCamera();
            ConfigurePlayer();
            EnsureSystems();
            VisualArtSetup.Apply();

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[RunnerSetup] Scene ready. Press Play to run the endless runner.");
        }

        private static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
                camera = cameraObject.GetComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.53f, 0.81f, 0.92f); // sky blue
            camera.transform.position = new Vector3(0f, 1f, -10f);

            GetOrAdd<CameraFollow>(camera.gameObject);
        }

        private static void ConfigurePlayer()
        {
            var player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (player == null)
            {
                player = new GameObject("Player") { tag = PlayerTag };
            }

            player.transform.position = new Vector3(0f, 2f, 0f);
            player.transform.localScale = Vector3.one; // collider is sized absolutely; visual holds the scale

            // The sprite lives on a child so the triple-jump flip can spin it without
            // rotating the collider. Any sprite left on the root from an older build is removed.
            var rootRenderer = player.GetComponent<SpriteRenderer>();
            if (rootRenderer != null)
            {
                Object.DestroyImmediate(rootRenderer);
            }

            var visualTransform = player.transform.Find("Visual");
            GameObject visual = visualTransform != null ? visualTransform.gameObject : new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.85f, 1.0f, 1f);
            var renderer = GetOrAdd<SpriteRenderer>(visual);
            renderer.sprite = Shapes.Capsule;
            renderer.color = new Color(0.20f, 0.55f, 0.95f);
            renderer.sortingOrder = 10;

            // A capsule collider lets the player slide cleanly along walls and ledges.
            var oldBox = player.GetComponent<BoxCollider2D>();
            if (oldBox != null)
            {
                Object.DestroyImmediate(oldBox);
            }
            var capsule = GetOrAdd<CapsuleCollider2D>(player);
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.85f, 1.0f);

            var body = GetOrAdd<Rigidbody2D>(player);
            body.freezeRotation = true;
            body.gravityScale = 1f; // recalculated by PlayerController at runtime

            GetOrAdd<PlayerController>(player);
        }

        private static void EnsureSystems()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (manager == null)
            {
                manager = new GameObject("GameManager").AddComponent<GameManager>();
            }

            // The scroll line is a session-level service, so it lives alongside the manager.
            if (Object.FindFirstObjectByType<WorldScroller>() == null)
            {
                manager.gameObject.AddComponent<WorldScroller>();
            }

            if (Object.FindFirstObjectByType<LevelGenerator>() == null)
            {
                var generator = new GameObject("LevelGenerator");
                generator.AddComponent<LevelGenerator>();
            }

            if (Object.FindFirstObjectByType<HudController>() == null)
            {
                new GameObject("HUD").AddComponent<HudController>();
            }
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        /// <summary>Adds a named user layer if it does not already exist.</summary>
        private static void EnsureLayerExists(string layerName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[RunnerSetup] Could not open TagManager to add the layer.");
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");

            // User layers occupy indices 8..31; 0..7 are reserved by Unity.
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return; // already present
                }
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }

            Debug.LogWarning($"[RunnerSetup] No free user-layer slot available for \"{layerName}\".");
        }
    }
}
#endif
