using System.Text;
using UnityEngine;

namespace AmbedkarHeritage.VR
{
    /// <summary>
    /// Provides runtime status for headset and controller tracking, exposed so
    /// UI (and later hand/stage code) can react. Also publishes the currently
    /// selected exhibit id for the future AI guide/context-aware features.
    /// </summary>
    public sealed class VrSession : MonoBehaviour
    {
        public static VrSession Instance { get; private set; }

        public static string CurrentExhibitId { get; private set; }

        public static void SelectExhibit(string exhibitId)
        {
            CurrentExhibitId = exhibitId;
        }

        [SerializeField]
        private bool recenterOnStart = true;

        public bool HeadsetPresent { get; private set; }
        public bool LeftControllerPresent { get; private set; }
        public bool RightControllerPresent { get; private set; }

        private readonly StringBuilder _status = new StringBuilder(128);

        public string StatusText
        {
            get
            {
                _status.Length = 0;
                _status.Append("Headset: ").Append(HeadsetPresent ? "tracked" : "not detected");
                _status.Append(" | L: ").Append(LeftControllerPresent ? "ok" : "--");
                _status.Append(" | R: ").Append(RightControllerPresent ? "ok" : "--");
                _status.Append(" | Mode: ").Append(Core.MuseumApp.IsDemoMode ? "DEMO" : "LIVE");
                _status.Append(" | Data: ").Append(Core.ArchiveService.DataModeLabel);
                return _status.ToString();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (recenterOnStart && OVRManager.instance != null)
            {
                OVRManager.display.RecenterPose();
            }
        }

        private void Update()
        {
            HeadsetPresent = OVRManager.isHmdPresent;
            LeftControllerPresent =
                (OVRInput.GetConnectedControllers() & OVRInput.Controller.LTouch) != 0;
            RightControllerPresent =
                (OVRInput.GetConnectedControllers() & OVRInput.Controller.RTouch) != 0;
        }
    }
}