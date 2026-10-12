using EstudiApp.Domain;
using UnityEngine;

namespace EstudiApp.Core
{
    /// <summary>Preferencias del estudiante.</summary>
    public class AppSettings
    {
        public int Version = 1;
        /// <summary>Escala del texto de las lecciones (0.9 – 1.3).</summary>
        public float TextScale = 1f;
        public bool ShowHanzi = true;
        /// <summary>Simulación de compra (sólo con planes activos, hasta conectar la tienda).</summary>
        public bool PremiumPreview;
        public bool SeenWelcome;
    }

    /// <summary>Todo lo que comparten las pantallas: contenido, progreso, biblioteca y preferencias.</summary>
    public class AppServices
    {
        private readonly JsonStore<ProgressState> _progressStore = new JsonStore<ProgressState>("progress.json");
        private readonly JsonStore<AppSettings> _settingsStore = new JsonStore<AppSettings>("settings.json");
        private bool _dirty;

        public Curriculum Curriculum { get; }
        public ProgressService Progress { get; }
        public ChallengeService Challenges { get; }
        public LibraryService Library { get; }
        public SearchService Search { get; }
        public EntitlementPolicy Entitlements { get; }
        public AppSettings Settings { get; }

        public AppServices(Curriculum curriculum)
        {
            Curriculum = curriculum;
            Settings = _settingsStore.Load();
            Entitlements = new EntitlementPolicy(AppConfig.DefaultMode);
            if (Entitlements.Mode == AccessMode.Free && Settings.PremiumPreview)
                Entitlements.Mode = AccessMode.Premium;
            Progress = new ProgressService(curriculum, _progressStore.Load(), Entitlements);
            Progress.Changed += () => _dirty = true;
            Challenges = new ChallengeService(curriculum, Progress);
            Library = new LibraryService(curriculum);
            Search = new SearchService(curriculum);
        }

        /// <summary>Con true, el progreso sólo cambia en memoria (recorrido de capturas con progreso simulado).</summary>
        public bool SavingSuspended { get; set; }

        /// <summary>Guarda el progreso si cambió (se llama periódicamente y al salir).</summary>
        public void Flush()
        {
            if (!_dirty || SavingSuspended)
                return;
            _dirty = false;
            _progressStore.Save(Progress.State);
        }

        public void SaveSettings() => _settingsStore.Save(Settings);

        public void SetPremiumPreview(bool value)
        {
            Settings.PremiumPreview = value;
            if (Entitlements.Mode != AccessMode.Open)
                Entitlements.Mode = value ? AccessMode.Premium : AccessMode.Free;
            SaveSettings();
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var color) ? color : fallback;
        }
    }
}
