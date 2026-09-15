#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Run.Effects;
using Run.Generation;
using Run.Collectibles;
using Run.Hazards;

namespace Run.EditorTools
{
    public static class VisualEffectsChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

        [MenuItem("Tools/Endless Runner/Verify Visual Effects")]
        public static void RunMenu() { Debug.Log(RunChecks()); }

        public static string RunChecks()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var host = new GameObject("Effect Checks"); host.SetActive(false);
            var effects = host.AddComponent<GameFeel>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                typeof(GameFeel).GetMethod("Awake", flags).Invoke(effects, null);
                foreach (BurstKind kind in Enum.GetValues(typeof(BurstKind)))
                {
                    int before = effects.TotalEmitted;
                    effects.Emit(kind, new Vector3(2f, 3f, 0f));
                    Check(effects.TotalEmitted > before, "Missing particles: " + kind);
                }
                int emitted = effects.TotalEmitted;
                int active = effects.ActiveCount;
                effects.Simulate(0f);
                Check(effects.ActiveCount == active, "Pause must freeze particles.");
                var renderer = Array.Find(host.GetComponentsInChildren<SpriteRenderer>(true), r => r.enabled);
                Vector3 old = renderer.transform.position;
                effects.Shift(-1000f);
                Check(Vector3.Distance(renderer.transform.position, old + Vector3.left * 1000f) < 0.001f, "World shift must preserve particles.");
                effects.Simulate(0.1f);
                Check(renderer.color.a < 1f, "Particles must fade.");
                for (int i = 0; i < 1000; i++) effects.Emit(BurstKind.Milestone, Vector3.zero);
                Check(effects.ActiveCount <= effects.Capacity && host.transform.childCount == effects.Capacity, "Particle pool must stay bounded.");
                effects.Simulate(5f);
                Check(effects.ActiveCount == 0, "Particles must expire without allocations or orphan visuals.");
                typeof(GameFeel).GetField("_intensity", flags).SetValue(effects, 0f);
                effects.Emit(BurstKind.Death, Vector3.zero);
                Check(effects.ActiveCount == 0, "Intensity zero must disable effects.");

                int decorated = 0;
                foreach (var prefab in Resources.LoadAll<LevelChunk>("Levels"))
                {
                    for (int i = 0; i < prefab.transform.childCount; i++)
                    {
                        var piece = prefab.transform.GetChild(i);
                        Check(piece.Find("Artwork") != null, "Every level piece should have editable artwork.");
                        if (piece.GetComponent<Coin>() != null || piece.GetComponent<Hazard>() != null)
                            Check(piece.GetComponent<ItemSparkle>() != null, "Missing collectible/hazard sparkle.");
                        decorated++;
                    }
                }
                var scenery = Resources.Load<GameObject>("Scenery/Twilight");
                Check(scenery != null && scenery.transform.Find("Near Mountains") != null && scenery.transform.Find("Far Mountains") != null,
                    "Missing authored parallax scenery.");
                foreach (var sr in scenery.GetComponentsInChildren<SpriteRenderer>())
                    Check(sr.sprite != null && AssetDatabase.Contains(sr.sprite), "Scenery must use persistent sprites.");
                return "PASS all 10 particle kinds: emitted=" + emitted + "; capacity=" + effects.Capacity +
                    "; 1000-burst stress bounded; pause, fade, expiry, rebase and intensity=0 verified\n" +
                    "PASS editable artwork on " + decorated + " level pieces; coin/hazard sparks; persistent two-layer scenery\n";
            }
            finally
            {
                typeof(GameFeel).GetMethod("OnDestroy", flags).Invoke(effects, null);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
#endif
