using System;
using System.Collections.Generic;
using UnityEngine;

namespace AmbedkarHeritage.Core
{
    /// <summary>
    /// Demo/offline archive loader. Loads sample data from Resources so the
    /// prototype runs without a backend. All records are flagged isSampleData.
    /// This is a stand-in for the institutional backend archive.
    /// </summary>
    public sealed class DemoArchiveLoader
    {
        private const string SampleResourcePath = "Data/sample_archive";

        private static DemoArchiveLoader s_instance;

        private readonly List<ArchiveRecord> _records = new List<ArchiveRecord>();
        private Dictionary<string, ArchiveRecord> _byId;

        public static DemoArchiveLoader Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new DemoArchiveLoader();
                    s_instance.Load();
                }

                return s_instance;
            }
        }

        public IReadOnlyList<ArchiveRecord> Records
        {
            get { return _records; }
        }

        public bool LoadedFromJson { get; private set; }

        public void Load()
        {
            _records.Clear();
            _byId = null;

            TextAsset asset = Resources.Load<TextAsset>(SampleResourcePath);
            if (asset != null)
            {
                try
                {
                    DemoArchiveData data = JsonUtility.FromJson<DemoArchiveData>(asset.text);
                    if (data != null && data.config != null)
                    {
                        MuseumApp.ApplyConfig(data.config);
                    }

                    if (data != null && data.records != null)
                    {
                        foreach (ArchiveRecord record in data.records)
                        {
                            if (record != null)
                            {
                                record.EnsureSafeDefaults();
                            }

                            if (record != null && !string.IsNullOrEmpty(record.id))
                            {
                                _records.Add(record);
                            }
                        }

                        LoadedFromJson = true;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError("[AmbedkarHeritage] Failed to parse sample archive: " + exception.Message);
                }
            }

            if (!LoadedFromJson)
            {
                LoadBuiltInSamples();
            }

            _byId = new Dictionary<string, ArchiveRecord>();
            foreach (ArchiveRecord record in _records)
            {
                if (!_byId.ContainsKey(record.id))
                {
                    _byId.Add(record.id, record);
                }
            }
        }

        public ArchiveRecord Find(string id)
        {
            if (string.IsNullOrEmpty(id) || _byId == null)
            {
                return null;
            }

            _byId.TryGetValue(id, out ArchiveRecord record);
            return record;
        }

        /// <summary>
        /// Small built-in fallback (clearly marked SAMPLE) used only when the
        /// JSON resource is missing.
        /// </summary>
        private void LoadBuiltInSamples()
        {
            _records.Clear();

            _records.Add(new ArchiveRecord
            {
                id = "DEMO-CFG",
                type = ArchiveRecordType.Event,
                title = "[SAMPLE] Museum Configuration Record",
                description = "Built-in fallback demo record. Install Resources/Data/sample_archive.json to replace this data.",
                date = "",
                category = "System",
                source = "demo-fallback",
                language = "en",
                isSampleData = true
            });
        }
    }
}