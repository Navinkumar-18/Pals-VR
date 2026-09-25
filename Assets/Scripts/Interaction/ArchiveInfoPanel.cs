using AmbedkarHeritage.Core;
using AmbedkarHeritage.UI;
using AmbedkarHeritage.VR;
using UnityEngine;
using UnityEngine.UI;

namespace AmbedkarHeritage.Interaction
{
    /// <summary>
    /// Central world-space archive information panel. Any exhibit (or related
    /// record) can be opened here. Shows TITLE / Date / Category / Description /
    /// Source plus the action row [VIEW ORIGINAL] [READ OCR TEXT] [LISTEN]
    /// [TRANSLATE] [RELATED CONTENT] [ASK AI] [CLOSE].
    ///
    /// Features that arrive in later phases show an honest
    /// "coming in next phase" toast instead of pretending to work.
    /// </summary>
    public sealed class ArchiveInfoPanel : MonoBehaviour
    {
        public static ArchiveInfoPanel Instance { get; private set; }

        [SerializeField]
        private Text titleText;

        [SerializeField]
        private Text metaText;

        [SerializeField]
        private Text descriptionText;

        [SerializeField]
        private Text sourceText;

        [SerializeField]
        private Text toastText;

        [SerializeField]
        private GameObject bodyRoot;

        [SerializeField]
        private GameObject relatedRoot;

        [SerializeField]
        private Text relatedHeaderText;

        [SerializeField]
        private Transform relatedListRoot;

        [SerializeField]
        private VRButton viewOriginalButton;

        [SerializeField]
        private VRButton readTextButton;

        [SerializeField]
        private VRButton listenButton;

        [SerializeField]
        private VRButton translateButton;

        [SerializeField]
        private VRButton relatedButton;

        [SerializeField]
        private VRButton askAiButton;

        [SerializeField]
        private VRButton closeButton;

        [SerializeField]
        private VRButton backButton;

        [SerializeField]
        private VRButton relatedButtonTemplate;

        private ArchiveRecord _record;
        private ExhibitController _sourceExhibit;
        private float _toastUntil;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>Number of wired action buttons (view/read/listen/translate/related/ask-ai).</summary>
        public int ActionButtonCount
        {
            get
            {
                int count = 0;
                if (viewOriginalButton != null) count++;
                if (readTextButton != null) count++;
                if (listenButton != null) count++;
                if (translateButton != null) count++;
                if (relatedButton != null) count++;
                if (askAiButton != null) count++;
                return count;
            }
        }

        public VRButton CloseButton
        {
            get { return closeButton; }
        }

        public bool IsShowing(ExhibitController exhibit)
        {
            return gameObject.activeInHierarchy && _sourceExhibit == exhibit;
        }

        public void Configure(Text title, Text meta, Text description, Text source, Text toast,
            VRButton viewOriginal, VRButton readText, VRButton listen, VRButton translate,
            VRButton related, VRButton askAi, VRButton close, VRButton back,
            GameObject body, GameObject relatedRootGo, Text relatedHeader, Transform relatedList,
            VRButton relatedTemplate)
        {
            titleText = title;
            metaText = meta;
            descriptionText = description;
            sourceText = source;
            toastText = toast;
            viewOriginalButton = viewOriginal;
            readTextButton = readText;
            listenButton = listen;
            translateButton = translate;
            relatedButton = related;
            askAiButton = askAi;
            closeButton = close;
            backButton = back;
            bodyRoot = body;
            relatedRoot = relatedRootGo;
            relatedHeaderText = relatedHeader;
            relatedListRoot = relatedList;
            relatedButtonTemplate = relatedTemplate;

            if (viewOriginalButton != null) viewOriginalButton.SetAction(() => ShowViewer(false));
            if (readTextButton != null) readTextButton.SetAction(() => ShowViewer(true));
            if (listenButton != null) listenButton.SetAction(() => ShowComingSoon(listenButton.Label));
            if (translateButton != null) translateButton.SetAction(() => ShowComingSoon(translateButton.Label));
            if (relatedButton != null) relatedButton.SetAction(ShowRelated);
            if (askAiButton != null) askAiButton.SetAction(() => ShowComingSoon(askAiButton.Label));
            if (closeButton != null) closeButton.SetAction(Hide);
            if (backButton != null) backButton.SetAction(ShowBody);

            if (toastText != null)
            {
                toastText.text = "";
            }
        }

        public void Open(ArchiveRecord record, ExhibitController sourceExhibit)
        {
            if (record == null)
            {
                return;
            }

            _record = record;
            _sourceExhibit = sourceExhibit;

            ShowBody();
            gameObject.SetActive(true);
            VrSession.SelectExhibit(record.id);

            if (titleText != null) titleText.text = record.title ?? "";
            if (metaText != null) metaText.text = FormatMeta(record);
            if (descriptionText != null) descriptionText.text = record.description ?? "";
            if (sourceText != null) sourceText.text = FormatSource(record);
            if (toastText != null) toastText.text = "";
        }

        /// <summary>Closes the panel. Only honored when opened for the same exhibit (guards cross-exhibit closes).</summary>
        public void Close(ExhibitController requester)
        {
            if (requester != null && _sourceExhibit != null && requester != _sourceExhibit)
            {
                return;
            }

            Hide();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            _record = null;
            if (_sourceExhibit != null)
            {
                _sourceExhibit.SetClosedByPanel();
                _sourceExhibit = null;
            }
        }

        /// <summary>Called by an exhibit when its station activation is toggled off directly.</summary>
        public void NotifyExhibitClosed(ExhibitController exhibit)
        {
            if (_sourceExhibit == exhibit)
            {
                _sourceExhibit = null;
            }
        }

        private void ShowBody()
        {
            if (bodyRoot != null) bodyRoot.SetActive(true);
            if (relatedRoot != null) relatedRoot.SetActive(false);
        }

        private void ShowViewer(bool textMode)
        {
            if (_record == null || DocumentViewer.Instance == null)
            {
                return;
            }

            DocumentViewer.Instance.Open(_record, textMode);
        }

        private void ShowRelated()
        {
            if (_record == null || bodyRoot == null || relatedRoot == null)
            {
                return;
            }

            System.Collections.Generic.List<ArchiveRecord> related = ExhibitRegistry.ResolveRelated(_record);
            if (related.Count == 0)
            {
                SetToast("No related records in this demo record. Add relatedIds to link records.");
                return;
            }

            bodyRoot.SetActive(false);

            if (relatedHeaderText != null)
            {
                relatedHeaderText.text = "RELATED CONTENT — " + _record.id;
            }

            // Rebuild the button list from data (clear stragglers first).
            for (int i = relatedListRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(relatedListRoot.GetChild(i).gameObject);
            }

            for (int i = 0; i < related.Count; i++)
            {
                ArchiveRecord relatedRecord = related[i];
                VRButton button = CreateRelatedButton(relatedRecord, i, related.Count);
                if (button != null)
                {
                    button.SetLabel(relatedRecord.title ?? relatedRecord.id);
                    button.SetAction(() => ExhibitRegistry.ShowRecord(relatedRecord));
                }
            }

            relatedRoot.SetActive(true);
        }

        private VRButton CreateRelatedButton(ArchiveRecord record, int index, int count)
        {
            if (relatedButtonTemplate == null)
            {
                return null;
            }

            VRButton button = Instantiate(relatedButtonTemplate, relatedListRoot);
            button.name = "Related-" + (record.id ?? ("row" + index));

            float step = 1f / Mathf.Max(1, count);
            float centerY = 0.85f - (index + 0.5f) * step * 0.72f;
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0.5f, centerY);
            rect.anchorMax = new Vector2(0.5f, centerY);
            rect.anchoredPosition = Vector2.zero;
            return button;
        }

        private void ShowComingSoon(string feature)
        {
            SetToast(feature + " — coming in next phase");
        }

        private void SetToast(string message)
        {
            if (toastText == null)
            {
                return;
            }

            toastText.text = message;
            _toastUntil = Time.unscaledTime + 3.5f;
        }

        private void Update()
        {
            if (toastText != null && !string.IsNullOrEmpty(toastText.text) && Time.unscaledTime > _toastUntil)
            {
                toastText.text = "";
            }
        }

        public static string FormatMeta(ArchiveRecord record)
        {
            string meta = "Date: " + (string.IsNullOrEmpty(record.date) ? "n/a" : record.date)
                          + "   |   Category: " + (string.IsNullOrEmpty(record.category) ? "n/a" : record.category)
                          + "   |   Type: " + record.type
                          + "   |   Language: " + (string.IsNullOrEmpty(record.language) ? "n/a" : record.language);

            // Phase E: surface the document-level OCR state on the info panel.
            string ocrStatus = record.ocrStatus;
            if (!string.IsNullOrEmpty(ocrStatus) && ocrStatus != "NONE")
            {
                meta += "   |   OCR: " + ocrStatus;
                if (record.ocrPages > 0)
                {
                    meta += " (" + record.ocrPages + " page" + (record.ocrPages == 1 ? "" : "s") + ")";
                }
                if (!string.IsNullOrEmpty(record.ocrLanguage))
                {
                    meta += " [" + record.ocrLanguage + "]";
                }
            }

            return meta;
        }

        public static string FormatSource(ArchiveRecord record)
        {
            string label = record.isSampleData ? "SAMPLE/DEMO DATA" : "ARCHIVAL DATA";
            string source = string.IsNullOrEmpty(record.source) ? "n/a" : record.source;
            string citation = record.verified ? " | Citation: " + (string.IsNullOrEmpty(record.citation) ? "n/a" : record.citation) : "";
            return "Source (" + label + "): " + source + "   |   Document ID: " + record.id + citation;
        }
    }
}