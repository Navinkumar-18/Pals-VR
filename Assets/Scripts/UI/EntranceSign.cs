using UnityEngine;
using UnityEngine.UI;

namespace AmbedkarHeritage.UI
{
    /// <summary>
    /// Entrance sign shown on entry. Text is supplied from data (MuseumApp),
    /// never hard-coded.
    /// </summary>
    public sealed class EntranceSign : MonoBehaviour
    {
        [SerializeField]
        private Text titleText;

        [SerializeField]
        private Text welcomeText;

        public void Apply(string appName, string welcomeMessage)
        {
            if (titleText != null)
            {
                titleText.text = appName;
            }

            if (welcomeText != null)
            {
                welcomeText.text = welcomeMessage;
            }

            gameObject.SetActive(true);
        }
    }
}