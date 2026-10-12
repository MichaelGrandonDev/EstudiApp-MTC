using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace EstudiApp.Core
{
    /// <summary>Pedido de archivo en curso: se consulta con <see cref="Poll"/> hasta que termina.</summary>
    public sealed class PickRequest
    {
        private readonly Func<string> _poll;

        public bool Done { get; private set; }
        public bool Cancelled { get; private set; }
        public string Path { get; private set; }
        /// <summary>Nombre original del archivo (en móviles el archivo llega como copia temporal).</summary>
        public string OriginalName { get; private set; }
        public string Error { get; private set; }

        internal PickRequest(Func<string> poll)
        {
            _poll = poll;
        }

        internal static PickRequest Failed(string error)
        {
            var request = new PickRequest(null);
            request.Complete("error:" + error);
            return request;
        }

        internal static PickRequest Immediate(string result)
        {
            var request = new PickRequest(null);
            request.Complete(result);
            return request;
        }

        public bool Poll()
        {
            if (!Done && _poll != null)
            {
                var result = _poll();
                if (!string.IsNullOrEmpty(result))
                    Complete(result);
            }
            return Done;
        }

        /// <param name="result">"ok:ruta" o "ok:ruta|nombre original", "cancel", "none" o "error:mensaje".</param>
        private void Complete(string result)
        {
            Done = true;
            if (result.StartsWith("ok:"))
            {
                var value = result.Substring(3).Trim();
                var bar = value.IndexOf('|');
                Path = bar < 0 ? value : value.Substring(0, bar);
                OriginalName = bar < 0 ? System.IO.Path.GetFileName(Path) : value.Substring(bar + 1);
            }
            else if (result == "cancel" || result == "none")
                Cancelled = true;
            else
                Error = result.StartsWith("error:") ? result.Substring(6) : result;
        }
    }

    /// <summary>
    /// Elige un PDF con el selector del sistema: diálogo nativo en Mac, Windows y Linux; selector de documentos en
    /// Android (sin permisos de almacenamiento) e iPhone/iPad (incluye iCloud Drive y Archivos).
    /// </summary>
    public static class PdfPicker
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void EstudiAppPdf_Start();
        [DllImport("__Internal")] private static extern string EstudiAppPdf_TakeResult();
#endif

        public static PickRequest Request()
        {
#if UNITY_EDITOR
            var path = UnityEditor.EditorUtility.OpenFilePanel("Elige un libro en PDF", Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "pdf");
            return PickRequest.Immediate(string.IsNullOrEmpty(path) ? "cancel" : "ok:" + path);
#elif UNITY_ANDROID
            try
            {
                var bridge = new AndroidJavaClass("com.estudiapp.pdf.PdfPickActivity");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    bridge.CallStatic("start", activity);
                return new PickRequest(() => bridge.CallStatic<string>("takeResult"));
            }
            catch (Exception e)
            {
                return PickRequest.Failed(e.Message);
            }
#elif UNITY_IOS
            EstudiAppPdf_Start();
            return new PickRequest(EstudiAppPdf_TakeResult);
#else
            return DesktopDialog();
#endif
        }

        private static PickRequest DesktopDialog()
        {
            var result = string.Empty;
            var gate = new object();
            var thread = new Thread(() =>
            {
                string value;
                try
                {
                    value = RunDialog();
                }
                catch (Exception e)
                {
                    value = "error:" + e.Message;
                }
                lock (gate)
                    result = value;
            }) { IsBackground = true, Name = "Elegir PDF" };
            thread.Start();
            return new PickRequest(() =>
            {
                lock (gate)
                    return result;
            });
        }

        private static string RunDialog()
        {
            ProcessStartInfo info;
            switch (Application.platform)
            {
                case RuntimePlatform.OSXPlayer:
                    info = new ProcessStartInfo("/usr/bin/osascript",
                        "-e \"POSIX path of (choose file of type {\\\"com.adobe.pdf\\\"} with prompt \\\"Elige un libro en PDF\\\")\"");
                    break;
                case RuntimePlatform.WindowsPlayer:
                    info = new ProcessStartInfo("powershell.exe",
                        "-NoProfile -STA -Command \"[Console]::OutputEncoding=[Text.Encoding]::UTF8; Add-Type -AssemblyName System.Windows.Forms; " +
                        "$d=New-Object System.Windows.Forms.OpenFileDialog; $d.Title='Elige un libro en PDF'; " +
                        "$d.Filter='PDF|*.pdf'; if($d.ShowDialog() -eq 'OK'){[Console]::Out.Write($d.FileName)}\"");
                    break;
                case RuntimePlatform.LinuxPlayer:
                    info = new ProcessStartInfo("zenity", "--file-selection --title=\"Elige un libro en PDF\" --file-filter=\"*.pdf *.PDF\"");
                    break;
                default:
                    return "error:Este equipo no permite elegir archivos.";
            }
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.StandardOutputEncoding = Encoding.UTF8;
            using (var process = Process.Start(info))
            {
                if (process == null)
                    return "error:No se pudo abrir el diálogo de archivos.";
                var output = process.StandardOutput.ReadToEnd().Trim();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (!string.IsNullOrEmpty(output))
                    return "ok:" + output;
                return process.ExitCode == 0 || error.Contains("-128") || process.ExitCode == 1 ? "cancel" : "error:" + error.Trim();
            }
        }
    }
}
