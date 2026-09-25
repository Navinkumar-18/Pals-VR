using System;
using System.Collections.Generic;
using AmbedkarHeritage.Networking;

namespace AmbedkarHeritage.Core
{
    /// <summary>Data-mode of the active archive provider.</summary>
    public enum ArchiveDataMode
    {
        DemoLocal,          // IsDemoMode == true → local sample JSON
        ApiOnline,          // backend reachable, serving records
        ApiOfflineFallback  // backend unreachable → auto-fallback to demo data
    }

    /// <summary>
    /// Central archive data hub. Exhibit/registry/UI code resolves records
    /// through <see cref="Find"/> / <see cref="All"/> and never touches a
    /// provider directly, so switching DEMO &lt;-&gt; API mode (or falling back
    /// offline) requires no interaction-code changes.
    ///
    /// Startup: <see cref="Initialize"/> picks the provider from
    /// <see cref="MuseumApp.IsDemoMode"/>. In API mode a failed fetch flips to
    /// the local demo provider and exposes "Offline Demo Mode" via
    /// <see cref="DataModeLabel"/> (surfaced in the VR status readout).
    /// </summary>
    public static class ArchiveService
    {
        private static bool _initialized;
        private static bool _forceReinit;

        static ArchiveService()
        {
            Api.Changed += OnApiChanged;
        }

        public static ArchiveDataMode Mode { get; private set; } = ArchiveDataMode.DemoLocal;

        public static bool IsOfflineFallback
        {
            get { return Mode == ArchiveDataMode.ApiOfflineFallback; }
        }

        public static IArchiveDataProvider Current { get; private set; }

        public static LocalArchiveDataProvider Local { get; } = new LocalArchiveDataProvider();

        public static ApiArchiveDataProvider Api { get; } = new ApiArchiveDataProvider();

        /// <summary>Raised when the active provider / mode changes.</summary>
        public static event Action ModeChanged;

        /// <summary>Raised when new records become available or the provider
        /// switches (exhibits re-resolve on this).</summary>
        public static event Action DataChanged;

        public static string DataModeLabel
        {
            get
            {
                switch (Mode)
                {
                    case ArchiveDataMode.ApiOnline: return "API";
                    case ArchiveDataMode.ApiOfflineFallback: return "OFFLINE-DEMO";
                    default: return "DEMO";
                }
            }
        }

        public static IReadOnlyList<ArchiveRecord> All
        {
            get { return Current != null ? Current.AllRecords : new List<ArchiveRecord>(); }
        }

        public static ArchiveRecord Find(string id)
        {
            return Current != null ? Current.Find(id) : null;
        }

        /// <summary>Idempotent startup hook (called from Bootstrap).</summary>
        public static void Initialize()
        {
            if (_initialized && !_forceReinit)
            {
                return;
            }

            _initialized = true;
            _forceReinit = false;

            if (MuseumApp.IsDemoMode || string.IsNullOrEmpty(MuseumApp.ApiBaseUrl))
            {
                ActivateLocal();
            }
            else
            {
                ActivateApi();
            }
        }

        /// <summary>Ensure a provider is selected (safe from editors/tools).</summary>
        public static void EnsureReady()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        /// <summary>Re-run provider selection (used by tests/editor tools after
        /// changing MuseumApp.IsDemoMode or ApiBaseUrl).</summary>
        public static void ForceReinitialize()
        {
            _forceReinit = true;
            _initialized = false;
            Initialize();
        }

        /// <summary>Try to reach the backend again after an offline fallback.</summary>
        public static void RetryOnline()
        {
            if (MuseumApp.IsDemoMode || string.IsNullOrEmpty(MuseumApp.ApiBaseUrl))
            {
                return;
            }

            Api.ClearState();
            Current = Api;
            Mode = ArchiveDataMode.ApiOnline;
            ModeChanged?.Invoke();
            Api.Load();
        }

        public static void ActivateLocal()
        {
            Current = Local;
            Local.Load();
            Mode = ArchiveDataMode.DemoLocal;
            ModeChanged?.Invoke();
            DataChanged?.Invoke();
        }

        public static void ActivateApi()
        {
            Current = Api;
            Mode = ArchiveDataMode.ApiOnline;
            ModeChanged?.Invoke();
            Api.Load();
        }

        private static void OnApiChanged()
        {
            if (Api.IsReady)
            {
                if (Current != Api)
                {
                    Current = Api;
                }

                Mode = ArchiveDataMode.ApiOnline;
                DataChanged?.Invoke();
                return;
            }

            if (Api.IsLoading || Mode == ArchiveDataMode.ApiOfflineFallback)
            {
                return; // fetch in flight, or already fallen back
            }

            // Fetch failed: offline demo fallback, do not crash the VR app.
            Mode = ArchiveDataMode.ApiOfflineFallback;
            Current = Local;
            Local.Load();
            UnityEngine.Debug.LogWarning(
                "[AmbedkarHeritage] Backend unreachable (" + (Api.LastError ?? "unknown") +
                ") — falling back to OFFLINE DEMO MODE.");
            ModeChanged?.Invoke();
            DataChanged?.Invoke();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor/batch-only synchronous probe of the backend. On success the
        /// API provider is primed with the fetched records and activated; on
        /// failure the offline fallback is engaged. Returns whether the API is
        /// now serving.
        /// </summary>
        public static bool ProbeOnlineSync(out string error, float timeoutSeconds = 20f)
        {
            error = null;
            if (MuseumApp.IsDemoMode || string.IsNullOrEmpty(MuseumApp.ApiBaseUrl))
            {
                error = "Skipped (demo mode or empty ApiBaseUrl)";
                return false;
            }

            List<ArchiveRecord> records;
            if (!ArchiveApiClient.TryGetDocumentsSyncEditor(MuseumApp.ApiBaseUrl, out records, out error, timeoutSeconds))
            {
                Mode = ArchiveDataMode.ApiOfflineFallback;
                Current = Local;
                Local.Load();
                ModeChanged?.Invoke();
                DataChanged?.Invoke();
                return false;
            }

            Api.ReplaceAll(records);
            Current = Api;
            Mode = ArchiveDataMode.ApiOnline;
            ModeChanged?.Invoke();
            DataChanged?.Invoke();
            return true;
        }
#endif
    }
}