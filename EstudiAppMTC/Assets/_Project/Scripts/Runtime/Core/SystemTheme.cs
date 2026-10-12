using System;
using System.Diagnostics;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace EstudiApp.Core
{
    /// <summary>¿El sistema está en modo oscuro? Donde no se puede saber, responde que no (tema claro).</summary>
    public static class SystemTheme
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int EstudiApp_SystemIsDark();
#endif

        public static bool CanDetect
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.OSXPlayer:
                    case RuntimePlatform.OSXEditor:
                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.WindowsEditor:
                    case RuntimePlatform.Android:
                    case RuntimePlatform.IPhonePlayer:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public static bool IsDark()
        {
            try
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.OSXPlayer:
                    case RuntimePlatform.OSXEditor:
                        // Sin la clave AppleInterfaceStyle el sistema está en claro (y «defaults» termina con error).
                        return Run("/usr/bin/defaults", "read -g AppleInterfaceStyle").Trim() == "Dark";
                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.WindowsEditor:
                        return Run("reg", @"query HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize /v AppsUseLightTheme")
                            .Contains("0x0");
#if UNITY_ANDROID && !UNITY_EDITOR
                    case RuntimePlatform.Android:
                        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        using (var resources = activity.Call<AndroidJavaObject>("getResources"))
                        using (var configuration = resources.Call<AndroidJavaObject>("getConfiguration"))
                            return (configuration.Get<int>("uiMode") & 0x30) == 0x20;
#endif
#if UNITY_IOS && !UNITY_EDITOR
                    case RuntimePlatform.IPhonePlayer:
                        return EstudiApp_SystemIsDark() != 0;
#endif
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[EstudiApp] No se pudo leer el tema del sistema: " + e.Message);
            }
            return false;
        }

        private static string Run(string file, string arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(file, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);
            return output;
        }
    }
}
