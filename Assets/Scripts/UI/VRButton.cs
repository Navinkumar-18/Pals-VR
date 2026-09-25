using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AmbedkarHeritage.UI
{
    /// <summary>
    /// Minimal VR push-button. No EventSystem or OVR input module is required:
    /// a paired UIPointer ray hits this button's collider and calls Confirm(),
    /// which forwards to the wrapped Button.onClick. Keeps UI interaction
    /// working on Quest and in the desktop editor preview without extra
    /// input-module wiring.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class VRButton : MonoBehaviour
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private Text label;

        [SerializeField]
        private Image background;

        private Color _defaultColor = Color.white;
        private string _text = "";
        private bool _hovered;

        public string Label
        {
            get { return _text; }
        }

        public void Configure(Button uiButton, Text uiLabel, Image bg)
        {
            button = uiButton;
            label = uiLabel;
            background = bg;
            _text = label != null ? label.text : "";
            _defaultColor = background != null ? background.color : Color.white;
        }

        public void SetLabel(string value)
        {
            _text = value;
            if (label != null)
            {
                label.text = value;
            }
        }

        public void SetAction(UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(action);
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (background != null)
            {
                background.color = interactable ? _defaultColor : new Color(0.35f, 0.35f, 0.38f, 1f);
            }
        }

        public void SetHover(bool hovered)
        {
            if (_hovered == hovered || background == null)
            {
                return;
            }

            _hovered = hovered;
            if (hovered)
            {
                background.color = Color.Lerp(_defaultColor, Color.white, 0.55f);
            }
            else
            {
                background.color = _defaultColor;
            }
        }

        /// <summary>Rebrands the button (used by the filter bar to mark the active category).</summary>
        public void SetColor(Color color)
        {
            _defaultColor = color;
            if (background != null)
            {
                background.color = color;
            }
        }

        /// <summary>Invoked by the ray pointer. Calls the wrapped button's onClick.</summary>
        public void Confirm()
        {
            if (button != null && button.IsInteractable())
            {
                button.onClick.Invoke();
            }
        }

        private void Reset()
        {
            BoxCollider collider = GetComponent<BoxCollider>();
            collider.size = new Vector3(0.3f, 0.12f, 0.02f);
        }
    }
}