using AmbedkarHeritage.Core;
using AmbedkarHeritage.Interaction;
using AmbedkarHeritage.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AmbedkarHeritage.ExhibitRoom
{
    /// <summary>
    /// Data-driven constructor for the Archive Room: exhibit stations, the
    /// central archive info panel, the document viewer, the category filter bar
    /// and the UI pointers. Everything is driven by archive_room.json +
    /// sample_archive.json — no exhibit content lives in code. Used by the
    /// editor builder (baking the scene) and by regression checks (temp scene).
    /// </summary>
    public static class MuseumRoomBuilder
    {
        public const string RoomRootName = "ArchiveRoom";

        private static Material s_accent;
        private static Material s_board;
        private static Material s_screen;
        private static Material s_frame;

        private static readonly Color ButtonColor = new Color(0.22f, 0.30f, 0.42f, 1f);
        private static readonly Color CloseColor = new Color(0.48f, 0.20f, 0.16f, 1f);
        private static readonly Color AccentTextColor = new Color(1f, 0.85f, 0.5f, 1f);

        public static void BuildMuseumRoom(GameObject parent)
        {
            BuildMuseumRoom(parent, ArchiveRoomDataLoader.Load());
        }

        public static void BuildMuseumRoom(GameObject parent, ArchiveRoomConfig config)
        {
            if (parent == null || config == null)
            {
                return;
            }

            DemoArchiveLoader.Instance.Load();

            GameObject room = new GameObject(RoomRootName);
            room.transform.SetParent(parent.transform, false);

            EnsureMaterials(config);

            for (int i = 0; i < config.stations.Count; i++)
            {
                BuildStation(room.transform, config.stations[i]);
            }

            BuildInfoPanel(room.transform);
            BuildDocumentViewer(room.transform);
            BuildCategoryFilter(room.transform);
            AttachUIPointers();
        }

        // ------------------------------------------------------------------
        // Stations
        // ------------------------------------------------------------------

        private static void BuildStation(Transform room, ExhibitStation station)
        {
            if (station == null || string.IsNullOrEmpty(station.exhibitId))
            {
                Debug.LogWarning("[AmbedkarHeritage] Archive room contains an unnamed station; skipped.");
                return;
            }

            GameObject root = new GameObject("Exhibit-" + station.exhibitId);
            root.transform.SetParent(room, false);
            root.transform.localPosition = station.Position;
            root.transform.localRotation = station.Rotation;
            root.transform.localScale = Vector3.one * station.scale;

            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(root.transform, false);
            pedestal.transform.localPosition = new Vector3(0, 0.35f, 0);
            pedestal.transform.localScale = new Vector3(1.1f, 0.75f, 0.8f);
            pedestal.GetComponent<Renderer>().sharedMaterial = s_accent;
            // primitive BoxCollider stays (non-trigger) so the ray highlighter can find the station.

            ExhibitController exhibit = root.AddComponent<ExhibitController>();
            exhibit.RecordBoundId = station.exhibitId;
            exhibit.DisplayKind = station.displayType;
            exhibit.ResolveRecord();

            BuildStationVisual(root.transform, exhibit);
            BuildStationLabel(root.transform, exhibit, station);

            ShimGrabbable(root);
            ExhibitRegistry.Register(exhibit);

            Debug.Log("[AmbedkarHeritage] Archive station built: " + station.exhibitId);
        }

        private static void BuildStationVisual(Transform root, ExhibitController exhibit)
        {
            switch (exhibit.VisualKind)
            {
                case ExhibitVisualKind.Screen:
                {
                    GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    screen.name = "Screen";
                    screen.transform.SetParent(root, false);
                    screen.transform.localPosition = new Vector3(0, 1.3f, 0.02f);
                    screen.transform.localScale = new Vector3(1.6f, 0.95f, 0.06f);
                    screen.GetComponent<Renderer>().sharedMaterial = s_screen;
                    break;
                }
                case ExhibitVisualKind.Speaker:
                {
                    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    body.name = "Speaker";
                    body.transform.SetParent(root, false);
                    body.transform.localPosition = new Vector3(0, 1.02f, 0.02f);
                    body.transform.localScale = new Vector3(0.22f, 0.35f, 0.22f);
                    body.GetComponent<Renderer>().sharedMaterial = s_board;

                    GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    cap.name = "SpeakerCap";
                    cap.transform.SetParent(root, false);
                    cap.transform.localPosition = new Vector3(0, 1.42f, 0.02f);
                    cap.transform.localScale = Vector3.one * 0.18f;
                    cap.GetComponent<Renderer>().sharedMaterial = s_accent;
                    break;
                }
                default:
                {
                    GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    board.name = "Board";
                    board.transform.SetParent(root, false);
                    board.transform.localPosition = new Vector3(0, 1.18f, 0.02f);
                    board.transform.localScale = new Vector3(0.85f, 1.05f, 0.06f);
                    board.GetComponent<Renderer>().sharedMaterial = s_board;

                    GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    frame.name = "Frame";
                    frame.transform.SetParent(root, false);
                    frame.transform.localPosition = new Vector3(0, 1.18f, 0.05f);
                    frame.transform.localScale = new Vector3(0.62f, 0.78f, 0.02f);
                    frame.GetComponent<Renderer>().sharedMaterial = s_frame;
                    break;
                }
            }
        }

        private static void BuildStationLabel(Transform root, ExhibitController exhibit, ExhibitStation station)
        {
            ArchiveRecord record = exhibit.Record;
            Canvas canvas = UiFactory.MakeWorldCanvas("Label", root, new Vector3(0, 1.58f, 0.03f),
                Quaternion.identity, 700, 200, 0.001f);

            string title = record != null ? record.title : ("[" + station.exhibitId + "]");
            UiFactory.MakeLabel(canvas.transform, "Title", new Vector2(0.5f, 0.62f), new Vector2(660, 130),
                40, TextAnchor.MiddleCenter, title, Color.white);

            string subtitle = record != null
                ? CategoryMap.Label(CategoryMap.For(record.type)) + " · " + record.type
                : station.displayType;
            UiFactory.MakeLabel(canvas.transform, "Category", new Vector2(0.5f, 0.16f), new Vector2(660, 60),
                28, TextAnchor.MiddleCenter, subtitle, record != null && record.verified ? AccentTextColor : new Color(0.75f, 0.75f, 0.8f, 1f));
        }

        private static void ShimGrabbable(GameObject root)
        {
            // Stations are static display pieces, not grab balls.
            OVRGrabbable grabbable = root.GetComponent<OVRGrabbable>();
            if (grabbable != null)
            {
                grabbable.enabled = false;
            }

            Rigidbody body = root.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        // ------------------------------------------------------------------
        // Archive info panel
        // ------------------------------------------------------------------

        private static void BuildInfoPanel(Transform room)
        {
            Canvas canvas = UiFactory.MakeWorldCanvas("ArchiveInfoPanel", room,
                new Vector3(0, 1.62f, -1.9f), Quaternion.Euler(0, 180, 0), 1900, 1150, 0.001f,
                new Color(0.05f, 0.05f, 0.08f, 0.95f));
            UiFactory.AddBlockerCollider(canvas);

            RectTransform body = MakeFullStretchChild(canvas.transform, "BodyRoot");

            Text title = UiFactory.MakeLabel(body, "TitleText", new Vector2(0.5f, 0.92f), new Vector2(1700, 190),
                96, TextAnchor.MiddleCenter, "", Color.white);
            Text meta = UiFactory.MakeLabel(body, "MetaText", new Vector2(0.5f, 0.80f), new Vector2(1700, 90),
                48, TextAnchor.MiddleCenter, "", Color.white);
            Text desc = UiFactory.MakeLabel(body, "DescriptionText", new Vector2(0.5f, 0.575f), new Vector2(1680, 330),
                42, TextAnchor.UpperLeft, "", Color.white);
            Text source = UiFactory.MakeLabel(body, "SourceText", new Vector2(0.5f, 0.31f), new Vector2(1700, 110),
                36, TextAnchor.MiddleCenter, "", new Color(0.85f, 0.85f, 0.9f, 1f));
            Text toast = UiFactory.MakeLabel(body, "ToastText", new Vector2(0.5f, 0.225f), new Vector2(1700, 70),
                32, TextAnchor.MiddleCenter, "", AccentTextColor);

            string[] actionLabels = { "VIEW ORIGINAL", "READ TEXT", "LISTEN", "TRANSLATE", "RELATED CONTENT", "ASK AI", "CLOSE" };
            VRButton[] buttons = new VRButton[actionLabels.Length];
            for (int i = 0; i < actionLabels.Length; i++)
            {
                float centerX = 0.06f + i * 0.14667f;
                Color color = i == actionLabels.Length - 1 ? CloseColor : ButtonColor;
                buttons[i] = UiFactory.MakeButton(canvas, body, actionLabels[i], actionLabels[i],
                    new Vector2(centerX, 0.075f), new Vector2(235, 105), 32, color, null);
            }

            RectTransform relatedRoot = MakeFullStretchChild(canvas.transform, "RelatedRoot");
            Text relatedHeader = UiFactory.MakeLabel(relatedRoot, "RelatedHeaderText", new Vector2(0.5f, 0.9f),
                new Vector2(1700, 110), 56, TextAnchor.MiddleCenter, "RELATED CONTENT", Color.white);
            VRButton back = UiFactory.MakeButton(canvas, relatedRoot, "BackButton", "‹ BACK",
                new Vector2(0.1f, 0.06f), new Vector2(220, 95), 34, ButtonColor, null);

            VRButton relatedTemplate = UiFactory.MakeButton(canvas, relatedRoot, "RelatedTemplate", "RELATED",
                new Vector2(0.5f, 0.5f), new Vector2(900, 95), 34, ButtonColor, null);
            relatedTemplate.gameObject.SetActive(false);

            ArchiveInfoPanel panel = canvas.gameObject.AddComponent<ArchiveInfoPanel>();
            panel.Configure(title, meta, desc, source, toast,
                buttons[0], buttons[1], buttons[2], buttons[3], buttons[4], buttons[5], buttons[6],
                back, body.gameObject, relatedRoot.gameObject, relatedHeader, relatedRoot, relatedTemplate);

            canvas.gameObject.SetActive(false); // opened by exhibits / related navigation
        }

        // ------------------------------------------------------------------
        // Document viewer
        // ------------------------------------------------------------------

        private static void BuildDocumentViewer(Transform room)
        {
            Canvas canvas = UiFactory.MakeWorldCanvas("DocumentViewer", room,
                new Vector3(0, 1.72f, -2.25f), Quaternion.Euler(0, 180, 0), 2000, 1300, 0.001f,
                new Color(0.04f, 0.05f, 0.09f, 0.96f));
            UiFactory.AddBlockerCollider(canvas);

            Text header = UiFactory.MakeLabel(canvas.transform, "HeaderText", new Vector2(0.5f, 0.93f),
                new Vector2(1800, 110), 60, TextAnchor.MiddleCenter, "", Color.white);

            // Zoomable content area (image or text page).
            RectTransform content = MakeChildAt(canvas.transform, "Content", new Vector2(0.5f, 0.55f), new Vector2(900, 1000));

            GameObject imageGo = new GameObject("PageImage");
            imageGo.transform.SetParent(content, false);
            RectTransform imageRect = imageGo.AddComponent<RectTransform>();
            Stretch(imageRect);
            RawImage rawImage = imageGo.AddComponent<RawImage>();

            Text pageText = UiFactory.MakeLabel(content, "PageText", new Vector2(0.5f, 0.5f), new Vector2(880, 980),
                34, TextAnchor.UpperLeft, "", Color.white);

            Text counter = UiFactory.MakeLabel(canvas.transform, "PageCounterText", new Vector2(0.5f, 0.105f),
                new Vector2(500, 60), 30, TextAnchor.MiddleCenter, "", new Color(0.8f, 0.8f, 0.85f, 1f));

            string[] viewerLabels = { "ZOOM +", "ZOOM -", "PREV", "NEXT", "CLOSE" };
            float[] viewerX = { 0.12f, 0.32f, 0.5f, 0.68f, 0.86f };
            VRButton[] viewerButtons = new VRButton[viewerLabels.Length];
            for (int i = 0; i < viewerLabels.Length; i++)
            {
                Color color = i == viewerLabels.Length - 1 ? CloseColor : ButtonColor;
                viewerButtons[i] = UiFactory.MakeButton(canvas, canvas.transform, viewerLabels[i], viewerLabels[i],
                    new Vector2(viewerX[i], 0.05f), new Vector2(250, 100), 36, color, null);
            }

            DocumentViewer viewer = canvas.gameObject.AddComponent<DocumentViewer>();
            viewer.Configure(header, counter, content, rawImage, pageText,
                viewerButtons[0], viewerButtons[1], viewerButtons[2], viewerButtons[3], viewerButtons[4]);

            canvas.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Category filter bar
        // ------------------------------------------------------------------

        private static void BuildCategoryFilter(Transform room)
        {
            Canvas canvas = UiFactory.MakeWorldCanvas("ArchiveCategoryFilter", room,
                new Vector3(0, 1.78f, -4.55f), Quaternion.identity, 2210, 170, 0.0009f,
                new Color(0.06f, 0.06f, 0.1f, 0.92f));
            UiFactory.AddBlockerCollider(canvas);

            ArchiveCategory[] order = CategoryMap.FilterOrder;
            VRButton[] buttons = new VRButton[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                float centerX = 0.06f + i * 0.14667f;
                string label = CategoryMap.Label(order[i]);
                buttons[i] = UiFactory.MakeButton(canvas, canvas.transform, "Filter-" + label, label,
                    new Vector2(centerX, 0.5f), new Vector2(280, 110), 38, ButtonColor, null);
            }

            canvas.gameObject.AddComponent<ArchiveCategoryFilter>().Configure(buttons, order);
        }

        // ------------------------------------------------------------------
        // UI pointers (hand rays)
        // ------------------------------------------------------------------

        private static void AttachUIPointers()
        {
            OVRCameraRig rig = Object.FindObjectOfType<OVRCameraRig>();
            if (rig == null)
            {
                Debug.LogWarning("[AmbedkarHeritage] No OVRCameraRig found; UI pointers not attached "
                                 + "(buttons need a hand ray to press).");
                return;
            }

            AttachPointer(rig, "TrackingSpace/LeftHandAnchor", OVRInput.Controller.LTouch);
            AttachPointer(rig, "TrackingSpace/RightHandAnchor", OVRInput.Controller.RTouch);
        }

        private static void AttachPointer(OVRCameraRig rig, string anchorPath, OVRInput.Controller controller)
        {
            Transform anchor = rig.transform.Find(anchorPath);
            if (anchor == null)
            {
                return;
            }

            if (anchor.GetComponent<UIPointer>() == null)
            {
                UIPointer pointer = anchor.gameObject.AddComponent<UIPointer>();
                pointer.Configure(controller);
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static void EnsureMaterials(ArchiveRoomConfig config)
        {
            if (s_accent != null)
            {
                return;
            }

            s_accent = MakeMaterial(config.Accent);
            s_board = MakeMaterial(config.Board);
            s_screen = MakeMaterial(new Color(0.02f, 0.02f, 0.035f, 1f));
            s_frame = MakeMaterial(new Color(0.8f, 0.78f, 0.72f, 1f));
        }

        private static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.SetColor("_BaseColor", color);
            return material;
        }

        private static RectTransform MakeFullStretchChild(Transform parent, string name)
        {
            RectTransform rect = MakeChildAt(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero);
            Stretch(rect);
            return rect;
        }

        private static RectTransform MakeChildAt(Transform parent, string name, Vector2 centerNorm, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = centerNorm;
            rect.anchorMax = centerNorm;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}