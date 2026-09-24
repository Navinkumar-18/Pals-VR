using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AmbedkarHeritage.EditorTools
{
    /// <summary>
    /// One-shot Android build for Meta Quest.
    /// Run in batch mode via:
    ///   -executeMethod AmbedkarHeritage.EditorTools.QuestApkBuilder.BuildQuestApk
    /// </summary>
    public static class QuestApkBuilder
    {
        private const string ApkPath = "Builds/AmbedkarDigitalHeritage.apk";

        [MenuItem("Ambedkar Heritage/Build Quest APK")]
        public static void BuildQuestApk()
        {
            MainMuseumBuilder.BuildMainMuseum();

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            string[] scenes =
            {
                MainMuseumBuilder.ScenePath
            };

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[AmbedkarHeritage] APK build succeeded: " + ApkPath +
                          " (" + summary.totalSize + " bytes)");
                return;
            }

            Debug.LogError("[AmbedkarHeritage] APK build failed: " + summary.result);
            foreach (string error in report.steps.SelectMany(step => step.messages)
                         .Where(m => m.type == LogType.Error)
                         .Select(m => m.content))
            {
                Debug.LogError("[AmbedkarHeritage] " + error);
            }
        }
    }
}