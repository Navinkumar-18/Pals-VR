using System.Collections.Generic;
using AmbedkarHeritage.Core;

namespace AmbedkarHeritage.Interaction
{
    /// <summary>
    /// Registry of every exhibit currently in the scene. Powers the category
    /// filter (show/hide stations) and related-record navigation. The registry
    /// is populated by the room builder and is purely data-driven — exhibits
    /// never know about each other.
    /// </summary>
    public static class ExhibitRegistry
    {
        private static readonly List<ExhibitController> s_exhibits = new List<ExhibitController>();

        public static ArchiveCategory CurrentCategory { get; private set; } = ArchiveCategory.All;

        public static IReadOnlyList<ExhibitController> All
        {
            get { return s_exhibits; }
        }

        public static void Register(ExhibitController exhibit)
        {
            if (exhibit != null && !s_exhibits.Contains(exhibit))
            {
                s_exhibits.Add(exhibit);
            }
        }

        public static void Unregister(ExhibitController exhibit)
        {
            if (exhibit != null)
            {
                s_exhibits.Remove(exhibit);
            }
        }

        public static ExhibitController Find(string recordId)
        {
            if (string.IsNullOrEmpty(recordId))
            {
                return null;
            }

            for (int i = 0; i < s_exhibits.Count; i++)
            {
                if (s_exhibits[i] != null && s_exhibits[i].RecordBoundId == recordId)
                {
                    return s_exhibits[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Shows only the stations that belong to the selected category.
        /// Uncategorized records (e.g. timeline events) appear only under ALL.
        /// </summary>
        public static void ApplyCategory(ArchiveCategory category)
        {
            CurrentCategory = category;

            for (int i = 0; i < s_exhibits.Count; i++)
            {
                ExhibitController exhibit = s_exhibits[i];
                if (exhibit == null)
                {
                    continue;
                }

                bool match = category == ArchiveCategory.All || exhibit.Bucket == category;
                exhibit.gameObject.SetActive(match);

                if (!match && ArchiveInfoPanel.Instance != null && ArchiveInfoPanel.Instance.IsShowing(exhibit))
                {
                    ArchiveInfoPanel.Instance.Close(exhibit);
                }
            }
        }

        public static List<ArchiveRecord> ResolveRelated(ArchiveRecord record)
        {
            List<ArchiveRecord> related = new List<ArchiveRecord>();
            if (record == null || record.relatedIds == null)
            {
                return related;
            }

            for (int i = 0; i < record.relatedIds.Count; i++)
            {
                ArchiveRecord found = DemoArchiveLoader.Instance.Find(record.relatedIds[i]);
                if (found != null)
                {
                    related.Add(found);
                }
            }

            return related;
        }

        /// <summary>Opens the archive info panel for the record (station or virtual).</summary>
        public static void ShowRecord(ArchiveRecord record)
        {
            if (record == null || ArchiveInfoPanel.Instance == null)
            {
                return;
            }

            ArchiveInfoPanel.Instance.Open(record, Find(record.id));
        }
    }
}