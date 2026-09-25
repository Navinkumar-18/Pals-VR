using System.Collections.Generic;

namespace AmbedkarHeritage.Core
{
    /// <summary>
    /// Common contract for archive data sources. Today: the local demo JSON
    /// (offline) and the FastAPI backend (REST). Later: graph / knowledge-base
    /// sources. Exhibit code only ever talks to <see cref="ArchiveService"/>,
    /// never to a concrete provider, so switching DEMO &lt;-&gt; API mode does
    /// not change interaction code.
    /// </summary>
    public interface IArchiveDataProvider
    {
        /// <summary>Stable identifier, e.g. "local-demo" or "api".</summary>
        string ProviderName { get; }

        /// <summary>True once records are available for lookup.</summary>
        bool IsReady { get; }

        /// <summary>All currently loaded records (live view).</summary>
        IReadOnlyList<ArchiveRecord> AllRecords { get; }

        /// <summary>Find a record by its stable archival id, or null.</summary>
        ArchiveRecord Find(string id);

        /// <summary>Start loading/populating records (async providers kick a
        /// fetch; callers use <see cref="ArchiveService.DataChanged"/> to be
        /// notified when they become available).</summary>
        void Load();
    }
}