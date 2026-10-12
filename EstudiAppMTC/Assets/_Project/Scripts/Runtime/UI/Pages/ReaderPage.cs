using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EstudiApp.Core;
using EstudiApp.Domain;
using EstudiApp.Pdf;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Pages
{
    /// <summary>Lector de PDF: páginas, zoom, marcadores, búsqueda de texto y saltos desde las citas.</summary>
    public class ReaderPage : Page
    {
        private const float MaxPageWidth = 980f;
        private const int MaxPixels = 2600;

        private readonly UserBook _book;
        private readonly Reference _from;
        private int _page;
        private float _zoom = 1f;
        private PdfDocument _document;
        private string _error;
        private bool _built;

        private ScrollView _scroll;
        private VisualElement _sheet;
        private Label _pageLabel;
        private Label _status;
        private Button _bookmark;
        private VisualElement _search;
        private VisualElement _results;
        private Label _searchStatus;
        private VisualElement _banner;
        private Texture2D _texture;
        private int _renderVersion;
        private int _searchVersion;
        private Vector2 _pointerStart;

        public ReaderPage(UserBook book, int page, Reference from)
        {
            _book = book;
            _page = Math.Max(1, page);
            _from = from;
        }

        public override string Title => _book.Title;
        public override bool OwnsScroll => true;

        public override void Build()
        {
            if (_built)
                return;
            _built = true;
            Root.Clear();
            Root.AddToClassList("reader");
            try
            {
                _document = PdfDocument.Open(S.Library.FullPath(_book));
                if (_document.PageCount > 0 && _book.Pages != _document.PageCount)
                {
                    _book.Pages = _document.PageCount;
                    S.Library.Save();
                }
            }
            catch (Exception e)
            {
                _error = e is DllNotFoundException || e is EntryPointNotFoundException
                    ? "El lector de PDF no está disponible en esta plataforma."
                    : e.Message;
            }

            var bar = Root.Append(El.Row("reader__bar"));
            bar.Append(El.IconBtn(IconKind.Back, null, () => Go(_page - 1), "btn--ghost", "icon-btn--small"));
            var pageButton = bar.Append(El.Btn(string.Empty, AskPage, "reader__page"));
            _pageLabel = pageButton.Append(El.Text(string.Empty, "reader__page-label"));
            _pageLabel.pickingMode = PickingMode.Ignore;
            bar.Append(El.IconBtn(IconKind.Forward, null, () => Go(_page + 1), "btn--ghost", "icon-btn--small"));
            bar.Append(El.Spacer());
            bar.Append(El.IconBtn(IconKind.Minus, null, () => Zoom(-0.25f), "btn--ghost", "icon-btn--small"));
            bar.Append(El.IconBtn(IconKind.Plus, null, () => Zoom(0.25f), "btn--ghost", "icon-btn--small"));
            _bookmark = bar.Append(El.IconBtn(IconKind.Bookmark, null, ToggleBookmark, "btn--ghost", "icon-btn--small"));
            bar.Append(El.IconBtn(IconKind.Search, null, ToggleSearch, "btn--ghost", "icon-btn--small"));
            bar.Append(El.IconBtn(IconKind.Settings, null, ShowMenu, "btn--ghost", "icon-btn--small"));

            _banner = Root.Append(El.Div("reader__banner"));
            BuildBanner();

            _search = Root.Append(El.Div("reader__search"));
            _search.SetVisible(false);
            var field = _search.Append(new TextField());
            field.AddToClassList("search-field");
            field.textEdition.placeholder = "Buscar en el libro…";
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    RunSearch(field.value);
            }, TrickleDown.TrickleDown);
            var searchRow = _search.Append(El.Row());
            _searchStatus = searchRow.Append(El.Text("Escribe una palabra y pulsa Intro.", "muted"));
            searchRow.Append(El.Spacer());
            searchRow.Append(El.Btn("Buscar", () => RunSearch(field.value), "btn", "btn--small"));
            _results = _search.Append(El.VScroll("reader__results"));

            _scroll = Root.Append(El.With(new ScrollView(ScrollViewMode.VerticalAndHorizontal), "reader__scroll"));
            _sheet = _scroll.Append(El.Div("reader__sheet"));
            _status = _scroll.Append(El.Text(string.Empty, "reader__status"));
            _scroll.RegisterCallback<GeometryChangedEvent>(_ => Render());
            _scroll.RegisterCallback<PointerDownEvent>(e => _pointerStart = e.position, TrickleDown.TrickleDown);
            _scroll.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);

            Root.focusable = true;
            Root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.target is TextField || e.target is TextElement && ((VisualElement)e.target).GetFirstAncestorOfType<TextField>() != null)
                    return;
                if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.PageDown)
                    Go(_page + 1);
                else if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.PageUp)
                    Go(_page - 1);
            });
            Root.schedule.Execute(() => Root.Focus());

            if (_error != null)
            {
                _status.text = _error;
                _pageLabel.text = "—";
                return;
            }
            Go(_page);
        }

        private int PageCount => _document?.PageCount ?? Math.Max(1, _book.Pages);

        private void BuildBanner()
        {
            _banner.Clear();
            _banner.SetVisible(_from != null && _from.Pdf > 0);
            if (_from == null || _from.Pdf <= 0)
                return;
            var target = LibraryService.PageFor(_from, _book);
            _banner.Append(El.Text($"Cita: <b>{Refs.Format(_from, S.Curriculum)}</b> → página {target} de tu PDF" +
                                   (string.IsNullOrEmpty(_from.Note) ? "" : $" · {_from.Note}"), "reader__banner-text"));
            _banner.Append(El.Spacer());
            _banner.Append(El.Btn("¿No coincide?", () => ShowOffset(), "btn", "btn--ghost", "btn--small"));
        }

        private void Go(int page)
        {
            if (_document == null)
                return;
            _page = Mathf.Clamp(page, 1, PageCount);
            _pageLabel.text = $"{_page} / {PageCount}";
            _bookmark.EnableInClassList("icon-btn--on", _book.Bookmarks.Any(b => b.Page == _page));
            if (_book.LastPage != _page)
            {
                _book.LastPage = _page;
                S.Library.Save();
            }
            _scroll.scrollOffset = Vector2.zero;
            Render();
        }

        private void Zoom(float delta)
        {
            _zoom = Mathf.Clamp(_zoom + delta, 0.5f, 3f);
            Render();
        }

        private float PanelScale()
        {
            var width = Root.panel?.visualTree.resolvedStyle.width ?? 0f;
            return width > 1f ? Screen.width / width : 1f;
        }

        private void Render()
        {
            if (_document == null || _scroll == null)
                return;
            var viewport = _scroll.contentViewport.resolvedStyle.width;
            if (float.IsNaN(viewport) || viewport < 10f)
                return;
            var baseWidth = Mathf.Min(viewport - 24f, MaxPageWidth);
            var width = baseWidth * _zoom;
            var size = _document.PageSize(_page - 1);
            _sheet.style.width = width;
            _sheet.style.height = width * size.height / Mathf.Max(1f, size.width);
            var pixels = Mathf.Clamp(Mathf.RoundToInt(width * PanelScale()), 200, MaxPixels);
            var version = ++_renderVersion;
            Shell.StartCoroutine(RenderRoutine(version, _page, pixels));
        }

        private IEnumerator RenderRoutine(int version, int page, int pixels)
        {
            if (_texture == null)
                _status.text = "Cargando página…";
            var document = _document;
            var task = Task.Run(() => document.Render(page - 1, pixels));
            while (!task.IsCompleted)
                yield return null;
            if (version != _renderVersion || _document == null)
                yield break;
            if (task.IsFaulted)
            {
                _status.text = task.Exception?.GetBaseException().Message ?? "No se pudo mostrar la página.";
                yield break;
            }
            var bitmap = task.Result;
            var texture = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.BGRA32, false) { name = "pdf-page", filterMode = FilterMode.Bilinear };
            texture.LoadRawTextureData(bitmap.Bgra);
            texture.Apply(false, true);
            _sheet.style.backgroundImage = new StyleBackground(texture);
            if (_texture != null)
                UnityEngine.Object.Destroy(_texture);
            _texture = texture;
            _status.text = string.Empty;
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (_zoom > 1.01f)
                return;
            var delta = (Vector2)e.position - _pointerStart;
            if (Mathf.Abs(delta.x) > 90f && Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.8f)
                Go(_page + (delta.x < 0 ? 1 : -1));
        }

        private void AskPage()
        {
            if (_document == null)
                return;
            var body = Shell.Dialog("Ir a la página", $"Del 1 al {PageCount} (numeración del PDF).");
            var field = body.Append(new TextField { value = _page.ToString() });
            field.AddToClassList("search-field");
            body.Append(El.Btn("Ir", () =>
            {
                Shell.CloseOverlay();
                if (int.TryParse(field.value.Trim(), out var page))
                    Go(page);
            }, "btn"));
            field.schedule.Execute(() => field.Focus());
        }

        private void ToggleBookmark()
        {
            var existing = _book.Bookmarks.FirstOrDefault(b => b.Page == _page);
            if (existing != null)
            {
                _book.Bookmarks.Remove(existing);
                Shell.Toast("Marcador eliminado");
            }
            else
            {
                _book.Bookmarks.Add(new Bookmark { Page = _page, Label = "Página " + _page, Created = DateTime.Now });
                Shell.Toast("Página marcada");
            }
            S.Library.Save();
            _bookmark.EnableInClassList("icon-btn--on", existing == null);
        }

        private void ShowMenu()
        {
            var info = S.Curriculum.Book(_book.BibliographyId);
            var body = Shell.Dialog(_book.Title, info != null ? $"Enlazado con {info.Title}." : "Este PDF no está enlazado con la bibliografía.");
            body.Append(El.Btn("Enlazar con otro libro", () =>
            {
                Shell.CloseOverlay();
                LibraryPage.ChooseLink(Shell, _book, () => Shell.RefreshTopbar());
            }, "btn", "btn--ghost"));
            body.Append(El.Btn($"Ajustar desfase de páginas ({_book.PageOffset:+0;-0;0})", () =>
            {
                Shell.CloseOverlay();
                ShowOffset();
            }, "btn", "btn--ghost"));
            body.Append(El.Text("Marcadores", "dialog__subtitle"));
            if (_book.Bookmarks.Count == 0)
                body.Append(El.Text("Toca el marcador de la barra para guardar la página actual.", "muted"));
            foreach (var mark in _book.Bookmarks.OrderBy(b => b.Page))
                body.Append(El.Btn(mark.Label, () =>
                {
                    Shell.CloseOverlay();
                    Go(mark.Page);
                }, "option"));
        }

        /// <summary>Si el PDF es otra edición o escaneo, las citas caen corridas: el desfase lo corrige para todas.</summary>
        private void ShowOffset()
        {
            var body = Shell.Dialog("Desfase de páginas",
                "Si las citas abren una página corrida (otra edición o un escaneo distinto), navega hasta la página correcta y pulsa «Usar esta página», o ajusta el número a mano. Se aplica a todas las citas de este libro.");
            var value = body.Append(El.Text(string.Empty, "offset__value"));
            void Refresh() => value.text = $"Desfase actual: {_book.PageOffset:+0;-0;0}";
            Refresh();
            var row = body.Append(El.Row("offset__row"));
            row.Append(El.IconBtn(IconKind.Minus, null, () =>
            {
                _book.PageOffset--;
                Refresh();
            }, "btn--ghost"));
            row.Append(El.IconBtn(IconKind.Plus, null, () =>
            {
                _book.PageOffset++;
                Refresh();
            }, "btn--ghost"));
            if (_from != null && _from.Pdf > 0)
                row.Append(El.Btn("Usar esta página", () =>
                {
                    _book.PageOffset = _page - _from.Pdf;
                    Refresh();
                }, "btn", "btn--small"));
            body.Append(El.Btn("Guardar", () =>
            {
                Shell.CloseOverlay();
                S.Library.Save();
                BuildBanner();
                if (_from != null && _from.Pdf > 0)
                    Go(LibraryService.PageFor(_from, _book));
            }, "btn"));
        }

        private void ToggleSearch()
        {
            var show = _search.ClassListContains("hidden");
            _search.SetVisible(show);
            if (show)
                _search.Q<TextField>()?.Focus();
        }

        private void RunSearch(string query)
        {
            if (_document == null)
                return;
            var words = BookMatcher.Normalize(query ?? string.Empty);
            if (words.Length < 2)
                return;
            _results.Clear();
            Shell.StartCoroutine(SearchRoutine(++_searchVersion, words));
        }

        private IEnumerator SearchRoutine(int version, string query)
        {
            var document = _document;
            var found = new ConcurrentQueue<(int page, string snippet)>();
            var scanned = 0;
            var withText = 0;
            var count = document.PageCount;
            var task = Task.Run(() =>
            {
                for (var i = 0; i < count && version == _searchVersion; i++)
                {
                    var text = document.PageText(i);
                    if (text.Trim().Length > 20)
                        withText++;
                    var normalized = BookMatcher.Normalize(text);
                    var index = normalized.IndexOf(query, StringComparison.Ordinal);
                    if (index >= 0)
                    {
                        var start = Math.Max(0, index - 60);
                        var length = Math.Min(normalized.Length - start, query.Length + 120);
                        found.Enqueue((i + 1, normalized.Substring(start, length)));
                    }
                    scanned = i + 1;
                }
            });
            var hits = 0;
            while (!task.IsCompleted || !found.IsEmpty)
            {
                if (version != _searchVersion)
                    yield break;
                while (found.TryDequeue(out var hit) && hits < 300)
                {
                    hits++;
                    var page = hit.page;
                    var row = _results.Append(El.Btn(string.Empty, () => Go(page), "search-hit"));
                    row.Append(El.Text("p. " + page, "search-hit__page")).pickingMode = PickingMode.Ignore;
                    row.Append(El.Text("…" + Highlight(hit.snippet, query) + "…", "search-hit__text")).pickingMode = PickingMode.Ignore;
                }
                _searchStatus.text = $"Buscando… {scanned}/{count} páginas · {hits} resultados";
                yield return null;
            }
            _searchStatus.text = hits > 0
                ? $"{hits} página{(hits == 1 ? "" : "s")} con «{query}»"
                : withText < Math.Min(5, count)
                    ? "Este PDF parece un escaneo sin capa de texto: no se puede buscar en él."
                    : "Sin resultados.";
        }

        private static string Highlight(string snippet, string query)
        {
            var index = snippet.IndexOf(query, StringComparison.Ordinal);
            return index < 0 ? snippet : snippet.Substring(0, index) + "<b>" + query + "</b>" + snippet.Substring(index + query.Length);
        }

        public override void OnClose()
        {
            _renderVersion++;
            _searchVersion++;
            if (_texture != null)
                UnityEngine.Object.Destroy(_texture);
            _texture = null;
            var document = _document;
            _document = null;
            if (document != null)
                Task.Run(() => document.Dispose());
        }
    }
}
