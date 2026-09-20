#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Run.Player;
using Run.CameraRig;

namespace Run.EditorTools
{
    public static class CameraShakeChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
        }
        private static void Call(Component component, string method, params object[] args)
        {
            component.GetType().GetMethod(method, Flags | BindingFlags.Public).Invoke(component, args);
        }
        private static void Set(Component component, string field, object value)
        {
            component.GetType().GetField(field, Flags).SetValue(component, value);
        }

        [MenuItem("Tools/Endless Runner/Verify Camera Shake")]
        public static void RunMenu()
        {
            string result = RunChecks("Assets/Scenes/SampleScene.unity");
            Check(!result.Contains("hook=absent"), "Main Camera is missing CameraShake.");
            Debug.Log(result);
        }

        public static void RunBatch()
        {
            try
            {
                string path = "Assets/Scenes/SampleScene.unity";
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-shakeScene") path = args[i + 1];
                Debug.Log(RunChecks(path));
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static string RunChecks(string path)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                Camera camera = null;
                PlayerHealth health = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var candidate in root.GetComponentsInChildren<Camera>(true))
                        if (candidate.CompareTag("MainCamera")) camera = candidate;
                    if (health == null) health = root.GetComponentInChildren<PlayerHealth>(true);
                }
                Check(camera != null && health != null, "Scene needs camera and player health.");
                Call(health, "Awake");
                Vector3 origin = camera.transform.position;
                var shake = camera.GetComponent("CameraShake");
                if (shake == null)
                {
                    health.TakeHit();
                    Check(health.CurrentHearts == health.MaxHearts - 1, "Baseline damage changed.");
                    Check(camera.transform.position == origin, "Baseline camera moved.");
                    return "PASS hit: hearts=2; camera offset=(0,0,0); major explosion hook=absent";
                }
                Set(shake, "_health", health);
                Call(shake, "OnEnable");
                Action<float> step = dt => Call(shake, "Simulate", dt);
                Action clear = () => Call(shake, "Update");
                health.TakeHit();
                step(0.016f);
                float first = Vector3.Distance(camera.transform.position, origin);
                Check(first > 0f && first <= 0.15f, "Hit must cause bounded positional shake.");
                Check(camera.transform.position.z == origin.z && health.CurrentHearts == 2, "Damage/Z changed.");
                clear(); step(1f);
                Check(Vector3.Distance(camera.transform.position, origin) < 0.00001f, "Shake must expire exactly.");
                health.ResetHealth(); step(0.016f);
                Check(camera.transform.position == origin, "Healing must not shake.");
                Set(shake, "_heavyDamageThreshold", 2);
                health.TakeHit(); step(0.016f);
                Check(camera.transform.position == origin, "Below-threshold damage must not shake.");
                Call(shake, "TriggerMajorExplosion"); step(0.016f);
                Check(Vector3.Distance(camera.transform.position, origin) > 0f, "Explosion must shake.");
                clear(); step(0f);
                Check(camera.transform.position == origin, "Pause must remove offset.");
                step(0.016f);
                Call(shake, "OnDisable");
                Check(Vector3.Distance(camera.transform.position, origin) < 0.00001f, "Disable must restore pose.");
                health.TakeHit(); step(0.016f);
                Check(camera.transform.position == origin, "Disabled effect must unsubscribe.");
                Call(shake, "OnEnable");
                Set(shake, "_intensity", 0f);
                Call(shake, "TriggerMajorExplosion"); step(0.016f);
                Check(camera.transform.position == origin, "Zero intensity must not shake.");
                Set(shake, "_intensity", 0.12f);
                Call(shake, "TriggerMajorExplosion"); step(0.016f);
                var follow = camera.GetComponent<CameraFollow>();
                follow.ShiftX(-1000f); clear(); step(1f);
                Check(Vector3.Distance(camera.transform.position, origin + Vector3.left * 1000f) < 0.0001f,
                    "Rebase must preserve the clean camera pose.");
                origin = camera.transform.position;
                for (int i = 0; i < 1000; i++)
                {
                    clear(); Call(shake, "TriggerMajorExplosion"); step(0.016f);
                    Check(Vector3.Distance(camera.transform.position, origin) <= 0.15f, "Repeated events must be bounded.");
                }
                clear(); step(1f);
                Check(Vector3.Distance(camera.transform.position, origin) < 0.0001f, "Repeated shake must not drift.");
                Set(follow, "_target", health.transform);
                Set(follow, "_smoothTime", 0f);
                health.transform.position = origin + Vector3.left * 3f;
                Call(follow, "Start");
                origin = camera.transform.position;
                Quaternion rotation = camera.transform.rotation;
                float zoom = camera.orthographicSize;
                for (int i = 0; i < 100; i++)
                {
                    clear(); Call(follow, "LateUpdate");
                    Check(Vector3.Distance(camera.transform.position, origin) < 0.0001f, "Follow must never ingest shake offset.");
                    Call(shake, "TriggerMajorExplosion"); step(0.016f);
                }
                clear(); step(1f);
                Check(camera.transform.rotation == rotation && camera.orthographicSize == zoom, "Rotation/zoom changed.");
                Set(shake, "_duration", 1f);
                Set(shake, "_decayRate", 2f);
                Call(shake, "TriggerMajorExplosion"); step(0.5f);
                Check(Vector3.Distance(camera.transform.position, origin) > 0f &&
                    Vector3.Distance(camera.transform.position, origin) <= 0.0301f, "Decay envelope must follow tuning.");
                clear(); step(0.5f);
                Check(Vector3.Distance(camera.transform.position, origin) < 0.0001f, "Tuned duration must expire.");
                Set(shake, "_duration", 0f);
                Call(shake, "TriggerMajorExplosion"); step(0.016f);
                Check(camera.transform.position == origin, "Zero duration must disable shake.");
                Call(shake, "OnDisable");
                return "PASS hit: hearts=2; camera offset>0 and <=0.15; major explosion offset>0\n" +
                    "PASS expiry, healing, threshold, pause, disable/unsubscribe, zero intensity, rebase, 1000-event no-drift\n" +
                    "PASS follow ordering, unchanged rotation/zoom, tuned decay/duration, zero duration";
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
