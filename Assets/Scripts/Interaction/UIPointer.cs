using AmbedkarHeritage.UI;
using UnityEngine;

namespace AmbedkarHeritage.Interaction
{
    /// <summary>
    /// Hand-ray pointer for VR push buttons (VRButton). Mirrors the existing
    /// ExhibitRayHighlighter pattern: ray from the controller forward, hover
    /// feedback, primary index trigger confirms. No EventSystem required.
    /// Exposes whether any pointer currently rests on UI so the exhibit
    /// highlighter can yield priority to buttons.
    /// </summary>
    public sealed class UIPointer : MonoBehaviour
    {
        private static readonly bool[] s_overButton = new bool[2];

        [SerializeField]
        private float reachDistance = 3f;

        [SerializeField]
        private LayerMask hitMask = ~0;

        private OVRInput.Controller _controller = OVRInput.Controller.RTouch;
        private VRButton _hovered;
        private bool _wasPressed;

        public static bool IsAnyOverUI
        {
            get { return s_overButton[0] || s_overButton[1]; }
        }

        public void Configure(OVRInput.Controller controller)
        {
            _controller = controller;
        }

        private void Update()
        {
            int slot = _controller == OVRInput.Controller.LTouch ? 0 : 1;

            VRButton hit = null;
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit rayHit, reachDistance, hitMask,
                QueryTriggerInteraction.Ignore))
            {
                hit = rayHit.collider.GetComponentInParent<VRButton>();
            }

            s_overButton[slot] = hit != null;

            if (hit != _hovered)
            {
                if (_hovered != null)
                {
                    _hovered.SetHover(false);
                }

                _hovered = hit;
                if (_hovered != null)
                {
                    _hovered.SetHover(true);
                }
            }

            bool pressed = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, _controller);
            if (pressed && !_wasPressed && _hovered != null)
            {
                _hovered.Confirm();
            }

            _wasPressed = pressed;
        }

        private void OnDestroy()
        {
            s_overButton[_controller == OVRInput.Controller.LTouch ? 0 : 1] = false;
            if (_hovered != null)
            {
                _hovered.SetHover(false);
                _hovered = null;
            }
        }
    }
}