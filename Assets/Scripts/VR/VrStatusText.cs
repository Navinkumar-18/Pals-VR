using UnityEngine;
using UnityEngine.UI;

namespace AmbedkarHeritage.VR
{
    /// <summary>
    /// Mirrors VrSession status onto a world-space uGUI text so headset and
    /// controller tracking state is visible inside the headset during the
    /// Phase 1 milestone.
    /// </summary>
    public sealed class VrStatusText : MonoBehaviour
    {
        [SerializeField]
        private Text statusLabel;

        private void Update()
        {
            if (statusLabel != null && VrSession.Instance != null)
            {
                statusLabel.text = VrSession.Instance.StatusText;
            }
        }
    }
}