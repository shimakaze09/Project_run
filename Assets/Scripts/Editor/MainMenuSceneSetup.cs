#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using Run.UI;

namespace Run.EditorTools
{
    /// <summary>
    /// One-click assembly for the title screen's own scene, mirroring <see cref="RunnerSetup"/>'s
    /// "build/repair, then press Play" philosophy but for <c>MainMenu.unity</c> instead of the
    /// gameplay scene.
    /// </summary>
    /// <remarks>
    /// Kept as a genuinely separate scene (rather than an overlay inside the gameplay scene) so
    /// the title screen is its own page: nothing from the gameplay scene loads, runs, or shows
    /// through underneath it. Idempotent and non-destructive to whatever the Editor currently has
    /// open - the scene is built additively and closed again at the end, exactly the way
    /// <see cref="RunnerSetup"/> leaves the gameplay scene as the active one throughout.
    /// </remarks>
    public static class MainMenuSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Endless Runner/Build Main Menu Scene")]
        public static void BuildMainMenuScene()
        {
            var previousActiveScene = SceneManager.GetActiveScene();

            Scene menuScene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            EditorSceneManager.SetActiveScene(menuScene);
            try
            {
                ConfigureCamera(menuScene);
                EnsureMainMenu(menuScene);
                EnsureEventSystem(menuScene);
            }
            finally
            {
                EditorSceneManager.SetActiveScene(previousActiveScene);
            }

            EditorSceneManager.SaveScene(menuScene, ScenePath);
            EditorSceneManager.CloseScene(menuScene, true);

            RegisterBuildScenes();

            Debug.Log("[MainMenuSceneSetup] Main Menu scene ready. It will load first when the game starts.");
        }

        private static void ConfigureCamera(Scene menuScene)
        {
            var camera = FindInScene<Camera>(menuScene);
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
                SceneManager.MoveGameObjectToScene(cameraObject, menuScene);
                camera = cameraObject.GetComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // MainMenu's own UI canvas draws a full-screen background image over this (see
            // MainMenu.BuildBackground), so this color is only ever seen as a sliver before that
            // loads, or as its own fallback if the art is missing.
            camera.backgroundColor = new Color(0.04f, 0.09f, 0.16f);
            camera.transform.position = new Vector3(0f, 1f, -10f);
        }

        private static void EnsureMainMenu(Scene menuScene)
        {
            var menu = FindInScene<MainMenu>(menuScene);
            if (menu == null)
            {
                var menuObject = new GameObject("Main Menu");
                SceneManager.MoveGameObjectToScene(menuObject, menuScene);
                menuObject.AddComponent<MainMenu>();
            }
        }

        // UI buttons need an EventSystem in the scene to receive clicks; the project targets the
        // new Input System only, so StandaloneInputModule (which reads the legacy Input Manager)
        // would silently do nothing. Scoped to this scene's own objects rather than reusing
        // RunnerSetup's version, which searches every loaded scene and could otherwise pick up
        // the gameplay scene's EventSystem if it happens to be open additively at the same time.
        private static void EnsureEventSystem(Scene menuScene)
        {
            var eventSystem = FindInScene<EventSystem>(menuScene);
            GameObject eventSystemObject;
            if (eventSystem == null)
            {
                eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(eventSystemObject, menuScene);
            }
            else
            {
                eventSystemObject = eventSystem.gameObject;
            }

#if ENABLE_INPUT_SYSTEM
            var uiModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null)
            {
                uiModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            }

            if (uiModule.move.action == null || uiModule.move.action.bindings.Count == 0)
            {
                uiModule.AssignDefaultActions();
            }
#else
            if (eventSystemObject.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }

        /// <summary>Finds a component among just this scene's own root hierarchies, ignoring whatever else the Editor has open.</summary>
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>Puts MainMenu first in Build Settings so it's what the player sees on launch, then the gameplay scene.</summary>
        private static void RegisterBuildScenes()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(GameplayScenePath, true),
            };
            EditorBuildSettings.scenes = scenes;
        }
    }
}
#endif
