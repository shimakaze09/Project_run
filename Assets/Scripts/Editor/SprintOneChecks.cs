#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Run.Core;
using Run.Generation;
using Run.Collectibles;
using Run.Hazards;
using Run.UI;

namespace Run.EditorTools
{
    /// <summary>Deterministic, repeatable checks. Restores saved player data in finally.</summary>
    public static class SprintOneChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        [MenuItem("Tools/Endless Runner/Verify Sprint One")]
        public static void RunMenu() { Debug.Log(RunChecks()); }

        public static string RunChecks()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            string[] keys = { "run.lastRunDistance", "run.coins", "run.bestDistanceScore" };
            bool[] exists = Array.ConvertAll(keys, PlayerPrefs.HasKey);
            float last = PlayerPrefs.GetFloat(keys[0]);
            int coins = PlayerPrefs.GetInt(keys[1]);
            int best = PlayerPrefs.GetInt(keys[2]);
            var root = new GameObject("Sprint One Checks");
            root.SetActive(false);
            var result = new StringBuilder();
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                var gm = root.AddComponent<GameManager>();
                Call(gm, "Awake");
                typeof(GameManager).GetField("_distanceSource", Private).SetValue(gm, root.transform);
                Check(gm.LastRunProgress == 0f, "First run must have no target / no divide-by-zero.");
                gm.AddCoins(1);
                Check(gm.Coins == 0, "Ready coins ignored.");
                typeof(GameManager).GetProperty("LastRunDistance").GetSetMethod(true).Invoke(gm, new object[] { 100f });
                var hud = root.AddComponent<HudController>();
                Call(hud, "Awake"); Call(hud, "Start");
                gm.BeginRun();
                root.transform.position = new Vector3(50f, 0f, 0f);
                Call(gm, "RefreshScore");
                var fill = (RectTransform)typeof(HudController).GetField("_progressFill", Private).GetValue(hud);
                Check(gm.LastRunProgress == 0.5f && fill.anchorMax.x == 0.5f, "50m / 100m must draw 50%.");
                gm.AddCoins(5); gm.AddCoins(-1); gm.AddCoins(0);
                Check(gm.Score == 50 && gm.Coins == 5 && gm.RunCoins == 5, "Currency must not affect score.");
                Check(PlayerPrefs.GetInt(keys[1]) == 5, "Wallet must be saved.");
                var popup = (UnityEngine.UI.Text)typeof(HudController).GetField("_coinPopup", Private).GetValue(hud);
                var count = (UnityEngine.UI.Text)typeof(HudController).GetField("_coinsLabel", Private).GetValue(hud);
                gm.AddCoins(1);
                Check(count.text == "6" && popup.text == "+1" && popup.enabled, "Coin HUD must show only total and +1.");
                Call(hud, "TickEffects", 0.35f);
                Check(popup.color.a > 0f && popup.color.a < 1f && popup.rectTransform.anchoredPosition.y > -112f, "Popup must float and fade.");
                Call(hud, "TickEffects", 0.4f);
                Check(!popup.enabled, "Popup must disappear.");
                result.AppendLine("PASS halfway: score=50, wallet=5, HUD fill=0.5; invalid/Ready coin grants ignored");
                root.transform.position = new Vector3(100f, 0f, 0f); Call(gm, "RefreshScore");
                Check(fill.anchorMax.x == 1f && gm.State == GameState.Playing, "Matching target fills without ending endless run.");
                var group = (CanvasGroup)typeof(HudController).GetField("_barGroup", Private).GetValue(hud);
                Call(hud, "TickEffects", 0.3f);
                Check(Mathf.Abs(group.alpha - 0.5f) < 0.001f, "Full bar must fade halfway after 0.3 seconds.");
                Call(hud, "TickEffects", 0.3f);
                Check(group.alpha == 0f, "Full bar must disappear after 0.6 seconds.");
                foreach (var label in hud.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                    Check(!label.text.Contains("FIRST RUN") && !label.text.Contains("LAST RUN") && !label.text.Contains("this run"), "Unwanted run labels.");
                result.AppendLine("PASS HUD refinement: count=6, pickup=+1 floats/fades; bar alpha=0.5 at 0.3s and 0 at 0.6s; no run labels");
                root.transform.position = new Vector3(125f, 0f, 0f); Call(gm, "RefreshScore");
                Check(gm.LastRunProgress == 1f, "Progress must clamp above the target.");
                root.transform.position += new Vector3(-1000f, 0f, 0f);
                gm.ShiftDistanceOrigin(-1000f); Call(gm, "RefreshScore");
                Check(gm.Distance == 125f, "Rebase must preserve distance.");
                gm.NotifyPlayerDied(DeathCause.Hazard); gm.NotifyPlayerDied(DeathCause.FellIntoPit); gm.AddCoins(1);
                Check(gm.State == GameState.GameOver && gm.BestScore == 125 && gm.Coins == 6, "Run finalization must be idempotent.");
                Check(PlayerPrefs.GetFloat(keys[0]) == 125f && gm.LastRunDistance == 100f, "Last run saved; target frozen.");
                result.AppendLine("PASS target/exceeded: fill=1, remains Playing; rebase=125; death saves last=125 and best=125 once");
                UnityEngine.Object.DestroyImmediate(hud);
                Call(gm, "OnDestroy"); UnityEngine.Object.DestroyImmediate(gm);
                gm = root.AddComponent<GameManager>(); Call(gm, "Awake");
                Check(gm.LastRunDistance == 125f && gm.Coins == 6 && gm.BestScore == 125, "Reload must restore progress and wallet.");
                typeof(GameManager).GetField("_distanceSource", Private).SetValue(gm, root.transform);
                gm.BeginRun(); root.transform.position += new Vector3(20f, 0f, 0f);
                gm.NotifyPlayerDied(DeathCause.Hazard);
                Check(PlayerPrefs.GetFloat(keys[0]) == 20f && gm.BestScore == 125, "Target must be LAST run, not all-time best.");
                result.AppendLine("PASS reload: wallet=6; shorter next run saves last=20 while best remains125");
                Call(gm, "OnDestroy");

                var prefabs = Resources.LoadAll<LevelChunk>("Levels");
                Check(prefabs.Length == 8, "Expected eight authored layouts.");
                foreach (var prefab in prefabs)
                {
                    Check(prefab.Width > 0f, "Invalid chunk width.");
                    var chunk = UnityEngine.Object.Instantiate(prefab, root.transform);
                    chunk.Place(100f);
                    foreach (var renderer in chunk.GetComponentsInChildren<SpriteRenderer>())
                        Check(renderer.sprite != null && AssetDatabase.Contains(renderer.sprite), "Prefab sprites must be persistent assets.");
                    foreach (var coin in chunk.GetComponentsInChildren<Coin>()) coin.gameObject.SetActive(false);
                    chunk.Recycle(); chunk.Place(200f);
                    foreach (var coin in chunk.GetComponentsInChildren<Coin>(true))
                        Check(coin.gameObject.activeSelf, "Pooled coins must respawn.");
                    float end = chunk.EndX; chunk.Shift(-1000f);
                    Check(Mathf.Abs(chunk.EndX - (end - 1000f)) < 0.001f, "Prefab shift must preserve span.");
                    foreach (var enemy in chunk.GetComponentsInChildren<PatrolEnemy>())
                    {
                        float min = (float)typeof(PatrolEnemy).GetField("_minX", Private).GetValue(enemy);
                        float max = (float)typeof(PatrolEnemy).GetField("_maxX", Private).GetValue(enemy);
                        Check(min <= enemy.transform.position.x && max >= enemy.transform.position.x, "Patrol bounds must shift with prefab.");
                    }
                    UnityEngine.Object.DestroyImmediate(chunk.gameObject);
                }
                result.AppendLine("PASS eight prefab layouts: persistent sprites, coin reuse, world shifting, enemy patrol bounds");
                return result.ToString();
            }
            finally
            {
                var manager = root.GetComponent<GameManager>();
                if (manager != null) Call(manager, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(root);
                if (exists[0]) PlayerPrefs.SetFloat(keys[0], last); else PlayerPrefs.DeleteKey(keys[0]);
                if (exists[1]) PlayerPrefs.SetInt(keys[1], coins); else PlayerPrefs.DeleteKey(keys[1]);
                if (exists[2]) PlayerPrefs.SetInt(keys[2], best); else PlayerPrefs.DeleteKey(keys[2]);
                PlayerPrefs.Save();
            }
        }
    }
}
#endif
