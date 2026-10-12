using EstudiApp.Core;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Pages
{
    public class SettingsPage : Page
    {
        public override string Title => "Ajustes";

        public override void Build()
        {
            Root.Clear();
            var progress = S.Progress;
            var settings = S.Settings;
            Root.Append(El.Text("Ajustes", "page-title"));

            Section("Estudio");
            var free = Root.Append(new Toggle("Modo libre") { value = progress.State.FreeMode });
            free.AddToClassList("setting-toggle");
            free.RegisterValueChangedCallback(e =>
            {
                progress.SetFreeMode(e.newValue);
                Shell.Toast(e.newValue ? "Modo libre: todas las lecciones abiertas." : "Ruta guiada: las lecciones se abren en orden.");
            });
            Root.Append(El.Text("Abre todas las lecciones sin seguir el orden. Útil si ya tienes formación; para construir bases firmes se recomienda la ruta guiada.", "setting-help"));

            var hanzi = Root.Append(new Toggle("Mostrar caracteres chinos") { value = settings.ShowHanzi });
            hanzi.AddToClassList("setting-toggle");
            hanzi.RegisterValueChangedCallback(e =>
            {
                settings.ShowHanzi = e.newValue;
                S.SaveSettings();
                Shell.ApplySettings();
            });

            var size = Root.Append(new Slider("Tamaño del texto", 0.9f, 1.3f) { value = settings.TextScale });
            size.AddToClassList("setting-slider");
            size.RegisterValueChangedCallback(e =>
            {
                settings.TextScale = e.newValue;
                S.SaveSettings();
                Shell.ApplySettings();
            });

            Section("Apariencia");
            var themes = Root.Append(El.Div("chips", "theme-options"));
            foreach (var (mode, icon, label) in new[] { (ThemeMode.Light, IconKind.Sun, "Claro"), (ThemeMode.Dark, IconKind.Moon, "Oscuro"), (ThemeMode.System, IconKind.Settings, "Sistema") })
            {
                var chip = themes.Append(El.Chip(label, () => Shell.SetTheme(mode), Theme.Mode == mode));
                chip.Insert(0, new Icon(icon));
                chip.AddToClassList("theme-option");
            }
            Root.Append(El.Text(SystemTheme.CanDetect
                ? "«Sistema» sigue el modo claro u oscuro del dispositivo. Las páginas de tus PDF se ven siempre como el papel."
                : "En este dispositivo no se puede saber el modo del sistema: «Sistema» usa el tema claro. Las páginas de tus PDF se ven siempre como el papel.", "setting-help"));

            Section("Plan");
            if (S.Entitlements.ShowsPlans)
            {
                Root.Append(El.Text(S.Entitlements.Mode == AccessMode.Premium ? "Premium activo (vista previa)." : "Plan gratuito.", "setting-help"));
                Root.Append(El.IconBtn(IconKind.Crown, "Ver planes", () => Shell.Push(new PlansPage()), "btn", "btn--gold"));
            }
            else
                Root.Append(El.Text("Versión de prueba: todo el contenido está abierto. Más adelante habrá un plan gratuito y una suscripción Premium con los ejes clínicos avanzados.", "setting-help"));

            Section("Progreso");
            Root.Append(El.Text($"{progress.State.Lessons.Count} lecciones empezadas · {progress.State.Cards.Count} tarjetas de repaso · {progress.State.StudyDays.Count} días de estudio.", "setting-help"));
            Root.Append(El.Btn("Reiniciar progreso", () => Shell.Confirm("Reiniciar progreso",
                "Se borrarán las lecciones dominadas, los desafíos, las estrellas, la experiencia, las tarjetas de repaso y la racha. Tus notas y libros se conservan.", "Reiniciar", () =>
                {
                    progress.Reset();
                    S.Flush();
                    Shell.Toast("Progreso reiniciado.");
                    Build();
                }), "btn", "btn--danger"));

            Section("Acerca de");
            Root.Append(El.Text($"{AppConfig.AppName} · versión {Application.version}", "setting-help"));
            var author = Root.Append(El.Row("credit"));
            author.Append(new TaijiMark()).AddToClassList("credit__logo");
            var authorText = author.Append(El.Div("credit__text"));
            authorText.Append(El.Text("Creada por", "credit__label"));
            authorText.Append(El.Text(AppConfig.Author, "credit__name"));
            author.Append(El.Spacer());
            author.Append(El.Btn("GitHub", AppConfig.OpenAuthorPage, "btn", "btn--ghost", "btn--small")).tooltip = AppConfig.AuthorUrl;
            Root.Append(El.Text("Contenido elaborado con palabras propias a partir de Li Ping (<i>El gran libro de la medicina china</i>), Giovanni Maciocia (<i>Los fundamentos de la medicina china</i>), el <i>Su Wen</i>, Claudia Focks (<i>Atlas de acupuntura</i>) y los apuntes de la Escuela Neidan. Cada lección cita libro y página.", "setting-help"));
            Root.Append(El.Text("EstudiApp MTC es una herramienta de estudio. No sustituye la formación presencial ni el diagnóstico o tratamiento de un profesional de la salud.", "setting-help", "setting-help--warn"));
            Root.Append(El.Text("Tipografías Outfit, Cormorant Garamond y Noto Sans SC (SIL Open Font License). Lector de PDF basado en PDFium (BSD/Apache 2.0).", "setting-help"));
        }

        private void Section(string title) => Root.Append(El.Text(title, "section-title"));
    }

    /// <summary>Planes (preparado para la suscripción; la compra real se conectará a la tienda).</summary>
    public class PlansPage : Page
    {
        public override string Title => "Planes";

        public override void Build()
        {
            Root.Clear();
            Root.Append(El.Text("Planes", "page-title"));
            Root.Append(El.Text("Estudia los cimientos gratis. Con Premium se abren los ejes clínicos avanzados y la biblioteca sin límite.", "page-lead"));
            var plans = Root.Append(El.Div("plans"));

            var free = plans.Append(El.Div("plan"));
            free.Append(El.Text("Gratis", "plan__name"));
            free.Append(El.Text("0 €", "plan__price"));
            foreach (var line in new[] { "Ejes de cimientos y fisiología", "Diagramas interactivos de esos ejes", "Evaluaciones y repaso espaciado", $"Hasta {EntitlementPolicy.FreePdfLimit} libros PDF" })
                free.Append(El.Text("• " + line, "plan__line"));

            var premium = plans.Append(El.Div("plan", "plan--premium"));
            premium.Append(new Icon(IconKind.Crown)).AddToClassList("plan__crown");
            premium.Append(El.Text("Premium", "plan__name"));
            premium.Append(El.Text("Próximamente", "plan__price"));
            foreach (var line in new[] { "Todos los ejes, incluidos síndromes y clínica", "Todos los diagramas y recorridos", "Biblioteca de PDF sin límite", "Nuevos contenidos cada mes" })
                premium.Append(El.Text("• " + line, "plan__line"));

            if (S.Entitlements.Mode == AccessMode.Open)
            {
                Root.Append(El.Text("Ahora mismo estás en la versión de prueba: todo está abierto.", "pill", "pill--gold"));
                return;
            }
            var preview = S.Settings.PremiumPreview;
            Root.Append(El.Btn(preview ? "Desactivar vista previa Premium" : "Activar vista previa Premium", () =>
            {
                S.SetPremiumPreview(!preview);
                Shell.Toast(preview ? "Vuelves al plan gratuito." : "Vista previa Premium activada.");
                Build();
            }, "btn", "btn--gold"));
            Root.Append(El.Text("La compra se habilitará al publicar la app en las tiendas.", "muted"));
        }
    }
}
