using System.IO;
using System.Linq;
using EstudiApp.Core;
using EstudiApp.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.EditorTools
{
    /// <summary>Crea la escena principal (cámara, interfaz y bootstrap) y deja los ajustes del proyecto listos.</summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string Root = "Assets/_Project";
        public const string ScenePath = Root + "/Scenes/Main.unity";
        private const string OpenedKey = "EstudiApp.MainSceneOpened";

        static ProjectSetup()
        {
            EditorApplication.delayCall += OpenMainSceneOnFirstLoad;
        }

        private static void OpenMainSceneOnFirstLoad()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(OpenedKey, false))
                return;
            SessionState.SetBool(OpenedKey, true);
            if (!File.Exists(ScenePath))
                CreateMainScene();
            else if (EditorSceneManager.GetActiveScene().path != ScenePath && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Para -executeMethod: crea la escena si falta y configura el reproductor.</summary>
        public static void Batch()
        {
            ConfigurePlayer();
            if (!File.Exists(ScenePath))
                CreateMainScene();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("EstudiApp/Crear escena principal", priority = 0)]
        public static void CreateMainScene()
        {
            if (!AssetDatabase.IsValidFolder(Root + "/Scenes"))
                AssetDatabase.CreateFolder(Root, "Scenes");
            var panelSettings = EnsurePanelSettings();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0xF6, 0xF1, 0xE7, 0xFF);
            camera.cullingMask = 0;
            camera.orthographic = true;

            var uiGo = new GameObject("UI");
            var document = uiGo.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var shell = uiGo.AddComponent<AppShell>();

            var bootstrap = new GameObject("App").AddComponent<AppBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("appShell").objectReferenceValue = shell;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[EstudiApp] Escena creada en " + ScenePath);
        }

        private static PanelSettings EnsurePanelSettings()
        {
            var path = $"{Root}/Resources/{AppShell.PanelSettingsResource}.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (settings != null)
                return settings;
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 800);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.clearColor = true;
            settings.colorClearValue = new Color32(0xF6, 0xF1, 0xE7, 0xFF);
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>($"{Root}/Resources/UI/RuntimeTheme.tss");
            if (settings.themeStyleSheet == null)
                Debug.LogWarning("[EstudiApp] No se encontró RuntimeTheme.tss: asigna un Theme Style Sheet a PanelSettings.");
            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }

        [MenuItem("EstudiApp/Configurar reproductor", priority = 1)]
        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "EstudiApp";
            PlayerSettings.productName = AppConfig.AppName;
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "1.0")
                PlayerSettings.bundleVersion = "0.1.0";
            foreach (var target in new[] { UnityEditor.Build.NamedBuildTarget.Standalone, UnityEditor.Build.NamedBuildTarget.Android, UnityEditor.Build.NamedBuildTarget.iOS })
                PlayerSettings.SetApplicationIdentifier(target, "com.estudiapp.mtc");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            ConfigureIcons();

            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
        }

        /// <summary>Iconos de Tools/branding/make_icon.py: a sangre para móvil, con forma redondeada para macOS.</summary>
        private static void ConfigureIcons()
        {
            var square = LoadIcon(Root + "/Branding/AppIcon.png");
            var mac = LoadIcon(Root + "/Branding/AppIcon-mac.png");
            if (square == null)
                return;
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { square }, UnityEditor.IconKind.Any);
            ConfigureAndroidIcons(square);
            if (mac == null)
                return;
            var standalone = UnityEditor.Build.NamedBuildTarget.Standalone;
            var sizes = PlayerSettings.GetIconSizes(standalone, UnityEditor.IconKind.Any);
            PlayerSettings.SetIcons(standalone, Enumerable.Repeat(mac, sizes.Length).ToArray(), UnityEditor.IconKind.Any);
        }

        /// <summary>
        /// Android 8+ usa el icono adaptativo (fondo + emblema dentro de la zona segura); sin él Unity pone uno en blanco.
        /// Los tipos se buscan por nombre para no depender del módulo de Android al compilar.
        /// </summary>
        private static void ConfigureAndroidIcons(Texture2D square)
        {
            var background = LoadIcon(Root + "/Branding/AppIcon-android-bg.png");
            var foreground = LoadIcon(Root + "/Branding/AppIcon-android-fg.png");
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(android))
            {
                var name = kind.ToString();
                Texture2D[] layers;
                if (name == "Adaptive" && background != null && foreground != null)
                    layers = new[] { background, foreground };
                else if (name == "Legacy" || name == "Round")
                    layers = new[] { square };
                else
                    continue;
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (var icon in icons)
                    icon.SetTextures(layers);
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
        }

        private static Texture2D LoadIcon(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                (importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.npotScale != TextureImporterNPOTScale.None))
            {
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
