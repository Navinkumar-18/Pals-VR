using System;
using System.Collections.Generic;
using AmbedkarHeritage.Core;

namespace AmbedkarHeritage.Networking
{
    /// <summary>
    /// API-backed archive provider. Fetches PUBLISHED documents from the
    /// backend into an in-memory cache (the demo dataset is small; later
    /// phases can page). <see cref="Find"/> serves from the cache so
    /// exhibit code stays synchronous. Raises <see cref="Changed"/> when the
    /// fetch completes or fails so <see cref="ArchiveService"/> can switch to
    /// the offline fallback without any exhibit code changing.
    /// </summary>
    public sealed class ApiArchiveDataProvider : IArchiveDataProvider
    {
        private readonly List<ArchiveRecord> _records = new List<ArchiveRecord>();
        private Dictionary<string, ArchiveRecord> _byId = new Dictionary<string, ArchiveRecord>();
        private bool _loaded;
        private bool _loading;

        public string ProviderName
        {
            get { return "api"; }
        }

        public bool IsReady
        {
            get { return _loaded; }
        }

        public bool IsLoading
        {
            get { return _loading; }
        }

        public string LastError { get; private set; }

        /// <summary>Raised after a fetch attempt (success or failure).</summary>
        public event Action Changed;

        public IReadOnlyList<ArchiveRecord> AllRecords
        {
            get { return _records; }
        }

        public ArchiveRecord Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            ArchiveRecord record;
            return _byId.TryGetValue(id, out record) ? record : null;
        }

        public void Load()
        {
            FetchAll();
        }

        public void FetchAll()
        {
            if (_loading)
            {
                return;
            }

            _loading = true;
            ArchiveApiClient.GetDocuments(MuseumApp.ApiBaseUrl, OnDocumentsFetched);
        }

        /// <summary>Replace the cache (used by the editor probe/sync path and
        /// for tests).</summary>
        public void ReplaceAll(List<ArchiveRecord> records)
        {
            _records.Clear();
            if (records != null)
            {
                _records.AddRange(records);
            }

            _byId = new Dictionary<string, ArchiveRecord>();
            foreach (ArchiveRecord record in _records)
            {
                if (record != null && !string.IsNullOrEmpty(record.id))
                {
                    _byId[record.id] = record;
                }
            }

            _loaded = true;
            Changed?.Invoke();
        }

        public void ClearState()
        {
            _loaded = false;
            _loading = false;
            LastError = null;
            _byId.Clear();
            _records.Clear();
        }

        private void OnDocumentsFetched(List<ArchiveRecord> records, string error)
        {
            _loading = false;
            if (error != null)
            {
                LastError = error;
                Changed?.Invoke();
                return;
            }

            _records.Clear();
            if (records != null)
            {
                _records.AddRange(records);
            }

            _byId = new Dictionary<string, ArchiveRecord>();
            foreach (ArchiveRecord record in _records)
            {
                if (record != null && !string.IsNullOrEmpty(record.id))
                {
                    _byId[record.id] = record;
                }
            }

            _loaded = true;
            LastError = null;
            Changed?.Invoke();
        }
    }
}