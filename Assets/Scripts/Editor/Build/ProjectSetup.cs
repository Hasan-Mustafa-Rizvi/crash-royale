using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrashRoyale.EditorTools
{
    /// <summary>Player settings, scene list and renderer settings.</summary>
    internal static class ProjectSetup
    {
        public static readonly string[] Scenes =
        {
            $"{Paths.Scenes}/MainMenu.unity",
            $"{Paths.Scenes}/Battle.unity",
            $"{Paths.Scenes}/Deck.unity",
            $"{Paths.Scenes}/Shop.unity"
        };

        public static void Apply()
        {
            PlayerSettings.companyName = "Hasan Mustafa Rizvi";
            PlayerSettings.productName = "Crash Royale";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.hasanrizvi.crashroyale");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.hasanrizvi.crashroyale");
            PlayerSettings.Android.bundleVersionCode = 2;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultWebScreenWidth = 540;
            PlayerSettings.defaultWebScreenHeight = 960;
            PlayerSettings.runInBackground = false;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Paths.Branding}/AppIcon.png");
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            var scenes = new EditorBuildSettingsScene[Scenes.Length];
            for (int i = 0; i < Scenes.Length; i++) scenes[i] = new EditorBuildSettingsScene(Scenes[i], true);
            EditorBuildSettings.scenes = scenes;

            ConfigureRenderer();
            ConfigureTroopLayer();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Names the troop layer and turns off troop-on-troop physics (troops push each other softly in code).</summary>
        private static void ConfigureTroopLayer()
        {
            var tagManager = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            if (tagManager != null)
            {
                var so = new SerializedObject(tagManager);
                var layers = so.FindProperty("layers");
                if (layers != null && layers.arraySize > Battle.Unit.TroopLayer)
                {
                    layers.GetArrayElementAtIndex(Battle.Unit.TroopLayer).stringValue = "Troops";
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Physics2D.IgnoreLayerCollision(Battle.Unit.TroopLayer, Battle.Unit.TroopLayer, true);
        }

        /// <summary>Sort sprites by Y so troops further up the screen draw behind the ones below them.</summary>
        private static void ConfigureRenderer()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Settings" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null || asset.GetType().Name != "Renderer2DData") continue;

                var so = new SerializedObject(asset);
                var mode = so.FindProperty("m_TransparencySortMode");
                var axis = so.FindProperty("m_TransparencySortAxis");
                if (mode != null) mode.enumValueIndex = (int)TransparencySortMode.CustomAxis;
                if (axis != null) axis.vector3Value = new Vector3(0f, 1f, 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                Debug.Log($"[Builder] Y-sorting enabled on {path}");
            }

            GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
            GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, 0f);
        }

        public static void WriteTextFile(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, contents);
        }
    }
}
