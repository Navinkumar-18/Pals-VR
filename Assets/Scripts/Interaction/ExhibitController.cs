using AmbedkarHeritage.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AmbedkarHeritage.Interaction
{
    /// <summary>
    /// Reusable data-driven exhibit. Every exhibit instance binds to one
    /// ArchiveRecord (from the demo archive today, from the backend API later)
    /// and shows an information panel when activated. No custom scripts are
    /// needed per exhibit.
    /// </summary>
    [RequireComponent(typeof(OVRGrabbable))]
    public sealed class ExhibitController : MonoBehaviour
    {
        [SerializeField]
        private string archiveRecordId;

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

        public string RecordId
        {
            get { return _record != null ? _record.id : archiveRecordId; }
        }

        public ArchiveRecord Record
        {
            get { return _record; }
        }

        private void Awake()
        {
            _record = DemoArchiveLoader.Instance.Find(archiveRecordId);
            if (_record == null && !string.IsNullOrEmpty(archiveRecordId))
            {
                Debug.LogWarning("[AmbedkarHeritage] Exhibit bound to unknown record id: " + archiveRecordId);
            }

            SetActive(false);
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

        private void SetActive(bool active)
        {
            _isActive = active;
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

                VR.VrSession.SelectExhibit(_record.id);
            }
        }

        private static string FormatMeta(ArchiveRecord record)
        {
            return "Date: " + (string.IsNullOrEmpty(record.date) ? "n/a" : record.date)
                   + "   |   Category: " + record.category
                   + "   |   Type: " + record.type
                   + "   |   Language: " + record.language;
        }

        private static string FormatSource(ArchiveRecord record)
        {
            string label = record.isSampleData ? "SAMPLE/DEMO DATA" : "ARCHIVAL DATA";
            return "Source (" + label + "): " + (string.IsNullOrEmpty(record.source) ? "n/a" : record.source)
                   + "   |   Document ID: " + record.id;
        }
    }
}