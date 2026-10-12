using EstudiApp.Domain;

namespace EstudiApp.Core
{
    /// <summary>
    /// Modo de acceso según la compilación. Por defecto es la versión de prueba (todo abierto). Con el símbolo
    /// ESTUDIAPP_PLANS (BuildScript -plans) se activan el plan gratuito y la pantalla de planes; la compra real
    /// se conectará a la tienda cuando la suscripción esté lista.
    /// </summary>
    public static class AppConfig
    {
        public const string AppName = "EstudiApp MTC";
        public const string Author = "MichaelGrandonDev";
        public const string AuthorUrl = "https://github.com/MichaelGrandonDev";
        public static string Credit => "Creada por " + Author;

        public static void OpenAuthorPage() => UnityEngine.Application.OpenURL(AuthorUrl);

        public static AccessMode DefaultMode =>
#if ESTUDIAPP_PLANS
            AccessMode.Free;
#else
            AccessMode.Open;
#endif
    }
}
