using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EstudiApp.EditorTools
{
    /// <summary>
    /// Asigna cada binario de PDFium a su plataforma (si no, Unity los incluiría todos y chocarían los .so de Linux y
    /// Android) y declara lo que necesita el selector de documentos de iOS.
    /// </summary>
    public class PluginSetup : AssetPostprocessor
    {
        private const string Pdfium = "Assets/Plugins/PDFium/";

        private static readonly BuildTarget[] AllTargets =
        {
            BuildTarget.StandaloneOSX, BuildTarget.StandaloneWindows, BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneLinux64, BuildTarget.Android, BuildTarget.iOS, BuildTarget.WebGL
        };

        private static readonly Dictionary<string, Action<PluginImporter>> Rules = new Dictionary<string, Action<PluginImporter>>
        {
            { Pdfium + "macOS/libpdfium.dylib", p => Desktop(p, "OSX", BuildTarget.StandaloneOSX) },
            { Pdfium + "Windows/pdfium.dll", p => Desktop(p, "Windows", BuildTarget.StandaloneWindows64, "x86_64") },
            { Pdfium + "Linux/libpdfium.so", p => Desktop(p, "Linux", BuildTarget.StandaloneLinux64, "x86_64") },
            { "Assets/Plugins/Android/libs/arm64-v8a/libpdfium.so", p =>
                {
                    Only(p, BuildTarget.Android);
                    p.SetPlatformData(BuildTarget.Android, "CPU", "ARM64");
                }
            },
            { "Assets/Plugins/iOS/pdfium.framework", p =>
                {
                    Only(p, BuildTarget.iOS);
                    p.SetPlatformData(BuildTarget.iOS, "AddToEmbeddedBinaries", "true");
                }
            },
            { "Assets/Plugins/iOS/EstudiAppPdf.mm", p =>
                {
                    Only(p, BuildTarget.iOS);
                    p.SetPlatformData(BuildTarget.iOS, "FrameworkDependencies", "UniformTypeIdentifiers;");
                }
            }
        };

        private static void Only(PluginImporter importer, BuildTarget target)
        {
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            foreach (var other in AllTargets)
                importer.SetCompatibleWithPlatform(other, other == target);
        }

        private static void Desktop(PluginImporter importer, string os, BuildTarget target, string cpu = "AnyCPU")
        {
            Only(importer, target);
            importer.SetCompatibleWithEditor(true);
            importer.SetEditorData("OS", os);
            importer.SetEditorData("CPU", cpu);
            importer.SetPlatformData(target, "CPU", cpu);
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
                if (Rules.ContainsKey(path))
                    Apply(path);
        }

        [MenuItem("EstudiApp/Configurar plugins nativos", priority = 2)]
        public static void ApplyAll()
        {
            foreach (var path in Rules.Keys)
                Apply(path);
        }

        private static void Apply(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is PluginImporter importer))
                return;
            const string marker = "estudiapp-v1";
            if (importer.userData == marker)
                return;
            Rules[path](importer);
            importer.userData = marker;
            importer.SaveAndReimport();
            Debug.Log("[EstudiApp] Plugin configurado: " + path);
        }
    }
}
