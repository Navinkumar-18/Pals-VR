using System.IO;
using AmbedkarHeritage.Core;
using AmbedkarHeritage.Interaction;
using AmbedkarHeritage.UI;
using AmbedkarHeritage.VR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AmbedkarHeritage.EditorTools
{
    public static class MainMuseumBuilder
    {
        private const string RigPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        public const string ScenePath = "Assets/Scenes/MainMuseum/MainMuseum.unity";
        private const string ExhibitPrefabPath = "Assets/Prefabs/Exhibits/Exhibit.prefab";
        private const string MaterialsDir = "Assets/Materials";

        private static readonly string[] ExhibitIds = { "AMB-SAM-001", "AMB-SAM-002", "AMB-SAM-003" };

        [MenuItem("Ambedkar Heritage/Build MainMuseum Scene")]
        public static void BuildMainMuseum()
        {
            EnsureFolders();
            EnsureMaterials();
            DemoArchiveLoader.Instance.Load();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject world = CreateWorld();
            CreateLighting();
            CreateOvrRig();
            CreateEntranceSign();
            CreateExhibitPrefab();
            PlaceExhibits(world);
            CreateGrabBall();
            FoundationRegistrar.AddMissingCoreObjects();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AmbedkarHeritage] MainMuseum built: " + ScenePath);
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets/Scenes", "MainMuseum");
            CreateFolder("Assets/Prefabs", "Exhibits");
            CreateFolder("Assets", "Materials");
        }

        private static void CreateFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void EnsureMaterials()
        {
            EnsureMaterial("Floor.mat", new Color(0.16f, 0.16f, 0.19f));
            EnsureMaterial("Wall.mat", new Color(0.28f, 0.27f, 0.32f));
            EnsureMaterial("Accent.mat", new Color(0.55f, 0.35f, 0.12f));
            EnsureMaterial("Exhibit.mat", new Color(0.20f, 0.42f, 0.55f));
        }

        private static void EnsureMaterial(string file, Color color)
        {
            string path = MaterialsDir + "/" + file;
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                return;
            }

            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
            }

            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
        }

        private static Material Mat(string file)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/" + file);
        }

        private static GameObject CreateWorld()
        {
            GameObject world = new GameObject("World");
            Box(world, "Floor", Mat("Floor.mat"), new Vector3(0, -0.05f, 0), new Vector3(20, 0.1f, 20));
            Box(world, "Wall-N", Mat("Wall.mat"), new Vector3(0, 1.5f, 5), new Vector3(20, 3, 0.2f));
            Box(world, "Wall-S", Mat("Wall.mat"), new Vector3(0, 1.5f, -5), new Vector3(20, 3, 0.2f));
            Box(world, "Wall-E", Mat("Wall.mat"), new Vector3(5, 1.5f, 0), new Vector3(0.2f, 3, 20));
            Box(world, "Wall-W", Mat("Wall.mat"), new Vector3(-5, 1.5f, 0), new Vector3(0.2f, 3, 20));
            return world;
        }

        private static GameObject Box(GameObject parent, string name, Material material, Vector3 pos, Vector3 scale)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent.transform, false);
            box.transform.localPosition = pos;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private static void CreateLighting()
        {
            GameObject lightGo = new GameObject("Sun");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.97f, 0.92f);
            lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.4f);
        }

        private static void CreateOvrRig()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (prefab == null)
            {
                Debug.LogError("[AmbedkarHeritage] OVRCameraRig prefab not found at " + RigPath);
                return;
            }

            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.name = "OVR Camera Rig";
            rig.transform.position = new Vector3(0, 0, -3.5f);

            GameObject managerGo = new GameObject("OVRManager");
            OVRManager manager = managerGo.AddComponent<OVRManager>();
            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;

            Transform tracking = rig.transform.Find("TrackingSpace");
            if (tracking == null)
            {
                Debug.LogError("[AmbedkarHeritage] OVRCameraRig has no TrackingSpace child.");
                return;
            }

            SetupHand(tracking.Find("LeftHandAnchor"), OVRInput.Controller.LTouch);
            SetupHand(tracking.Find("RightHandAnchor"), OVRInput.Controller.RTouch);
        }

        private static void SetupHand(Transform anchor, OVRInput.Controller controller)
        {
            if (anchor == null)
            {
                Debug.LogWarning("[AmbedkarHeritage] Hand anchor missing for " + controller);
                return;
            }

            Transform grip = new GameObject("Grip").transform;
            grip.SetParent(anchor, false);
            grip.localPosition = new Vector3(0, 0, -0.05f);

            GameObject volumeGo = new GameObject("GrabVolume");
            volumeGo.transform.SetParent(anchor, false);
            volumeGo.transform.localPosition = new Vector3(0, 0, -0.03f);
            SphereCollider volume = volumeGo.AddComponent<SphereCollider>();
            volume.isTrigger = true;
            volume.radius = 0.12f;

            OVRGrabber grabber = anchor.gameObject.AddComponent<OVRGrabber>();
            SerializedObject so = new SerializedObject(grabber);
            so.FindProperty("m_controller").intValue = (int)controller;
            so.FindProperty("m_gripTransform").objectReferenceValue = grip;
            so.FindProperty("m_parentHeldObject").boolValue = false;
            so.FindProperty("m_moveHandPosition").boolValue = false;
            SerializedProperty volumes = so.FindProperty("m_grabVolumes");
            volumes.arraySize = 1;
            volumes.GetArrayElementAtIndex(0).objectReferenceValue = volume;
            so.ApplyModifiedPropertiesWithoutUndo();

            ExhibitRayHighlighter highlighter = anchor.gameObject.AddComponent<ExhibitRayHighlighter>();
            highlighter.Configure(controller);
        }

        private static void CreateEntranceSign()
        {
            GameObject signGo = new GameObject("EntranceSign");
            signGo.transform.position = new Vector3(0, 2.3f, -4.75f);
            signGo.transform.rotation = Quaternion.Euler(0, 180, 0);

            Canvas canvas = signGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(5.5f, 2.6f);
            rect.localScale = Vector3.one * 0.01f;

            Text title = MakeText(signGo, "TitleText", new Vector2(0.5f, 0.82f), new Vector2(1f, 0.2f),
                120, TextAnchor.MiddleCenter, MuseumApp.AppName);
            Text welcome = MakeText(signGo, "WelcomeText", new Vector2(0.5f, 0.42f), new Vector2(1f, 0.4f),
                46, TextAnchor.UpperCenter, MuseumApp.WelcomeMessage);
            Text status = MakeText(signGo, "StatusText", new Vector2(0.5f, 0.06f), new Vector2(1f, 0.16f),
                30, TextAnchor.MiddleCenter, "");

            EntranceSign sign = signGo.AddComponent<EntranceSign>();
            Bind(sign, ("titleText", title), ("welcomeText", welcome));
            VrStatusText statusText = signGo.AddComponent<VrStatusText>();
            Bind(statusText, ("statusLabel", status));
            sign.Apply(MuseumApp.AppName, MuseumApp.WelcomeMessage);
        }

        private static void CreateExhibitPrefab()
        {
            GameObject exhibit = new GameObject("Exhibit");
            Rigidbody body = exhibit.AddComponent<Rigidbody>();
            body.useGravity = true;

            BoxCollider collider = exhibit.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.4f, 0.4f, 0.4f);

            OVRGrabbable grabbable = exhibit.AddComponent<OVRGrabbable>();
            SerializedObject grabSo = new SerializedObject(grabbable);
            grabSo.FindProperty("m_allowOffhandGrab").boolValue = true;
            grabSo.FindProperty("m_snapPosition").boolValue = false;
            grabSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(exhibit.transform, false);
            visual.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = Mat("Exhibit.mat");

            GameObject panel = CreateInfoPanel(exhibit.transform);

            ExhibitController controller = exhibit.AddComponent<ExhibitController>();
            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("archiveRecordId").stringValue = ExhibitIds[0];
            controllerSo.FindProperty("infoPanel").objectReferenceValue = panel.transform;
            Bind(controllerSo, panel,
                ("titleLabel", "TitleText"),
                ("metaLabel", "MetaText"),
                ("descriptionLabel", "DescriptionText"),
                ("sourceLabel", "SourceText"));
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(exhibit, ExhibitPrefabPath);
            Object.DestroyImmediate(exhibit);
        }

        private static GameObject CreateInfoPanel(Transform parent)
        {
            GameObject panel = new GameObject("InfoPanel");
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = new Vector3(0, 0.55f, 0);
            panel.transform.localRotation = Quaternion.identity;

            Canvas canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(1.5f, 1.05f);
            rect.localScale = Vector3.one * 0.0035f;

            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(panel.transform, false);
            RectTransform bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            bgImage.raycastTarget = false;

            MakeText(panel, "TitleText", new Vector2(0.5f, 0.88f), new Vector2(1f, 0.2f),
                26, TextAnchor.MiddleCenter, "");
            MakeText(panel, "MetaText", new Vector2(0.5f, 0.68f), new Vector2(1f, 0.16f),
                15, TextAnchor.MiddleCenter, "");
            MakeText(panel, "DescriptionText", new Vector2(0.5f, 0.4f), new Vector2(1f, 0.4f),
                15, TextAnchor.UpperCenter, "");
            MakeText(panel, "SourceText", new Vector2(0.5f, 0.07f), new Vector2(1f, 0.14f),
                12, TextAnchor.MiddleCenter, "");

            panel.SetActive(false);
            return panel;
        }

        private static void PlaceExhibits(GameObject world)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExhibitPrefabPath);
            float[] xs = { -2.6f, 0f, 2.6f };

            for (int i = 0; i < ExhibitIds.Length; i++)
            {
                GameObject pedestal = Box(world, "Pedestal-" + i, Mat("Accent.mat"),
                    new Vector3(xs[i], 0.45f, 2.6f), new Vector3(1.1f, 0.9f, 1.1f));

                GameObject exhibit = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                exhibit.name = "Exhibit-" + ExhibitIds[i];
                exhibit.transform.SetParent(pedestal.transform, false);
                exhibit.transform.localPosition = new Vector3(0, 0.45f, 0);
                exhibit.transform.localRotation = Quaternion.Euler(0, 180, 0);

                ExhibitController controller = exhibit.GetComponent<ExhibitController>();
                SerializedObject so = new SerializedObject(controller);
                so.FindProperty("archiveRecordId").stringValue = ExhibitIds[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void CreateGrabBall()
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "DemoGrabBall";
            ball.transform.position = new Vector3(0.9f, 1.25f, -0.5f);
            ball.transform.localScale = Vector3.one * 0.3f;
            ball.AddComponent<Rigidbody>();
            ball.AddComponent<OVRGrabbable>();
        }

        private static Text MakeText(GameObject parent, string name, Vector2 anchor, Vector2 size,
            int fontSize, TextAnchor alignment, string value)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor - size * 0.5f;
            rect.anchorMax = anchor + size * 0.5f;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = go.AddComponent<Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.font = LoadFont();
            text.raycastTarget = false;
            return text;
        }

        private static Font LoadFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void Bind(Object target, params (string field, Object value)[] fields)
        {
            SerializedObject so = new SerializedObject(target);
            foreach ((string field, Object value) entry in fields)
            {
                so.FindProperty(entry.field).objectReferenceValue = entry.value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Bind(SerializedObject so, GameObject root,
            params (string field, string child)[] fields)
        {
            foreach ((string field, string child) entry in fields)
            {
                so.FindProperty(entry.field).objectReferenceValue = root.transform.Find(entry.child);
            }
        }
    }
}