using System.Collections;
using EstudiApp.UI;
using UnityEngine;

namespace EstudiApp.Core
{
    /// <summary>Punto de entrada: carga el temario, crea los servicios y construye la interfaz.</summary>
    public class AppBootstrap : MonoBehaviour
    {
        [SerializeField] private AppShell appShell;

        private void Awake()
        {
            // Sin foco la app se pausa: el recorrido de capturas no arrancaría si la ventana se abre detrás de otra.
            if (!string.IsNullOrEmpty(ScreenshotTour.Folder))
                Application.runInBackground = true;
        }

        private IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            if (appShell == null)
                appShell = FindAnyObjectByType<AppShell>();
            yield return null;
            AppServices services = null;
            string error = null;
            yield return ContentLoader.Load((curriculum, loadError) =>
            {
                error = loadError;
                if (curriculum != null)
                    services = new AppServices(curriculum);
            });
            if (services == null)
            {
                Debug.LogError("[EstudiApp] No se pudo cargar el temario: " + error);
                appShell.ShowError("No se pudo cargar el temario. " + error);
                yield break;
            }
            appShell.Initialize(services);
            if (!string.IsNullOrEmpty(ScreenshotTour.Folder))
                StartCoroutine(ScreenshotTour.Run(appShell, services));
        }
    }
}
