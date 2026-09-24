using UnityEngine;

namespace AmbedkarHeritage.Core
{
    /// <summary>
    /// Runtime session state for the museum app. Consumed by VR, exhibits,
    /// networking and (later) the AI guide so the whole app shares one config.
    /// </summary>
    public static class MuseumApp
    {
        public static string AppName { get; private set; } = "AMBEDKAR DIGITAL HERITAGE";
        public static string WelcomeMessage { get; private set; } =
            "Welcome to the Ambedkar Digital Heritage Museum.";
        public static string[] SupportedLanguages { get; private set; } = { "en" };

        public static bool UseDemoMode { get; set; } = true;
        public static string ApiBaseUrl { get; set; } = "http://127.0.0.1:8000/api";

        public static void ApplyConfig(MuseumConfig config)
        {
            if (config == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(config.appName))
            {
                AppName = config.appName;
            }

            if (!string.IsNullOrEmpty(config.welcomeMessage))
            {
                WelcomeMessage = config.welcomeMessage;
            }

            if (config.supportedLanguages != null && config.supportedLanguages.Count > 0)
            {
                SupportedLanguages = config.supportedLanguages.ToArray();
            }
        }
    }
}