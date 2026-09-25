using System;
using System.Collections.Generic;
using AmbedkarHeritage.Core;
using AmbedkarHeritage.VR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AmbedkarHeritage.EditorTools
{
    /// <summary>
    /// Lightweight regression gate for the Phase 1 foundation. Run manually via
    /// menu (Ambedkar Heritage -> Run Foundation Regression Checks) or in batch:
    ///   -executeMethod AmbedkarHeritage.EditorTools.FoundationRegressionChecks.RunAllFromCommandLine
    ///
    /// Intended to catch silent regressions in the data pipeline and scene wiring.
    /// </summary>
    public static class FoundationRegressionChecks
    {
        private const string SampleResourcePath = "Data/sample_archive";

        public static bool LastRunPassed { get; private set; } = true;

        [MenuItem("Ambedkar Heritage/Run Foundation Regression Checks")]
        public static void RunAll()
        {
            // Repair scenes first so the scene check has a stable basis to validate.
            FoundationRegistrar.EnsureCoreObjectsInScenes();

            int passed = 0;
            int failed = 0;

            Run("sample archive exists and parses", CheckSampleArchiveParses, ref passed, ref failed);
            Run("sample archive records are schema-valid", CheckSampleRecordsValid, ref passed, ref failed);
            Run("record type parsing", CheckRecordTypeParsing, ref passed, ref failed);
            Run("museum config applies to MuseumApp", CheckConfigApplies, ref passed, ref failed);
            Run("build settings contain MainMuseum", CheckBuildSettings, ref passed, ref failed);
            Run("build scenes contain core objects", CheckScenesContainCoreObjects, ref passed, ref failed);

            LastRunPassed = failed == 0;

            Debug.Log("[AmbedkarHeritage] Regression checks: " + passed + " passed, " + failed + " failed.");
            if (failed > 0)
            {
                Debug.LogError("[AmbedkarHeritage] Regression checks FAILED — do not proceed.");
            }
        }

        /// <summary>Batch entry point for CI-style runs.</summary>
        public static void RunAllFromCommandLine()
        {
            RunAll();
        }

        private static void Run(string name, Func<bool> check, ref int passed, ref int failed)
        {
            bool ok;
            try
            {
                ok = check();
            }
            catch (Exception exception)
            {
                ok = false;
                Debug.LogError("[AmbedkarHeritage] Regression check threw: " + exception);
            }

            if (ok)
            {
                passed++;
                Debug.Log("[AmbedkarHeritage] [PASS] " + name);
            }
            else
            {
                failed++;
                Debug.LogError("[AmbedkarHeritage] [FAIL] " + name);
            }
        }

        private static bool CheckSampleArchiveParses()
        {
            TextAsset asset = Resources.Load<TextAsset>(SampleResourcePath);
            if (asset == null)
            {
                Debug.LogError("[AmbedkarHeritage] Missing resource: " + SampleResourcePath);
                return false;
            }

            DemoArchiveData data = JsonUtility.FromJson<DemoArchiveData>(asset.text);
            if (data == null || data.config == null || data.records == null)
            {
                Debug.LogError("[AmbedkarHeritage] sample_archive.json produced no config or records.");
                return false;
            }

            Debug.Log("[AmbedkarHeritage] sample_archive.json OK: " +
                      data.records.Count + " record(s), name='" + data.config.appName + "'.");
            return true;
        }

        private static bool CheckSampleRecordsValid()
        {
            TextAsset asset = Resources.Load<TextAsset>(SampleResourcePath);
            DemoArchiveData data = JsonUtility.FromJson<DemoArchiveData>(asset.text);
            if (data == null || data.records == null || data.records.Count == 0)
            {
                return false;
            }

            HashSet<string> ids = new HashSet<string>();
            foreach (ArchiveRecord record in data.records)
            {
                if (string.IsNullOrEmpty(record.id))
                {
                    return false;
                }

                if (!ids.Add(record.id))
                {
                    Debug.LogError("[AmbedkarHeritage] Duplicate record id: " + record.id);
                    return false;
                }

                if (record.type == ArchiveRecordType.Unknown)
                {
                    Debug.LogError("[AmbedkarHeritage] Record " + record.id + " has unresolved type.");
                    return false;
                }
            }

            return true;
        }

        private static bool CheckRecordTypeParsing()
        {
            var cases = new Dictionary<ArchiveRecordType, string[]>
            {
                { ArchiveRecordType.Manuscript, new[] { "Manuscript", "manuscript" } },
                { ArchiveRecordType.Book, new[] { "Book" } },
                { ArchiveRecordType.Photograph, new[] { "Photograph", "photo" } },
                { ArchiveRecordType.Letter, new[] { "Letter" } },
                { ArchiveRecordType.Document, new[] { "Document", "scan" } },
                { ArchiveRecordType.Speech, new[] { "Speech" } },
                { ArchiveRecordType.Audio, new[] { "Audio" } },
                { ArchiveRecordType.Video, new[] { "Video" } },
                { ArchiveRecordType.Event, new[] { "Event", "timeline" } }
            };

            foreach (KeyValuePair<ArchiveRecordType, string[]> entry in cases)
            {
                foreach (string token in entry.Value)
                {
                    if (ArchiveRecord.ParseType(token) != entry.Key)
                    {
                        Debug.LogError("[AmbedkarHeritage] ParseType('" + token + "') != " + entry.Key);
                        return false;
                    }
                }
            }

            return ArchiveRecord.ParseType("not-a-real-type") == ArchiveRecordType.Unknown;
        }

        private static bool CheckConfigApplies()
        {
            // Capture current state to restore afterwards (checks must be side-effect free).
            string oldName = MuseumApp.AppName;
            string oldWelcome = MuseumApp.WelcomeMessage;
            string[] oldLanguages = MuseumApp.SupportedLanguages;

            var test = new MuseumConfig
            {
                appName = "REGRESSION TEST",
                welcomeMessage = "Regression message.",
                supportedLanguages = new List<string> { "en", "xx" }
            };

            MuseumApp.ApplyConfig(test);

            bool ok = MuseumApp.AppName == "REGRESSION TEST"
                      && MuseumApp.WelcomeMessage == "Regression message."
                      && MuseumApp.SupportedLanguages.Length == 2
                      && MuseumApp.SupportedLanguages[1] == "xx";

            // Restore.
            var restore = new MuseumConfig
            {
                appName = oldName,
                welcomeMessage = oldWelcome,
                supportedLanguages = new List<string>(oldLanguages)
            };
            MuseumApp.ApplyConfig(restore);

            return ok;
        }

        private static bool CheckBuildSettings()
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene != null && scene.enabled && scene.path == MainMuseumBuilder.ScenePath)
                {
                    return true;
                }
            }

            Debug.LogError("[AmbedkarHeritage] MainMuseum scene is missing from build settings.");
            return false;
        }

        private static bool CheckScenesContainCoreObjects()
        {
            bool allOk = true;
            Scene previous = SceneManager.GetActiveScene();

            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (buildScene == null || !buildScene.enabled)
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);

                bool hasBootstrap = FindComponentInScene<Bootstrap>(scene) != null;
                bool hasVrSession = FindComponentInScene<VrSession>(scene) != null;
                bool ok = hasBootstrap && hasVrSession;

                if (!ok)
                {
                    Debug.LogError("[AmbedkarHeritage] Scene " + buildScene.path +
                                   " missing Bootstrap=" + hasBootstrap + " VrSession=" + hasVrSession);
                    allOk = false;
                }

                EditorSceneManager.CloseScene(scene, true);
            }

            SceneManager.SetActiveScene(previous);
            return allOk;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid())
            {
                return null;
            }

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