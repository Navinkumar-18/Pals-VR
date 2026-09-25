using UnityEngine;

namespace AmbedkarHeritage.Interaction
{
    /// <summary>
    /// Ray-based exhibit interaction for one controller. Points from the hand
    /// forward, highlights the hovered exhibit, and toggles its info panel when
    /// the index trigger is pressed. Designed so the ray source can later be
    /// swapped for a hand-tracking anchor without changing exhibit code.
    /// </summary>
    public sealed class ExhibitRayHighlighter : MonoBehaviour
    {
        [SerializeField]
        private float reachDistance = 3f;

        [SerializeField]
        private LayerMask exhibitMask = ~0;

        private OVRInput.Controller _controller = OVRInput.Controller.RTouch;

        private ExhibitController _hovered;
        private bool _wasPressed;

        public void Configure(OVRInput.Controller controller)
        {
            _controller = controller;
        }

        private void Update()
        {
            // Give UI buttons priority: while any pointer rests on a button,
            // do not hover/toggle exhibits behind the panels.
            if (UIPointer.IsAnyOverUI)
            {
                if (_hovered != null)
                {
                    _hovered.SetHighlight(false);
                    _hovered = null;
                }

                _wasPressed = false;
                return;
            }

            ExhibitController hit = null;

            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit rayHit, reachDistance, exhibitMask, QueryTriggerInteraction.Ignore))
            {
                hit = rayHit.collider.GetComponentInParent<ExhibitController>();
            }

            if (hit != _hovered)
            {
                if (_hovered != null)
                {
                    _hovered.SetHighlight(false);
                }

                _hovered = hit;
                if (_hovered != null)
                {
                    _hovered.SetHighlight(true);
                }
            }

            bool pressed = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, _controller);
            if (pressed && !_wasPressed && _hovered != null)
            {
                _hovered.Toggle();
            }

            _wasPressed = pressed;
        }
    }
}