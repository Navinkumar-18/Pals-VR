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
            DemoArchiveLoader.Instance.Load();

            ApplyConfigToScene();

            Debug.Log("[AmbedkarHeritage] Bootstrap complete. AppName=" + MuseumApp.AppName
                      + " | Mode=" + (MuseumApp.IsDemoMode ? "DEMO" : "LIVE"));
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