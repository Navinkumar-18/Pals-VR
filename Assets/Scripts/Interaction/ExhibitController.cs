using AmbedkarHeritage.Core;
using AmbedkarHeritage.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AmbedkarHeritage.Interaction
{
    /// <summary>How an exhibit station is presented (from data, never hard-coded content).</summary>
    public enum ExhibitVisualKind
    {
        Automatic,
        Board,
        Speaker,
        Screen
    }

    /// <summary>
    /// Reusable data-driven exhibit. Every exhibit instance binds to one
    /// ArchiveRecord (from the demo archive today, from the backend API later)
    /// and opens the central archive info panel when activated. No custom
    /// scripts are needed per exhibit — the room builder creates stations from
    /// archive_room.json.
    ///
    /// Phase B behavior is preserved: highlight via property blocks, Toggle /
    /// Activate / Deactivate, VrSession selection, and the legacy per-exhibit
    /// info panel fallback when no central panel is wired.
    /// </summary>
    [RequireComponent(typeof(OVRGrabbable))]
    public sealed class ExhibitController : MonoBehaviour
    {
        [SerializeField]
        private string archiveRecordId;

        [SerializeField]
        private string displayKind = "";

        [SerializeField]
        private Transform infoPanel;

        [SerializeField]
        private Text titleLabel;

        [SerializeField]
        private Text metaLabel;

        [SerializeField]
        private Text descriptionLabel;

        [SerializeField]
        private Text sourceLabel;

        private ArchiveRecord _record;
        private bool _isActive;

        public string RecordBoundId
        {
            get { return archiveRecordId; }
            set { archiveRecordId = value; }
        }

        public string DisplayKind
        {
            get { return displayKind; }
            set { displayKind = value; }
        }

        public string RecordId
        {
            get { return _record != null ? _record.id : archiveRecordId; }
        }

        public ArchiveRecord Record
        {
            get { return _record; }
        }

        public bool IsOpen
        {
            get { return _isActive; }
        }

        public ArchiveCategory Bucket
        {
            get { return CategoryMap.For(_record != null ? _record.type : ArchiveRecordType.Unknown); }
        }

        public ExhibitVisualKind VisualKind
        {
            get { return ResolveKind(); }
        }

        private void Awake()
        {
            ResolveRecord();
            SetActive(false);
        }

        /// <summary>Re-binds the record (idempotent; called from Awake and by regression checks).</summary>
        public void ResolveRecord()
        {
            _record = DemoArchiveLoader.Instance.Find(archiveRecordId);
            if (_record == null && !string.IsNullOrEmpty(archiveRecordId))
            {
                Debug.LogWarning("[AmbedkarHeritage] Exhibit bound to unknown record id: " + archiveRecordId);
            }
        }

        public void SetHighlight(bool highlighted)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                if (highlighted)
                {
                    block.SetColor("_BaseColor", new Color(1f, 0.92f, 0.4f, 1f));
                }

                renderers[i].SetPropertyBlock(block);
            }
        }

        public void Toggle()
        {
            SetActive(!_isActive);
        }

        public void Activate()
        {
            SetActive(true);
        }

        public void Deactivate()
        {
            SetActive(false);
        }

        /// <summary>Called by the central panel so station selection stays in sync when CLOSE is pressed.</summary>
        public void SetClosedByPanel()
        {
            _isActive = false;
            if (infoPanel != null)
            {
                infoPanel.gameObject.SetActive(false);
            }
        }

        private void SetActive(bool active)
        {
            _isActive = active;

            if (active)
            {
                VR.VrSession.SelectExhibit(_record != null ? _record.id : archiveRecordId);

                ArchiveInfoPanel panel = ArchiveInfoPanel.Instance;
                if (panel != null && panel.gameObject.activeInHierarchy && _record != null)
                {
                    panel.Open(_record, this);
                    if (infoPanel != null)
                    {
                        infoPanel.gameObject.SetActive(false);
                    }

                    return;
                }
            }
            else
            {
                ArchiveInfoPanel panel = ArchiveInfoPanel.Instance;
                if (panel != null)
                {
                    panel.Close(this);
                }
            }

            // Legacy fallback: per-exhibit info panel for scenes without the central panel.
            if (infoPanel != null)
            {
                infoPanel.gameObject.SetActive(active);
            }

            if (active && _record != null)
            {
                if (titleLabel != null)
                {
                    titleLabel.text = _record.title;
                }

                if (metaLabel != null)
                {
                    metaLabel.text = FormatMeta(_record);
                }

                if (descriptionLabel != null)
                {
                    descriptionLabel.text = _record.description;
                }

                if (sourceLabel != null)
                {
                    sourceLabel.text = FormatSource(_record);
                }
            }
        }

        private ExhibitVisualKind ResolveKind()
        {
            string kind = (displayKind ?? string.Empty).Trim().ToLowerInvariant();
            switch (kind)
            {
                case "speech":
                case "audio":
                    return ExhibitVisualKind.Speaker;
                case "video":
                    return ExhibitVisualKind.Screen;
                case "board":
                case "manuscript":
                case "book":
                case "document":
                case "letter":
                case "photograph":
                case "event":
                    return ExhibitVisualKind.Board;
                default:
                    return KindForRecordType();
            }
        }

        private ExhibitVisualKind KindForRecordType()
        {
            if (_record == null)
            {
                return ExhibitVisualKind.Board;
            }

            switch (_record.type)
            {
                case ArchiveRecordType.Speech:
                case ArchiveRecordType.Audio:
                    return ExhibitVisualKind.Speaker;
                case ArchiveRecordType.Video:
                    return ExhibitVisualKind.Screen;
                default:
                    return ExhibitVisualKind.Board;
            }
        }

        private void OnDestroy()
        {
            ExhibitRegistry.Unregister(this);

            ArchiveInfoPanel panel = ArchiveInfoPanel.Instance;
            if (panel != null)
            {
                panel.NotifyExhibitClosed(this);
            }
        }

        private static string FormatMeta(ArchiveRecord record)
        {
            return "Date: " + (string.IsNullOrEmpty(record.date) ? "n/a" : record.date)
                   + "   |   Category: " + (string.IsNullOrEmpty(record.category) ? "n/a" : record.category)
                   + "   |   Type: " + record.type
                   + "   |   Language: " + (string.IsNullOrEmpty(record.language) ? "n/a" : record.language);
        }

        private static string FormatSource(ArchiveRecord record)
        {
            string label = record.isSampleData ? "SAMPLE/DEMO DATA" : "ARCHIVAL DATA";
            return "Source (" + label + "): " + (string.IsNullOrEmpty(record.source) ? "n/a" : record.source)
                   + "   |   Document ID: " + record.id;
        }
    }
}