using AmbedkarHeritage.Core;
using AmbedkarHeritage.Interaction;
using UnityEngine;

namespace AmbedkarHeritage.UI
{
    /// <summary>
    /// VR category navigation bar. Exposes the seven archive buckets
    /// (ALL / MANUSCRIPTS / BOOKS / DOCUMENTS / PHOTOGRAPHS / SPEECHES / VIDEOS)
    /// and filters the exhibit stations in the room via ExhibitRegistry.
    /// </summary>
    public sealed class ArchiveCategoryFilter : MonoBehaviour
    {
        public static ArchiveCategoryFilter Instance { get; private set; }

        private VRButton[] _buttons;
        private ArchiveCategory[] _categories;

        public int ButtonCount
        {
            get { return _buttons != null ? _buttons.Length : 0; }
        }

        public ArchiveCategory ActiveCategory { get; private set; } = ArchiveCategory.All;

        private void Awake()
        {
            Instance = this;
        }

        public void Configure(VRButton[] buttons, ArchiveCategory[] categories)
        {
            _buttons = buttons;
            _categories = categories;

            if (_buttons != null)
            {
                for (int i = 0; i < _buttons.Length && i < _categories.Length; i++)
                {
                    int captured = i;
                    _buttons[i].SetAction(() => SetActiveCategory(_categories[captured]));
                }
            }

            SetActiveCategory(ArchiveCategory.All);
        }

        public void SetActiveCategory(ArchiveCategory category)
        {
            ActiveCategory = category;
            ExhibitRegistry.ApplyCategory(category);

            if (_buttons == null)
            {
                return;
            }

            for (int i = 0; i < _buttons.Length && i < _categories.Length; i++)
            {
                bool active = _categories[i] == category;
                _buttons[i].SetColor(active ? new Color(0.75f, 0.55f, 0.1f, 1f) : new Color(0.22f, 0.28f, 0.36f, 1f));
            }
        }
    }
}