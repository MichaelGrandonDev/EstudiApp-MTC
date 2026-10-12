using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EstudiApp.Core;
using EstudiApp.Domain;
using EstudiApp.UI.Pages;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace EstudiApp.UI
{
    public enum Tab
    {
        Home,
        Path,
        Diagrams,
        Review,
        Library
    }

    /// <summary>
    /// Marco de la app: navegación por pestañas (barra lateral en pantallas anchas, pestañas abajo en el móvil),
    /// una pila de páginas por pestaña, diálogos y avisos.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class AppShell : MonoBehaviour
    {
        public const string PanelSettingsResource = "UI/PanelSettings";
        private const float NarrowWidth = 720f;

        private static readonly (Tab tab, IconKind icon, string label)[] Tabs =
        {
            (Tab.Home, IconKind.Home, "Inicio"),
            (Tab.Path, IconKind.Path, "Temario"),
            (Tab.Diagrams, IconKind.Diagram, "Diagramas"),
            (Tab.Review, IconKind.Review, "Repaso"),
            (Tab.Library, IconKind.Library, "Biblioteca")
        };

        public AppServices Services { get; private set; }

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _app;
        private VisualElement _stage;
        private VisualElement _overlay;
        private VisualElement _toasts;
        private VisualElement _loading;
        private Label _title;
        private Button _back;
        private readonly Dictionary<Tab, Button> _tabButtons = new Dictionary<Tab, Button>();
        private Label _reviewBadge;
        private readonly Dictionary<Tab, List<Page>> _stacks = new Dictionary<Tab, List<Page>>();
        private readonly Dictionary<Page, VisualElement> _views = new Dictionary<Page, VisualElement>();
        private readonly List<Button> _themeButtons = new List<Button>();
        private PanelSettings _panel;
        private Tab _tab = Tab.Home;
        private float _flushTimer;

        public bool Narrow { get; private set; }
        public Page Current => _stacks.TryGetValue(_tab, out var stack) && stack.Count > 0 ? stack[stack.Count - 1] : null;

        private void Awake()
        {
            // El tema se decide antes del primer fotograma (también la pantalla de carga): sin destello claro.
            Theme.Mode = Theme.ParseMode(ScreenshotTour.ThemeArgument ?? new JsonStore<AppSettings>(AppServices.SettingsFile).Load().Theme);
            Theme.Dark = ResolveDark(Theme.Mode);
            Debug.Log($"[EstudiApp] Apariencia: {Theme.ModeId(Theme.Mode)} → {(Theme.Dark ? "oscuro" : "claro")}");
            _document = GetComponent<UIDocument>();
            var source = _document.panelSettings != null ? _document.panelSettings : Resources.Load<PanelSettings>(PanelSettingsResource);
            if (source != null)
                _document.panelSettings = _panel = ConfigurePanel(source);
            _root = _document.rootVisualElement;
            var theme = Resources.Load<StyleSheet>("UI/Theme");
            if (theme != null)
                _root.styleSheets.Add(theme);
            _root.AddToClassList("root");
            ApplyThemeClass();
            _loading = _root.Append(El.Div("loading"));
            var logo = _loading.Append(new TaijiMark());
            logo.AddToClassList("loading__logo");
            _loading.Append(El.Text(AppConfig.AppName, "loading__title"));
            _loading.Append(El.Text("Cargando el temario…", "loading__text"));
            _loading.Append(El.Text(AppConfig.Credit, "loading__credit"));
        }

        /// <summary>Copia de PanelSettings con la escala de cada plataforma y la fuente china como respaldo.</summary>
        private static PanelSettings ConfigurePanel(PanelSettings source)
        {
            var settings = Instantiate(source);
            settings.colorClearValue = Theme.Paper;
            if (Application.isMobilePlatform)
            {
                settings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
                settings.referenceDpi = 160f;
                settings.fallbackDpi = 320f;
            }
            else
            {
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = new Vector2Int(1280, 800);
                settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                settings.match = 0.5f;
            }
            try
            {
                var font = Resources.Load<Font>("UI/Fonts/NotoSansSC-Subset");
                if (font != null)
                {
                    var asset = FontAsset.CreateFontAsset(font);
                    var text = settings.textSettings != null ? Instantiate(settings.textSettings) : ScriptableObject.CreateInstance<PanelTextSettings>();
                    if (text.fallbackFontAssets == null)
                        text.fallbackFontAssets = new List<FontAsset>();
                    text.fallbackFontAssets.Add(asset);
                    settings.textSettings = text;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[EstudiApp] Sin fuente de respaldo para caracteres chinos: " + e.Message);
            }
            return settings;
        }

        public void ShowError(string message)
        {
            _loading.Q<Label>(className: "loading__text").text = message;
        }

        public void Initialize(AppServices services)
        {
            Services = services;
            _loading.RemoveFromHierarchy();
            BuildFrame();
            ApplySettings();
            foreach (var (tab, _, _) in Tabs)
                _stacks[tab] = new List<Page>();
            SwitchTab(Tab.Home);
            services.Progress.Changed += UpdateBadges;
            UpdateBadges();
            if (!services.Settings.SeenWelcome)
                ShowWelcome();
        }

        private void BuildFrame()
        {
            _app = _root.Append(El.Div("app"));
            var nav = _app.Append(El.Div("nav"));
            var brand = nav.Append(El.Div("nav__brand"));
            brand.Append(new TaijiMark()).AddToClassList("nav__logo");
            var brandText = brand.Append(El.Div("nav__brand-text"));
            brandText.Append(El.Text("EstudiApp", "nav__name"));
            brandText.Append(El.Text("Medicina Tradicional China", "nav__tagline"));

            foreach (var (tab, icon, label) in Tabs)
            {
                var button = nav.Append(El.IconBtn(icon, label, () => SwitchTab(tab, true), "nav__btn"));
                _tabButtons[tab] = button;
                if (tab == Tab.Review)
                {
                    _reviewBadge = button.Append(El.Text(string.Empty, "badge"));
                    _reviewBadge.pickingMode = PickingMode.Ignore;
                }
            }
            nav.Append(El.Spacer()).AddToClassList("nav__spacer");
            nav.Append(El.IconBtn(IconKind.Search, "Buscar", () => Push(new SearchPage()), "nav__btn", "nav__btn--secondary"));
            nav.Append(El.IconBtn(IconKind.Settings, "Ajustes", () => Push(new SettingsPage()), "nav__btn", "nav__btn--secondary"));
            _themeButtons.Add(nav.Append(El.IconBtn(IconKind.Moon, "Modo oscuro", ToggleTheme, "nav__btn", "nav__btn--secondary")));
            var credit = nav.Append(El.Btn(string.Empty, AppConfig.OpenAuthorPage, "nav__credit"));
            credit.tooltip = AppConfig.AuthorUrl;
            credit.Append(El.Text("Creada por", "nav__credit-label")).pickingMode = PickingMode.Ignore;
            credit.Append(El.Text(AppConfig.Author, "nav__credit-name")).pickingMode = PickingMode.Ignore;

            var main = _app.Append(El.Div("main"));
            var bar = main.Append(El.Div("topbar"));
            _back = bar.Append(El.IconBtn(IconKind.Back, null, Back, "topbar__back"));
            _title = bar.Append(El.Text(string.Empty, "topbar__title"));
            bar.Append(El.Spacer());
            bar.Append(El.IconBtn(IconKind.Search, null, () => Push(new SearchPage()), "topbar__btn"));
            _themeButtons.Add(bar.Append(El.IconBtn(IconKind.Moon, null, ToggleTheme, "topbar__btn")));
            bar.Append(El.IconBtn(IconKind.Settings, null, () => Push(new SettingsPage()), "topbar__btn", "topbar__btn--narrow"));
            _stage = main.Append(El.Div("stage"));
            RefreshThemeButtons();

            _overlay = _root.Append(El.Div("overlay"));
            _overlay.SetVisible(false);
            _toasts = _root.Append(El.Div("toasts"));
            _toasts.pickingMode = PickingMode.Ignore;

            _root.RegisterCallback<GeometryChangedEvent>(_ => UpdateNarrow());
            UpdateNarrow();
        }

        private void UpdateNarrow()
        {
            var width = _root.resolvedStyle.width;
            if (float.IsNaN(width) || width < 1f)
                return;
            var narrow = width < NarrowWidth;
            if (narrow == Narrow && _app.ClassListContains("app--ready"))
                return;
            Narrow = narrow;
            _app.AddToClassList("app--ready");
            _root.EnableInClassList("narrow", narrow);
            _root.EnableInClassList("wide", !narrow);
        }

        public void ApplySettings()
        {
            var settings = Services.Settings;
            _root.EnableInClassList("hide-hanzi", !settings.ShowHanzi);
            _root.EnableInClassList("text-large", settings.TextScale > 1.08f);
            _root.EnableInClassList("text-xlarge", settings.TextScale > 1.2f);
            _root.EnableInClassList("text-small", settings.TextScale < 0.95f);
        }

        // ---- Apariencia ---------------------------------------------------------------------------------------

        private static bool ResolveDark(ThemeMode mode) =>
            mode == ThemeMode.Dark || mode == ThemeMode.System && SystemTheme.IsDark();

        /// <summary>Cambia y guarda la apariencia; se aplica al momento.</summary>
        public void SetTheme(ThemeMode mode)
        {
            Services.Settings.Theme = Theme.ModeId(mode);
            Services.SaveSettings();
            UseTheme(mode);
        }

        private void ToggleTheme() => SetTheme(Theme.Dark ? ThemeMode.Light : ThemeMode.Dark);

        /// <summary>Aplica la apariencia sin guardarla.</summary>
        public void UseTheme(ThemeMode mode)
        {
            var dark = ResolveDark(mode);
            var changed = dark != Theme.Dark;
            Theme.Mode = mode;
            Theme.Dark = dark;
            ApplyThemeClass();
            RefreshThemeButtons();
            if (changed)
            {
                // Lo pintado con Painter2D y los colores puestos desde código no siguen a la hoja de estilos.
                _root.Query<VisualElement>().ForEach(e => e.MarkDirtyRepaint());
                _root.Query<Diagrams.DiagramCanvas>().ForEach(c => c.Refresh());
            }
            Rebuild();
        }

        private void ApplyThemeClass()
        {
            _root.EnableInClassList("theme-dark", Theme.Dark);
            if (_panel != null)
                _panel.colorClearValue = Theme.Paper;
            var camera = Camera.main;
            if (camera != null)
                camera.backgroundColor = Theme.Paper;
        }

        private void RefreshThemeButtons()
        {
            foreach (var button in _themeButtons)
            {
                var icon = button.Q<Icon>();
                if (icon != null)
                    icon.Kind = Theme.Dark ? IconKind.Sun : IconKind.Moon;
                var label = button.Q<Label>(className: "icon-btn__label");
                if (label != null)
                    label.text = Theme.Dark ? "Modo claro" : "Modo oscuro";
                button.tooltip = Theme.Dark ? "Cambiar al modo claro" : "Cambiar al modo oscuro";
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && Services != null && Theme.Mode == ThemeMode.System && ResolveDark(ThemeMode.System) != Theme.Dark)
                UseTheme(ThemeMode.System);
        }

        private void UpdateBadges()
        {
            if (_reviewBadge == null)
                return;
            var due = Services.Progress.DueCount;
            _reviewBadge.text = due > 99 ? "99+" : due.ToString();
            _reviewBadge.SetVisible(due > 0);
        }

        // ---- Navegación ---------------------------------------------------------------------------------------

        public void SwitchTab(Tab tab, bool resetIfActive = false)
        {
            var stack = _stacks[tab];
            if (tab == _tab && resetIfActive && stack.Count > 1)
            {
                while (stack.Count > 1)
                    Close(stack[stack.Count - 1], stack);
            }
            Current?.OnHide();
            _tab = tab;
            foreach (var pair in _tabButtons)
                pair.Value.EnableInClassList("nav__btn--on", pair.Key == tab);
            if (stack.Count == 0)
            {
                var root = RootPage(tab);
                root.Attach(this);
                stack.Add(root);
            }
            Show(stack[stack.Count - 1]);
        }

        private static Page RootPage(Tab tab)
        {
            switch (tab)
            {
                case Tab.Path: return new PathPage();
                case Tab.Diagrams: return new DiagramsPage();
                case Tab.Review: return new ReviewPage();
                case Tab.Library: return new LibraryPage();
                default: return new HomePage();
            }
        }

        public void Push(Page page)
        {
            page.Attach(this);
            Current?.OnHide();
            _stacks[_tab].Add(page);
            Show(page);
        }

        /// <summary>Cambia de pestaña y abre la página encima de su raíz.</summary>
        public void Open(Tab tab, Page page)
        {
            SwitchTab(tab, true);
            Push(page);
        }

        /// <summary>Sustituye la página actual (por ejemplo, de la evaluación al resultado).</summary>
        public void Replace(Page page)
        {
            var stack = _stacks[_tab];
            if (stack.Count > 1)
                Close(stack[stack.Count - 1], stack);
            Push(page);
        }

        public void Back()
        {
            if (CloseOverlay())
                return;
            var stack = _stacks[_tab];
            if (stack.Count <= 1)
            {
                if (_tab != Tab.Home)
                    SwitchTab(Tab.Home);
                return;
            }
            Close(stack[stack.Count - 1], stack);
            Show(stack[stack.Count - 1]);
        }

        private void Close(Page page, List<Page> stack)
        {
            page.OnHide();
            page.OnClose();
            stack.Remove(page);
            _views.Remove(page);
        }

        private void Show(Page page)
        {
            _stage.Clear();
            if (!_views.TryGetValue(page, out var view))
            {
                if (page.OwnsScroll)
                    view = page.Root;
                else
                {
                    var scroll = El.VScroll("page-scroll");
                    scroll.Add(page.Root);
                    view = scroll;
                }
                _views[page] = view;
            }
            _stage.Add(view);
            page.Build();
            RefreshTopbar();
        }

        /// <summary>Vuelve a construir la página visible (tras cambiar el progreso o la biblioteca).</summary>
        public void Rebuild()
        {
            Current?.Build();
            RefreshTopbar();
        }

        public void RefreshTopbar()
        {
            var page = Current;
            _title.text = page?.Title ?? string.Empty;
            _back.SetVisible(_stacks[_tab].Count > 1);
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Services != null && Input.GetKeyDown(KeyCode.Escape))
                Back();
#endif
            if (Services == null)
                return;
            _flushTimer += Time.unscaledDeltaTime;
            if (_flushTimer > 2f)
            {
                _flushTimer = 0f;
                Services.Flush();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                Services?.Flush();
        }

        private void OnApplicationQuit() => Services?.Flush();

        // ---- Diálogos y avisos --------------------------------------------------------------------------------

        public void Toast(string text, float seconds = 3f)
        {
            var toast = _toasts.Append(El.Text(text, "toast"));
            toast.pickingMode = PickingMode.Ignore;
            toast.schedule.Execute(() => toast.AddToClassList("toast--out")).StartingIn((long)(seconds * 1000));
            toast.schedule.Execute(() => toast.RemoveFromHierarchy()).StartingIn((long)(seconds * 1000) + 400);
        }

        /// <summary>Diálogo modal; devuelve el cuerpo para añadir contenido.</summary>
        public VisualElement Dialog(string title, string text, params (string label, Action action, bool primary)[] buttons)
        {
            _overlay.Clear();
            _overlay.SetVisible(true);
            var scrim = _overlay.Append(El.Div("overlay__scrim"));
            scrim.RegisterCallback<ClickEvent>(_ => CloseOverlay());
            var dialog = _overlay.Append(El.Div("dialog"));
            dialog.Append(El.Text(title, "dialog__title"));
            if (!string.IsNullOrEmpty(text))
                dialog.Append(El.Text(text, "dialog__text"));
            var body = dialog.Append(El.Div("dialog__body"));
            var row = dialog.Append(El.Row("dialog__buttons"));
            row.Append(El.Spacer());
            if (buttons.Length == 0)
                buttons = new[] { ("Cerrar", (Action)null, true) };
            foreach (var (label, action, primary) in buttons)
                row.Append(El.Btn(label, () =>
                {
                    CloseOverlay();
                    action?.Invoke();
                }, "btn", primary ? null : "btn--ghost"));
            return body;
        }

        public void Confirm(string title, string text, string ok, Action onOk) =>
            Dialog(title, text, ("Cancelar", null, false), (ok, onOk, true));

        public bool CloseOverlay()
        {
            if (_overlay == null || !_overlay.IsShownOverlay())
                return false;
            _overlay.Clear();
            _overlay.SetVisible(false);
            return true;
        }

        private void ShowWelcome()
        {
            var body = Dialog("Bienvenido a EstudiApp MTC",
                "Una ruta para estudiar Medicina Tradicional China con bases firmes: ejes temáticos en orden, resúmenes con citas a los libros, diagramas interactivos, evaluaciones y repaso espaciado.",
                ("Empezar", () =>
                {
                    Services.Settings.SeenWelcome = true;
                    Services.SaveSettings();
                }, true));
            body.Append(El.Text("• Cada lección se abre al aprobar la anterior (70 %). Si ya sabes, activa el <b>modo libre</b> en Ajustes.", "dialog__text"));
            body.Append(El.Text("• Sube tus libros en PDF a la Biblioteca: las citas de las lecciones abrirán la página exacta.", "dialog__text"));
            body.Append(El.Text("• Es una versión de prueba: todo el contenido está abierto.", "dialog__text"));
            body.Append(El.Text($"{AppConfig.Credit} · {AppConfig.AuthorUrl.Replace("https://", "")}", "dialog__credit"));
        }

        // ---- Acciones compartidas -----------------------------------------------------------------------------

        public void OpenLesson(Lesson lesson)
        {
            if (lesson == null)
                return;
            var status = Services.Progress.Status(lesson);
            if (status == LessonStatus.PremiumLocked)
            {
                Push(new PlansPage());
                return;
            }
            if (status == LessonStatus.Locked)
            {
                var axis = Services.Curriculum.AxisOf(lesson.Id);
                var missing = Services.Progress.MissingRequirements(axis).ToList();
                var reason = !Services.Progress.IsAxisUnlocked(axis) && missing.Count > 0
                    ? $"Primero domina al menos el {ProgressService.AxisUnlockRatio:P0} de: {string.Join(", ", missing.Select(a => a.Title))}."
                    : "Aprueba la evaluación de la lección anterior para abrir esta.";
                Dialog("Lección bloqueada", reason + "\n\nSi ya tienes estos conocimientos, puedes activar el modo libre en Ajustes.",
                    ("Ir a Ajustes", () => Push(new SettingsPage()), false), ("Entendido", null, true));
                return;
            }
            Push(new LessonPage(lesson));
        }

        /// <summary>Abre un desafío: las lecciones como siempre; desafíos de unidad, exámenes y mini tests en su portada.</summary>
        public void OpenChallenge(Challenge challenge)
        {
            if (challenge == null)
                return;
            if (challenge.Kind == ChallengeKind.Lesson)
            {
                OpenLesson(challenge.Lesson);
                return;
            }
            if (!Services.Entitlements.CanStudy(challenge.Axis))
            {
                Push(new PlansPage());
                return;
            }
            Push(new ChallengeIntroPage(challenge));
        }

        public void OpenDiagram(string diagramId)
        {
            var spec = Services.Curriculum.Diagram(diagramId);
            if (spec != null)
                Push(new DiagramPage(spec));
        }

        /// <summary>Abre la cita en el PDF del estudiante; si no lo tiene, le ofrece subirlo.</summary>
        public void OpenReference(Reference reference)
        {
            if (reference == null)
                return;
            var library = Services.Library;
            var book = library.Library.ForBibliography(reference.Book);
            if (book != null)
            {
                Push(new ReaderPage(book, LibraryService.PageFor(reference, book), reference));
                return;
            }
            var info = Services.Curriculum.Book(reference.Book);
            var title = info != null ? info.Title : reference.Book;
            Dialog("Abrir la cita en tu libro",
                $"<b>{Refs.Format(reference, Services.Curriculum)}</b>\n{title}" + (info?.Author != null ? $" — {info.Author}" : string.Empty) +
                "\n\nTodavía no subiste este libro. Sube tu PDF y la app lo abrirá en la página citada.",
                ("Ahora no", null, false),
                ("Subir PDF", () => ImportPdf(added =>
                {
                    if (added.BibliographyId == null)
                        library.Link(added, reference.Book);
                    if (added.BibliographyId == reference.Book)
                        Push(new ReaderPage(added, LibraryService.PageFor(reference, added), reference));
                    else
                        Toast($"El PDF se reconoció como «{added.Title}». Puedes cambiar la asociación desde la Biblioteca.", 5f);
                }), true));
        }

        public void ImportPdf(Action<UserBook> onAdded = null)
        {
            if (!Services.Entitlements.CanImportPdf(Services.Library.Library.Books.Count))
            {
                Push(new PlansPage());
                return;
            }
            StartCoroutine(ImportRoutine(onAdded));
        }

        private IEnumerator ImportRoutine(Action<UserBook> onAdded)
        {
            var request = PdfPicker.Request();
            while (!request.Poll())
                yield return null;
            if (request.Cancelled)
                yield break;
            if (request.Error != null)
            {
                Dialog("No se pudo elegir el archivo", request.Error);
                yield break;
            }
            Toast("Importando " + request.OriginalName + "…");
            var task = Services.Library.Import(request.Path, request.OriginalName, Application.isMobilePlatform);
            while (!task.IsCompleted)
                yield return null;
            var result = task.Result;
            if (result.Error != null)
            {
                Dialog("No se pudo importar el PDF", result.Error);
                yield break;
            }
            Services.Library.Add(result.Book);
            Toast(result.Matched != null
                ? $"«{result.Book.Title}» añadido y enlazado con la bibliografía."
                : $"«{result.Book.Title}» añadido a tu biblioteca.", 4f);
            onAdded?.Invoke(result.Book);
            if (onAdded == null)
                Rebuild();
        }
    }

    internal static class OverlayExtensions
    {
        public static bool IsShownOverlay(this VisualElement element) => !element.ClassListContains("hidden");
    }
}
