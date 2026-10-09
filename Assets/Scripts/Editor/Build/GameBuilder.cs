using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CrashRoyale.EditorTools
{
    /// <summary>
    /// Entry points. "Crash Royale/Rebuild Content and Scenes" regenerates every generated asset and scene from code;
    /// the Build menu items produce players. All of them also run headless:
    /// Unity -batchmode -quit -projectPath . -executeMethod CrashRoyale.EditorTools.GameBuilder.RebuildAll
    /// </summary>
    public static class GameBuilder
    {
        [MenuItem("Crash Royale/Rebuild Content and Scenes")]
        public static void RebuildAll()
        {
            foreach (var folder in new[] { Paths.Data, Paths.CardData, Paths.Skins, Paths.Resources, Paths.Prefabs, Paths.Scenes })
                EditorKit.EnsureFolder(folder);

            ContentBuilder.Build();
            BattleSceneBuilder.Build();
            MenuSceneBuilder.BuildAll();
            ProjectSetup.Apply();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Builder] Rebuild complete.");
        }

        /// <summary>Rebuild everything, then build the Windows player (one headless run instead of two).</summary>
        public static void RebuildAndBuildWindows()
        {
            RebuildAll();
            BuildWindows();
        }

        [MenuItem("Crash Royale/Build/Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/CrashRoyale.exe");

        [MenuItem("Crash Royale/Build/Android APK")]
        public static void BuildAndroid()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, "Builds/Android/CrashRoyale.apk");
        }

        [MenuItem("Crash Royale/Build/WebGL")]
        public static void BuildWebGL() => Build(BuildTarget.WebGL, "Builds/WebGL");

        private static void Build(BuildTarget target, string output)
        {
            ProjectSetup.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Builds");

            var options = new BuildPlayerOptions
            {
                scenes = ProjectSetup.Scenes,
                locationPathName = output,
                target = target,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Builder] {target}: {summary.result}, {summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalErrors} errors, {summary.totalTime}");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
