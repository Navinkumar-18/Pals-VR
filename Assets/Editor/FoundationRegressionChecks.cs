using System;
using System.Collections.Generic;
using AmbedkarHeritage.Core;
using AmbedkarHeritage.ExhibitRoom;
using AmbedkarHeritage.Interaction;
using AmbedkarHeritage.Networking;
using AmbedkarHeritage.UI;
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
            Run("extended archive fields parse", CheckExtendedFieldsParse, ref passed, ref failed);
            Run("minimal record loads safely", CheckMinimalRecordLoadsSafely, ref passed, ref failed);
            Run("archive room config parses", CheckArchiveRoomParses, ref passed, ref failed);
            Run("archive room stations resolve records", CheckArchiveRoomStationsValid, ref passed, ref failed);
            Run("category buckets are valid", CheckCategoryBuckets, ref passed, ref failed);
            Run("archive room builds into a scene", CheckArchiveRoomBuilds, ref passed, ref failed);
            Run("info panel formats metadata safely", CheckInfoPanelFormats, ref passed, ref failed);
            Run("document viewer plans pages safely", CheckDocumentViewerPlans, ref passed, ref failed);
            Run("phase e ocr fields parse", CheckOcrFieldsParse, ref passed, ref failed);
            Run("phase e viewer paginates long text", CheckOcrViewerPaginates, ref passed, ref failed);
            Run("scene contains archive room UI", CheckSceneContainsArchiveUI, ref passed, ref failed);
            Run("data provider architecture (local + api)", CheckDataProviderArchitecture, ref passed, ref failed);
            Run("api payload parses into archive records", CheckApiPayloadParses, ref passed, ref failed);
            Run("offline demo fallback engages safely", CheckApiOfflineFallback, ref passed, ref failed);
            Run("unity api integration (live)", CheckUnityApiIntegration, ref passed, ref failed);
            Run("phase e live ocr integration", CheckUnityOcrIntegration, ref passed, ref failed);

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

        /// <summary>
        /// Batch entry point for Phase C verification: regenerate the MainMuseum
        /// scene from data, then run the full regression suite against it.
        /// </summary>
        public static void BuildAndRunAllFromCommandLine()
        {
            MainMuseumBuilder.BuildMainMuseum();
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
            // Exercise the actual runtime path (loader normalizes records).
            DemoArchiveLoader loader = DemoArchiveLoader.Instance;
            loader.Load();

            if (loader.Records == null || loader.Records.Count < 5)
            {
                Debug.LogError("[AmbedkarHeritage] Loader returned too few records: " +
                               (loader.Records == null ? 0 : loader.Records.Count));
                return false;
            }

            HashSet<string> ids = new HashSet<string>();
            foreach (ArchiveRecord record in loader.Records)
            {
                if (string.IsNullOrEmpty(record.id))
                {
                    Debug.LogError("[AmbedkarHeritage] Record without id.");
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

                // Missing optional fields must normalize to empty (never null).
                if (record.tags == null || record.relatedIds == null)
                {
                    Debug.LogError("[AmbedkarHeritage] Record " + record.id +
                                   " has null collections after normalization.");
                    return false;
                }
            }

            return true;
        }

        private static bool CheckExtendedFieldsParse()
        {
            DemoArchiveLoader loader = DemoArchiveLoader.Instance;
            loader.Load();

            ArchiveRecord book = loader.Find("AMB-SAM-006");
            ArchiveRecord speech = loader.Find("AMB-SAM-004");
            ArchiveRecord timeEvent = loader.Find("AMB-SAM-005");
            ArchiveRecord photo = loader.Find("AMB-SAM-003");

            if (book == null || speech == null || timeEvent == null || photo == null)
            {
                Debug.LogError("[AmbedkarHeritage] Extended-field records missing from demo archive.");
                return false;
            }

            bool ok = true;
            ok &= book.type == ArchiveRecordType.Book;
            ok &= !string.IsNullOrEmpty(book.image);
            ok &= !string.IsNullOrEmpty(book.document);
            ok &= !string.IsNullOrEmpty(book.ocrText);
            ok &= book.tags != null && book.tags.Count >= 2;
            ok &= !string.IsNullOrEmpty(book.citation);
            ok &= book.verified == false;

            ok &= !string.IsNullOrEmpty(speech.audio);
            ok &= !string.IsNullOrEmpty(speech.ocrText);

            ok &= timeEvent.eventId == "TL-1891";
            ok &= timeEvent.tags != null && timeEvent.tags.Count >= 2;

            ok &= !string.IsNullOrEmpty(photo.image);
            ok &= photo.audio == null && photo.video == null; // omitted fields stay null

            if (!ok)
            {
                Debug.LogError("[AmbedkarHeritage] Extended archive fields did not deserialize as expected.");
            }

            return ok;
        }

        private static bool CheckMinimalRecordLoadsSafely()
        {
            const string minimalJson =
                "{\"id\":\"MIN-001\",\"title\":\"Minimal\",\"description\":\"No optional fields set.\"}";

            try
            {
                ArchiveRecord record = JsonUtility.FromJson<ArchiveRecord>(minimalJson);
                if (record == null)
                {
                    Debug.LogError("[AmbedkarHeritage] Minimal JSON produced a null record.");
                    return false;
                }

                // Optional fields must never be fatal or malformed.
                if (record.relatedIds == null || record.tags == null)
                {
                    Debug.LogError("[AmbedkarHeritage] Minimal record has null collections.");
                    return false;
                }

                if (!string.IsNullOrEmpty(record.image)
                    || !string.IsNullOrEmpty(record.audio)
                    || !string.IsNullOrEmpty(record.video)
                    || !string.IsNullOrEmpty(record.document)
                    || !string.IsNullOrEmpty(record.ocrText)
                    || !string.IsNullOrEmpty(record.eventId)
                    || record.verified
                    || !string.IsNullOrEmpty(record.citation))
                {
                    Debug.LogError("[AmbedkarHeritage] Minimal record got unexpected populated fields.");
                    return false;
                }

                record.EnsureSafeDefaults(); // must be idempotent and safe
                return record.tags != null && record.relatedIds != null;
            }
            catch (Exception exception)
            {
                Debug.LogError("[AmbedkarHeritage] Minimal JSON threw: " + exception.Message);
                return false;
            }
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

        // ------------------------------------------------------------------
        // Phase C checks
        // ------------------------------------------------------------------

        private static bool CheckArchiveRoomParses()
        {
            ArchiveRoomConfig config = ArchiveRoomDataLoader.Load();
            if (config == null || config.stations == null || config.stations.Count < 6)
            {
                Debug.LogError("[AmbedkarHeritage] Archive room config missing or has too few stations: " +
                               (config == null || config.stations == null ? 0 : config.stations.Count));
                return false;
            }

            foreach (ExhibitStation station in config.stations)
            {
                if (string.IsNullOrEmpty(station.exhibitId))
                {
                    Debug.LogError("[AmbedkarHeritage] Archive room contains a station without exhibitId.");
                    return false;
                }

                Vector3 pos = station.Position;
                if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z)
                    || float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z))
                {
                    Debug.LogError("[AmbedkarHeritage] Station " + station.exhibitId + " has non-finite position.");
                    return false;
                }

                if (station.scale <= 0f)
                {
                    Debug.LogError("[AmbedkarHeritage] Station " + station.exhibitId + " has invalid scale.");
                    return false;
                }
            }

            return true;
        }

        private static bool CheckArchiveRoomStationsValid()
        {
            ArchiveRoomConfig config = ArchiveRoomDataLoader.Load();
            DemoArchiveLoader loader = DemoArchiveLoader.Instance;
            loader.Load();

            HashSet<string> distinct = new HashSet<string>();
            foreach (ExhibitStation station in config.stations)
            {
                if (loader.Find(station.exhibitId) == null)
                {
                    Debug.LogError("[AmbedkarHeritage] Station references unknown record: " + station.exhibitId);
                    return false;
                }

                distinct.Add(station.exhibitId);
            }

            if (distinct.Count < 6)
            {
                Debug.LogError("[AmbedkarHeritage] Archive room needs at least 6 distinct exhibits, has " + distinct.Count);
                return false;
            }

            // The demo video record must exist and be placed (video bucket coverage).
            if (loader.Find("AMB-SAM-008") == null || !distinct.Contains("AMB-SAM-008"))
            {
                Debug.LogError("[AmbedkarHeritage] Demo video record/station missing.");
                return false;
            }

            return true;
        }

        private static bool CheckCategoryBuckets()
        {
            if (CategoryMap.FilterOrder.Length != 7)
            {
                Debug.LogError("[AmbedkarHeritage] Filter bar must expose exactly 7 categories.");
                return false;
            }

            string expectedAll = "ALL";
            if (CategoryMap.Label(ArchiveCategory.All) != "ALL"
                || CategoryMap.Label(ArchiveCategory.Manuscripts) != "MANUSCRIPTS"
                || CategoryMap.Label(ArchiveCategory.Books) != "BOOKS"
                || CategoryMap.Label(ArchiveCategory.Documents) != "DOCUMENTS"
                || CategoryMap.Label(ArchiveCategory.Photographs) != "PHOTOGRAPHS"
                || CategoryMap.Label(ArchiveCategory.Speeches) != "SPEECHES"
                || CategoryMap.Label(ArchiveCategory.Videos) != "VIDEOS")
            {
                Debug.LogError("[AmbedkarHeritage] Category labels do not match the required VR filter set.");
                return false;
            }

            if (CategoryMap.For(ArchiveRecordType.Manuscript) != ArchiveCategory.Manuscripts
                || CategoryMap.For(ArchiveRecordType.Book) != ArchiveCategory.Books
                || CategoryMap.For(ArchiveRecordType.Document) != ArchiveCategory.Documents
                || CategoryMap.For(ArchiveRecordType.Letter) != ArchiveCategory.Documents
                || CategoryMap.For(ArchiveRecordType.Photograph) != ArchiveCategory.Photographs
                || CategoryMap.For(ArchiveRecordType.Speech) != ArchiveCategory.Speeches
                || CategoryMap.For(ArchiveRecordType.Audio) != ArchiveCategory.Speeches
                || CategoryMap.For(ArchiveRecordType.Video) != ArchiveCategory.Videos
                || CategoryMap.For(ArchiveRecordType.Event) != ArchiveCategory.Uncategorized)
            {
                Debug.LogError("[AmbedkarHeritage] Record type -> category mapping is wrong.");
                return false;
            }

            return true;
        }

        private static bool CheckArchiveRoomBuilds()
        {
            Scene previous = SceneManager.GetActiveScene();
            bool previousUntitled = !previous.IsValid() || string.IsNullOrEmpty(previous.path);

            // Unity forbids creating a NEW scene additively while the active
            // scene is an unsaved untitled scene (batch mode starts this way).
            Scene temp;
            if (previousUntitled)
            {
                temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(temp);
            }

            bool ok = true;

            try
            {
                GameObject root = new GameObject("TestRoomRoot");
                MuseumRoomBuilder.BuildMuseumRoom(root, ArchiveRoomDataLoader.Load());

                ExhibitController[] exhibits = root.GetComponentsInChildren<ExhibitController>(true);
                if (exhibits.Length < 6)
                {
                    Debug.LogError("[AmbedkarHeritage] Room build produced " + exhibits.Length + " exhibit(s).");
                    ok = false;
                }

                HashSet<string> ids = new HashSet<string>();
                foreach (ExhibitController exhibit in exhibits)
                {
                    exhibit.ResolveRecord();
                    if (exhibit.Record == null)
                    {
                        Debug.LogError("[AmbedkarHeritage] Station did not resolve its record: " + exhibit.RecordBoundId);
                        ok = false;
                    }

                    ids.Add(exhibit.RecordBoundId);
                }

                if (ids.Count < 6)
                {
                    Debug.LogError("[AmbedkarHeritage] Room build stations are not distinct.");
                    ok = false;
                }

                ArchiveInfoPanel panel = root.GetComponentInChildren<ArchiveInfoPanel>(true);
                if (panel == null || panel.ActionButtonCount != 6 || panel.CloseButton == null)
                {
                    Debug.LogError("[AmbedkarHeritage] Archive info panel missing or not fully wired.");
                    ok = false;
                }

                DocumentViewer viewer = root.GetComponentInChildren<DocumentViewer>(true);
                if (viewer == null || !viewer.HasZoom || !viewer.IsWired)
                {
                    Debug.LogError("[AmbedkarHeritage] Document viewer missing or not fully wired.");
                    ok = false;
                }

                ArchiveCategoryFilter filter = root.GetComponentInChildren<ArchiveCategoryFilter>(true);
                if (filter == null || filter.ButtonCount != 7)
                {
                    Debug.LogError("[AmbedkarHeritage] Category filter missing or not fully wired.");
                    ok = false;
                }
            }
            catch (Exception exception)
            {
                ok = false;
                Debug.LogError("[AmbedkarHeritage] Room build threw: " + exception);
            }
            finally
            {
                EditorSceneManager.CloseScene(temp, true);
                if (!previousUntitled && previous.IsValid())
                {
                    SceneManager.SetActiveScene(previous);
                }
            }

            return ok;
        }

        private static bool CheckInfoPanelFormats()
        {
            try
            {
                ArchiveRecord sample = DemoArchiveLoader.Instance.Find("AMB-SAM-002");
                if (sample == null)
                {
                    return false;
                }

                string meta = ArchiveInfoPanel.FormatMeta(sample);
                if (string.IsNullOrEmpty(meta) || !meta.Contains("Date: 1916") || !meta.Contains("Category:"))
                {
                    Debug.LogError("[AmbedkarHeritage] Info panel meta format wrong: " + meta);
                    return false;
                }

                string source = ArchiveInfoPanel.FormatSource(sample);
                if (string.IsNullOrEmpty(source) || !source.Contains("SAMPLE/DEMO DATA")
                    || !source.Contains("Document ID: AMB-SAM-002"))
                {
                    Debug.LogError("[AmbedkarHeritage] Info panel source format wrong: " + source);
                    return false;
                }

                // Null-safe: a sparse record must not throw and must degrade to "n/a".
                var empty = new ArchiveRecord { isSampleData = true };
                string emptyMeta = ArchiveInfoPanel.FormatMeta(empty);
                string emptySource = ArchiveInfoPanel.FormatSource(empty);
                if (string.IsNullOrEmpty(emptyMeta) || !emptyMeta.Contains("n/a") || string.IsNullOrEmpty(emptySource))
                {
                    Debug.LogError("[AmbedkarHeritage] Info panel formats are not null-safe.");
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[AmbedkarHeritage] Info panel format check threw: " + exception.Message);
                return false;
            }
        }

        private static bool CheckDocumentViewerPlans()
        {
            try
            {
                DemoArchiveLoader loader = DemoArchiveLoader.Instance;
                loader.Load();

                ArchiveRecord manuscript = loader.Find("AMB-SAM-001");
                List<DocumentViewer.ViewerPage> pages = DocumentViewer.PlanPages(manuscript);
                if (pages.Count < manuscript.pages.Count + 1)
                {
                    Debug.LogError("[AmbedkarHeritage] Multi-page plan ignored record.pages.");
                    return false;
                }

                if (pages[pages.Count - 1].kind != DocumentViewer.ViewerPage.Kind.Text
                    || string.IsNullOrEmpty(pages[pages.Count - 1].text))
                {
                    Debug.LogError("[AmbedkarHeritage] Page plan lacks a trailing text page.");
                    return false;
                }

                ArchiveRecord photo = loader.Find("AMB-SAM-003");
                List<DocumentViewer.ViewerPage> photoPages = DocumentViewer.PlanPages(photo);
                if (photoPages.Count < 2)
                {
                    Debug.LogError("[AmbedkarHeritage] Photo with image ref should plan image + text pages.");
                    return false;
                }

                ArchiveRecord video = loader.Find("AMB-SAM-008");
                List<DocumentViewer.ViewerPage> videoPages = DocumentViewer.PlanPages(video);
                if (videoPages.Count < 1 || videoPages[0].kind != DocumentViewer.ViewerPage.Kind.Text
                    || !videoPages[0].text.Contains("video"))
                {
                    Debug.LogError("[AmbedkarHeritage] Video record should plan an honest text page.");
                    return false;
                }

                List<DocumentViewer.ViewerPage> nullPlan = DocumentViewer.PlanPages(null);
                return nullPlan.Count == 1;
            }
            catch (Exception exception)
            {
                Debug.LogError("[AmbedkarHeritage] Viewer plan check threw: " + exception.Message);
                return false;
            }
        }

        private static bool CheckSceneContainsArchiveUI()
        {
            EditorBuildSettingsScene buildScene = null;
            foreach (EditorBuildSettingsScene candidate in EditorBuildSettings.scenes)
            {
                if (candidate != null && candidate.enabled)
                {
                    buildScene = candidate;
                    break;
                }
            }

            if (buildScene == null)
            {
                Debug.LogError("[AmbedkarHeritage] No build scene registered.");
                return false;
            }

            Scene previouslyActive = SceneManager.GetActiveScene();
            bool openedHere = false;
            Scene scene;

            if (previouslyActive.IsValid() && previouslyActive.path == buildScene.path)
            {
                scene = previouslyActive;
            }
            else
            {
                scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                openedHere = true;
            }

            bool ok = true;

            GameObject room = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                room = FindChildRecursive(root.transform, MuseumRoomBuilder.RoomRootName);
                if (room != null)
                {
                    break;
                }
            }

            if (room == null)
            {
                Debug.LogError("[AmbedkarHeritage] ArchiveRoom missing from scene.");
                ok = false;
            }
            else
            {
                if (room.GetComponentInChildren<ExhibitController>(true) == null)
                {
                    Debug.LogError("[AmbedkarHeritage] ArchiveRoom has no exhibits.");
                    ok = false;
                }

                if (room.GetComponentInChildren<ArchiveInfoPanel>(true) == null)
                {
                    Debug.LogError("[AmbedkarHeritage] ArchiveRoom lacks the archive info panel.");
                    ok = false;
                }

                if (room.GetComponentInChildren<DocumentViewer>(true) == null)
                {
                    Debug.LogError("[AmbedkarHeritage] ArchiveRoom lacks the document viewer.");
                    ok = false;
                }

                if (room.GetComponentInChildren<ArchiveCategoryFilter>(true) == null)
                {
                    Debug.LogError("[AmbedkarHeritage] ArchiveRoom lacks the category filter.");
                    ok = false;
                }
            }

            if (openedHere)
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previouslyActive.IsValid())
                {
                    SceneManager.SetActiveScene(previouslyActive);
                }
            }

            return ok;
        }

        // ------------------------------------------------------------------
        // Phase D: backend integration checks
        // ------------------------------------------------------------------

        private static bool CheckDataProviderArchitecture()
        {
            // Local provider wraps the demo dataset.
            LocalArchiveDataProvider local = new LocalArchiveDataProvider();
            if (local.ProviderName != "local-demo")
            {
                Debug.LogError("[AmbedkarHeritage] Local provider name mismatch.");
                return false;
            }

            if (!ArchiveService.Local.IsReady)
            {
                ArchiveService.Local.Load();
            }

            if (!ArchiveService.Local.IsReady || ArchiveService.Local.AllRecords.Count < 8
                || ArchiveService.Local.Find("AMB-SAM-008") == null)
            {
                Debug.LogError("[AmbedkarHeritage] Local demo provider did not load the sample dataset.");
                return false;
            }

            // API provider exists, starts unloaded and is null-safe.
            ApiArchiveDataProvider api = new ApiArchiveDataProvider();
            if (api.ProviderName != "api" || api.IsReady || api.Find("AMB-SAM-001") != null)
            {
                Debug.LogError("[AmbedkarHeritage] API provider default state wrong.");
                return false;
            }

            // ArchiveService selects the local provider in demo mode.
            bool originalDemo = MuseumApp.IsDemoMode;
            try
            {
                MuseumApp.IsDemoMode = true;
                ArchiveService.ForceReinitialize();
                if (!(ArchiveService.Current is LocalArchiveDataProvider) || ArchiveService.IsOfflineFallback)
                {
                    Debug.LogError("[AmbedkarHeritage] Demo mode did not activate the local provider.");
                    return false;
                }

                if (ArchiveService.Find("AMB-SAM-001") == null
                    || ArchiveService.DataModeLabel != "DEMO")
                {
                    Debug.LogError("[AmbedkarHeritage] Demo-mode data lookup failed.");
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[AmbedkarHeritage] Provider architecture check threw: " + exception.Message);
                return false;
            }
            finally
            {
                MuseumApp.IsDemoMode = originalDemo;
                ArchiveService.ForceReinitialize();
            }
        }

        private static bool CheckApiPayloadParses()
        {
            // Canned FastAPI /api/documents payload (camelCase, numeric type).
            const string payload =
                "{\"items\":[" +
                "{\"id\":\"API-DOC-1\",\"type\":1,\"title\":\"API Manuscript\",\"description\":\"from backend\"," +
                "\"date\":\"1916\",\"category\":\"Manuscripts\",\"source\":\"api\",\"language\":\"en\"," +
                "\"isSampleData\":true,\"verified\":true,\"status\":\"PUBLISHED\",\"citation\":\"api\",\"tags\":[\"ms\"]," +
                "\"relatedIds\":[\"API-DOC-2\"],\"image\":\"images/api-doc-1.jpg\",\"pages\":[\"images/api-doc-1-p1.jpg\"]," +
                "\"ocrText\":\"sample ocr\"}," +
                "{\"id\":\"API-DOC-2\",\"type\":5,\"title\":\"API Document\",\"description\":\"d\",\"date\":\"\",\"category\":\"\",\"source\":\"\",\"language\":\"en\",\"isSampleData\":false,\"verified\":false,\"status\":\"PUBLISHED\",\"tags\":[],\"relatedIds\":[]}" +
                "],\"count\":2}";

            try
            {
                ApiDocumentListDto dto = JsonUtility.FromJson<ApiDocumentListDto>(payload);
                if (dto == null || dto.items == null || dto.items.Count != 2 || dto.count != 2)
                {
                    Debug.LogError("[AmbedkarHeritage] API list wrapper failed to parse.");
                    return false;
                }

                ArchiveRecord first = dto.items[0];
                if (first.type != ArchiveRecordType.Manuscript || !first.isSampleData || !first.verified)
                {
                    Debug.LogError("[AmbedkarHeritage] Numeric type / flags mapping failed.");
                    return false;
                }

                if (first.relatedIds == null || first.relatedIds.Count != 1 || first.relatedIds[0] != "API-DOC-2"
                    || first.pages == null || first.pages.Count != 1 || string.IsNullOrEmpty(first.ocrText)
                    || first.tags == null || first.tags.Count != 1)
                {
                    Debug.LogError("[AmbedkarHeritage] Extended fields did not parse from API payload.");
                    return false;
                }

                if (dto.items[1].type != ArchiveRecordType.Document)
                {
                    Debug.LogError("[AmbedkarHeritage] Second record type mapping failed.");
                    return false;
                }

                first.EnsureSafeDefaults(); // must not throw
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[AmbedkarHeritage] API payload check threw: " + exception.Message);
                return false;
            }
        }

        private static bool CheckApiOfflineFallback()
        {
            bool originalDemo = MuseumApp.IsDemoMode;
            string originalBase = MuseumApp.ApiBaseUrl;
            try
            {
                // API mode against an unroutable endpoint.
                MuseumApp.IsDemoMode = false;
                MuseumApp.ApiBaseUrl = "http://127.0.0.1:1/api";
                string error;
                bool online = ArchiveService.ProbeOnlineSync(out error, 5f);
                if (online)
                {
                    Debug.LogError("[AmbedkarHeritage] Offline probe unexpectedly connected to port 1.");
                    return false;
                }

                if (!ArchiveService.IsOfflineFallback || ArchiveService.Current == null)
                {
                    Debug.LogError("[AmbedkarHeritage] Offline fallback was not engaged after failed probe: " + error);
                    return false;
                }

                ArchiveRecord sample = ArchiveService.Find("AMB-SAM-001");
                if (sample == null || !ArchiveService.DataModeLabel.Contains("OFFLINE"))
                {
                    Debug.LogError("[AmbedkarHeritage] Offline demo data unavailable in fallback mode.");
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[AmbedkarHeritage] Offline fallback check threw: " + exception.Message);
                return false;
            }
            finally
            {
                MuseumApp.IsDemoMode = originalDemo;
                MuseumApp.ApiBaseUrl = originalBase;
                ArchiveService.ForceReinitialize();
            }
        }

        private static bool CheckUnityApiIntegration()
        {
            // Live end-to-end: FastAPI -> PostgreSQL/SQLite -> JSON -> Unity.
            // Env var set by CI / the Phase D run script; otherwise skipped.
            string url = System.Environment.GetEnvironmentVariable("AMBEKAR_API_URL");
            if (string.IsNullOrEmpty(url))
            {
                Debug.Log("[AmbedkarHeritage] [INFO] AMBEKAR_API_URL not set — live API check skipped (offline path covered above).");
                return true;
            }

            bool originalDemo = MuseumApp.IsDemoMode;
            string originalBase = MuseumApp.ApiBaseUrl;
            try
            {
                MuseumApp.IsDemoMode = false;
                MuseumApp.ApiBaseUrl = url.TrimEnd('/');
                string error;
                if (!ArchiveService.ProbeOnlineSync(out error, 30f))
                {
                    Debug.LogError("[AmbedkarHeritage] Live API unreachable: " + error);
                    return false;
                }

                ArchiveRecord seed = ArchiveService.Find("AMB-SAM-001");
                if (seed == null || string.IsNullOrEmpty(seed.title))
                {
                    Debug.LogError("[AmbedkarHeritage] Seeded record AMB-SAM-001 missing from API.");
                    return false;
                }

                ArchiveRecord doc = ArchiveService.Find("AMB-SAM-002");
                if (doc == null || doc.type != ArchiveRecordType.Document || doc.date != "1916")
                {
                    Debug.LogError("[AmbedkarHeritage] API record mapping mismatch for AMB-SAM-002.");
                    return false;
                }

                if (seed.relatedIds == null || seed.relatedIds.Count == 0
                    || ArchiveService.Find(seed.relatedIds[0]) == null)
                {
                    Debug.LogError("[AmbedkarHeritage] relatedIds from the API do not resolve locally.");
                    return false;
                }

                Debug.Log("[AmbedkarHeritage] Live API integration OK: " + ArchiveService.All.Count
                          + " record(s) served by the backend.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[AmbedkarHeritage] Live API integration check threw: " + exception.Message);
                return false;
            }
            finally
            {
                MuseumApp.IsDemoMode = originalDemo;
                MuseumApp.ApiBaseUrl = originalBase;
                ArchiveService.ForceReinitialize();
            }
        }

        private static bool CheckOcrFieldsParse()
        {
            // Phase E ArchiveRecord fields must deserialize from API-style JSON.
            const string json = "{\"id\":\"OCR-TEST-001\",\"type\":5,\"title\":\"OCR field test\","
                + "\"ocrText\":\"Extracted page text\",\"ocrStatus\":\"COMPLETED\","
                + "\"ocrLanguage\":\"en\",\"ocrPages\":3}";
            try
            {
                ArchiveRecord record = JsonUtility.FromJson<ArchiveRecord>(json);
                if (record == null)
                {
                    Debug.LogError("[AmbedkarHeritage] OCR field payload did not parse.");
                    return false;
                }

                record.EnsureSafeDefaults();
                bool ok = record.ocrStatus == "COMPLETED"
                          && record.ocrLanguage == "en"
                          && record.ocrPages == 3
                          && record.ocrText == "Extracted page text";
                if (!ok)
                {
                    Debug.LogError("[AmbedkarHeritage] OCR fields deserialized to unexpected values.");
                }

                return ok;
            }
            catch (Exception exception)
            {
                Debug.LogError("[AmbedkarHeritage] OCR field parse check threw: " + exception.Message);
                return false;
            }
        }

        private static bool CheckOcrViewerPaginates()
        {
            try
            {
                // Very long OCR transcript -> multiple, marked text pages.
                System.Text.StringBuilder longText = new System.Text.StringBuilder();
                for (int i = 0; i < DocumentViewer.TextPageChars * 4; i++)
                {
                    longText.Append((char)('A' + (i % 26)));
                }

                ArchiveRecord longDoc = new ArchiveRecord
                {
                    id = "OCR-LONG-001",
                    type = ArchiveRecordType.Document,
                    title = "Long OCR document",
                    ocrText = longText.ToString()
                };
                List<DocumentViewer.ViewerPage> pages = DocumentViewer.PlanPages(longDoc);
                int textPages = 0;
                foreach (DocumentViewer.ViewerPage page in pages)
                {
                    if (page.kind == DocumentViewer.ViewerPage.Kind.Text)
                    {
                        textPages++;
                    }
                }

                if (textPages < 4)
                {
                    Debug.LogError("[AmbedkarHeritage] Long OCR text was not paginated into multiple text pages.");
                    return false;
                }

                // Every chunk body (excluding the "OCR TEXT - PART n / m" header)
                // must stay within the readability cap, and every sub-page must
                // carry a PART marker so the reader knows where they are.
                for (int i = 0; i < pages.Count; i++)
                {
                    // Strip the "OCR TEXT - PART n / m" header. The header is
                    // joined with Environment.NewLine (\r\n on Windows), so skip
                    // any run of \r/\n to reach the chunk body.
                    string body = pages[i].text ?? "";
                    int nl = body.IndexOf('\n');
                    if (nl >= 0)
                    {
                        int start = nl;
                        while (start < body.Length && (body[start] == '\n' || body[start] == '\r'))
                        {
                            start++;
                        }

                        body = body.Substring(start);
                    }

                    if (body.Length > DocumentViewer.TextPageChars)
                    {
                        Debug.LogError("[AmbedkarHeritage] OCR text page exceeds the readability cap (" + body.Length + ").");
                        return false;
                    }

                    if (!pages[i].text.Contains("PART " + (i + 1) + " /"))
                    {
                        Debug.LogError("[AmbedkarHeritage] Paginated OCR pages lack PART markers.");
                        return false;
                    }
                }

                // Short transcript -> exactly ONE trailing text page (backward compatible).
                ArchiveRecord shortDoc = new ArchiveRecord
                {
                    id = "OCR-SHORT-001",
                    type = ArchiveRecordType.Document,
                    title = "Short",
                    ocrText = "Short OCR body."
                };
                List<DocumentViewer.ViewerPage> shortPages = DocumentViewer.PlanPages(shortDoc);
                if (shortPages.Count != 1
                    || shortPages[0].kind != DocumentViewer.ViewerPage.Kind.Text
                    || !shortPages[0].text.Contains("Short OCR body."))
                {
                    Debug.LogError("[AmbedkarHeritage] Short OCR text should stay on a single text page.");
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[AmbedkarHeritage] Phase E viewer pagination check threw: " + exception.Message);
                return false;
            }
        }

        private static bool CheckUnityOcrIntegration()
        {
            // Live end-to-end: FastAPI OCR endpoints -> JsonUtility DTOs.
            // Needs a PUBLISHED, OCR-COMPLETED document on the backend; without
            // one (or without AMBEKAR_API_URL set) this check is skipped.
            string url = System.Environment.GetEnvironmentVariable("AMBEKAR_API_URL");
            if (string.IsNullOrEmpty(url))
            {
                Debug.Log("[AmbedkarHeritage] [INFO] AMBEKAR_API_URL not set — live OCR check skipped.");
                return true;
            }

            bool originalDemo = MuseumApp.IsDemoMode;
            string originalBase = MuseumApp.ApiBaseUrl;
            try
            {
                MuseumApp.IsDemoMode = false;
                MuseumApp.ApiBaseUrl = url.TrimEnd('/');
                string error;
                if (!ArchiveService.ProbeOnlineSync(out error, 30f))
                {
                    Debug.LogError("[AmbedkarHeritage] Live API unreachable for OCR check: " + error);
                    return false;
                }

                ArchiveRecord ocrDoc = null;
                foreach (ArchiveRecord record in ArchiveService.All)
                {
                    if (record.ocrStatus == "COMPLETED" && record.ocrPages > 0)
                    {
                        ocrDoc = record;
                        break;
                    }
                }

                if (ocrDoc == null)
                {
                    Debug.Log("[AmbedkarHeritage] [INFO] No OCR-COMPLETED document on the live backend — OCR e2e skipped (schema checks above).");
                    return true;
                }

                ApiOcrStatusDto status;
                if (!ArchiveApiClient.TryGetOcrStatusSyncEditor(MuseumApp.ApiBaseUrl, ocrDoc.id, out status, out error))
                {
                    Debug.LogError("[AmbedkarHeritage] OCR status fetch failed for " + ocrDoc.id + ": " + error);
                    return false;
                }

                if (status.status != "COMPLETED" || status.pages <= 0
                    || status.pageSummaries == null || status.pageSummaries.Count != status.pages
                    || status.pageSummaries[0].pageNumber != 1)
                {
                    Debug.LogError("[AmbedkarHeritage] OCR status mismatched expectations for " + ocrDoc.id
                        + ": status=" + status.status + " pages=" + status.pages
                        + " summaries=" + (status.pageSummaries == null ? 0 : status.pageSummaries.Count));
                    return false;
                }

                ApiOcrPageDto page;
                if (!ArchiveApiClient.TryGetOcrPageSyncEditor(MuseumApp.ApiBaseUrl, ocrDoc.id, 1, out page, out error))
                {
                    Debug.LogError("[AmbedkarHeritage] OCR page 1 fetch failed for " + ocrDoc.id + ": " + error);
                    return false;
                }

                if (page.pageNumber != 1 || string.IsNullOrEmpty(page.extractedText))
                {
                    Debug.LogError("[AmbedkarHeritage] OCR page 1 payload invalid for " + ocrDoc.id);
                    return false;
                }

                string previewText = page.extractedText.Replace('\n', ' ').Trim();
                if (previewText.Length > 60)
                {
                    previewText = previewText.Substring(0, 60);
                }

                Debug.Log("[AmbedkarHeritage] Live OCR e2e OK: " + ocrDoc.id + " (" + status.pages
                          + " page(s), page 1: \"" + previewText + "...\")");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[AmbedkarHeritage] Live OCR integration check threw: " + exception.Message);
                return false;
            }
            finally
            {
                MuseumApp.IsDemoMode = originalDemo;
                MuseumApp.ApiBaseUrl = originalBase;
                ArchiveService.ForceReinitialize();
            }
        }

        private static GameObject FindChildRecursive(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root.gameObject;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject found = FindChildRecursive(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}