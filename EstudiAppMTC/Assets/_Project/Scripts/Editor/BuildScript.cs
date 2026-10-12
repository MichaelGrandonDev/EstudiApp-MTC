using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EstudiApp.EditorTools
{
    /// <summary>
    /// Compilaciones desde el menú o por línea de comandos:
    /// <c>Unity -batchmode -quit -projectPath EstudiAppMTC -executeMethod EstudiApp.EditorTools.BuildScript.Mac</c>.
    /// Por defecto es la versión de prueba (todo abierto); con <c>-plans</c> se activan el plan gratuito y los planes.
    /// </summary>
    public static class BuildScript
    {
        private const string PlansDefine = "ESTUDIAPP_PLANS";

        [MenuItem("EstudiApp/Compilar/macOS")]
        public static void Mac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/EstudiApp MTC.app");

        [MenuItem("EstudiApp/Compilar/Windows")]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/EstudiApp MTC.exe");

        [MenuItem("EstudiApp/Compilar/Android (APK)")]
        public static void Android()
        {
            EditorUserBuildSettings.buildAppBundle = Args.Contains("-aab");
            Build(BuildTarget.Android, EditorUserBuildSettings.buildAppBundle ? "Builds/Android/EstudiAppMTC.aab" : "Builds/Android/EstudiAppMTC.apk");
        }

        [MenuItem("EstudiApp/Compilar/iOS (proyecto Xcode)")]
        public static void IOS() => Build(BuildTarget.iOS, "Builds/iOS");

        private static string[] Args => Environment.GetCommandLineArgs();

        private static void Build(BuildTarget target, string output)
        {
            ProjectSetup.Batch();
            PluginSetup.ApplyAll();
            var group = BuildPipeline.GetBuildTargetGroup(target);
            var named = NamedBuildTarget.FromBuildTargetGroup(group);
            var defines = PlayerSettings.GetScriptingDefineSymbols(named).Split(';').Where(d => d.Length > 0 && d != PlansDefine).ToList();
            if (Args.Contains("-plans"))
                defines.Add(PlansDefine);
            PlayerSettings.SetScriptingDefineSymbols(named, string.Join(";", defines));

            var directory = Path.GetDirectoryName(Path.GetFullPath(output));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = output,
                target = target,
                targetGroup = group,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[EstudiApp] Compilación {target}: {summary.result} · {summary.totalErrors} errores · {summary.totalSize / (1024 * 1024)} MB → {output}");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
