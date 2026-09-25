using System.Collections.Generic;

namespace AmbedkarHeritage.Core
{
    /// <summary>
    /// Offline demo provider backed by <see cref="DemoArchiveLoader"/> and
    /// Resources/Data/sample_archive.json. Used for DEMO mode and as the
    /// automatic fallback when the API backend is unreachable.
    /// </summary>
    public sealed class LocalArchiveDataProvider : IArchiveDataProvider
    {
        private readonly DemoArchiveLoader _loader = DemoArchiveLoader.Instance;

        public string ProviderName
        {
            get { return "local-demo"; }
        }

        public bool IsReady
        {
            get { return _loader.LoadedFromJson && _loader.Records.Count > 0; }
        }

        public IReadOnlyList<ArchiveRecord> AllRecords
        {
            get { return _loader.Records; }
        }

        public ArchiveRecord Find(string id)
        {
            return _loader.Find(id);
        }

        public void Load()
        {
            _loader.Load();
        }
    }
}