using AmbedkarHeritage.Core;
using AmbedkarHeritage.VR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AmbedkarHeritage.EditorTools
{
    /// <summary>
    /// Ensures every build scene contains the two singleton core objects that the
    /// runtime relies on:
    ///   - Bootstrap  : loads the demo/live archive config and applies it to the scene.
    ///   - VrSession  : publishes headset/controller status and the current exhibit id.
    ///
    /// Safe to re-run: never duplicates. Used by MainMuseumBuilder when generating
    /// a fresh scene and via menu/batch to repair already-committed scenes.
    /// </summary>
    public static class FoundationRegistrar
    {
        public const string BootstrapObjectName = "Bootstrap";
        public const string VrSessionObjectName = "VrSession";

        [MenuItem("Ambedkar Heritage/Ensure Core Objects in Scenes")]
        public static void EnsureCoreObjectsInScenes()
        {
            int repaired = 0;
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            Scene previousActive = SceneManager.GetActiveScene();

            foreach (EditorBuildSettingsScene buildScene in buildScenes)
            {
                if (buildScene == null || !buildScene.enabled || string.IsNullOrEmpty(buildScene.path))
                {
                    continue;
                }

                if (previousActive.IsValid() && previousActive.path == buildScene.path)
                {
                    // Already the open/active scene; no need to reopen.
                    if (AddMissingCoreObjects(previousActive) > 0)
                    {
                        EditorSceneManager.SaveScene(previousActive);
                        repaired++;
                    }

                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);

                int added = AddMissingCoreObjects(scene);
                if (added > 0)
                {
                    EditorSceneManager.SaveScene(scene);
                    repaired++;
                }

                EditorSceneManager.CloseScene(scene, true);
            }

            if (previousActive.IsValid())
            {
                SceneManager.SetActiveScene(previousActive);
            }

            Debug.Log("[AmbedkarHeritage] FoundationRegistrar: checked " + buildScenes.Length +
                      " build scene(s), repaired " + repaired + ".");
        }

        /// <summary>Adds missing core objects to the active scene. Returns how many were added.</summary>
        public static int AddMissingCoreObjects()
        {
            return AddMissingCoreObjects(SceneManager.GetActiveScene());
        }

        /// <summary>Adds missing core objects to the given scene. Returns how many were added.</summary>
        public static int AddMissingCoreObjects(Scene scene)
        {
            if (!scene.IsValid())
            {
                Debug.LogError("[AmbedkarHeritage] FoundationRegistrar: scene is not valid.");
                return 0;
            }

            int added = 0;

            if (FindComponentInScene<Bootstrap>(scene) == null)
            {
                GameObject go = new GameObject(BootstrapObjectName);
                go.AddComponent<Bootstrap>();
                added++;
            }

            if (FindComponentInScene<VrSession>(scene) == null)
            {
                GameObject go = new GameObject(VrSessionObjectName);
                go.AddComponent<VrSession>();
                added++;
            }

            return added;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }
}