using UnityEngine;

namespace AmbedkarHeritage.Core
{
    /// <summary>
    /// Single runtime entry point. Loads data-driven config, applies it to the
    /// scene, and guarantees an OVRManager exists so head/controller tracking
    /// works even if the scene was built without one.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        public static Bootstrap Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            EnsureOvrManager();
            // Local demo JSON is ALWAYS loaded once: it carries MuseumApp
            // config (app name, welcome message) and the offline dataset.
            DemoArchiveLoader.Instance.Load();

            // Select the archive provider (DEMO vs API) and start loading
            // records; falls back to "Offline Demo Mode" if the API is
            // unreachable. Exhibit interaction code is provider-agnostic.
            ArchiveService.Initialize();

            ApplyConfigToScene();

            Debug.Log("[AmbedkarHeritage] Bootstrap complete. AppName=" + MuseumApp.AppName
                      + " | Mode=" + (MuseumApp.IsDemoMode ? "DEMO" : "LIVE")
                      + " | Data=" + ArchiveService.DataModeLabel);
        }

        private static void EnsureOvrManager()
        {
            OVRManager manager = FindObjectOfType<OVRManager>();
            if (manager == null)
            {
                GameObject go = new GameObject("OVRManager");
                manager = go.AddComponent<OVRManager>();
            }

            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
        }

        private void ApplyConfigToScene()
        {
            foreach (UI.EntranceSign sign in FindObjectsOfType<UI.EntranceSign>())
            {
                sign.Apply(MuseumApp.AppName, MuseumApp.WelcomeMessage);
            }
        }
    }
}