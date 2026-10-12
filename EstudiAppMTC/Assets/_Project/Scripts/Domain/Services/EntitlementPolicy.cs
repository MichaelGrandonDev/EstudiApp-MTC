namespace EstudiApp.Domain
{
    public enum AccessMode
    {
        /// <summary>Versión de prueba: todo abierto, sin planes a la vista.</summary>
        Open,
        /// <summary>Plan gratuito con contenido premium bloqueado.</summary>
        Free,
        Premium
    }

    /// <summary>Qué puede usar cada plan.</summary>
    public class EntitlementPolicy
    {
        public const int FreePdfLimit = 3;

        public AccessMode Mode { get; set; }

        public EntitlementPolicy(AccessMode mode)
        {
            Mode = mode;
        }

        public bool ShowsPlans => Mode != AccessMode.Open;

        public bool CanStudy(Axis axis) => axis == null || !axis.Premium || Mode != AccessMode.Free;

        public bool CanImportPdf(int alreadyImported) => Mode != AccessMode.Free || alreadyImported < FreePdfLimit;
    }
}
