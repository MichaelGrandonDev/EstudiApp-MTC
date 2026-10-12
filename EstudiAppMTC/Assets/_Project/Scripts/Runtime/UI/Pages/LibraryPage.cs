using System;
using System.Collections.Generic;
using System.Linq;
using EstudiApp.Domain;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Pages
{
    /// <summary>Biblioteca: los PDF del estudiante y la bibliografía en la que se basa el temario.</summary>
    public class LibraryPage : Page
    {
        private static readonly (string role, string title)[] Roles =
        {
            ("principal", "Obras principales"),
            ("clasico", "Clásicos"),
            ("complementaria", "Complementarias"),
            ("apuntes", "Apuntes de la Escuela Neidan")
        };

        private Dictionary<string, int> _citations;

        public override string Title => "Biblioteca";

        public override void Build()
        {
            Root.Clear();
            var curriculum = S.Curriculum;
            var library = S.Library.Library;
            _citations ??= CountCitations(curriculum);

            var head = Root.Append(El.Row("page-head"));
            var titles = head.Append(El.Div("page-head__text"));
            titles.Append(El.Text("Biblioteca", "page-title"));
            titles.Append(El.Text("Sube tus libros en PDF: se guardan sólo en este dispositivo. Los que coincidan con la bibliografía se enlazan solos y las citas de las lecciones los abren en la página exacta.", "page-lead"));
            head.Append(El.IconBtn(IconKind.Upload, "Subir PDF", () => Shell.ImportPdf(), "btn"));

            Root.Append(El.Text("Mis libros", "section-title"));
            if (library.Books.Count == 0)
            {
                var empty = Root.Append(El.Div("empty"));
                empty.Append(new Icon(IconKind.Library));
                empty.Append(El.Text("Todavía no subiste ningún libro.", "empty__title"));
                empty.Append(El.Text("Empieza por los de referencia: Li Ping, Maciocia, Su Wen y el atlas de Focks.", "empty__text"));
            }
            else
            {
                var list = Root.Append(El.Div("books"));
                foreach (var book in library.Books.OrderByDescending(b => b.Added))
                    list.Add(UserBookCard(book));
            }
            if (S.Entitlements.Mode == AccessMode.Free)
                Root.Append(El.Text($"Plan gratuito: hasta {EntitlementPolicy.FreePdfLimit} libros.", "muted"));

            Root.Append(El.Text("Bibliografía del temario", "section-title"));
            Root.Append(El.Text("Los resúmenes están escritos con palabras propias a partir de estas obras y citan su página. Para leer el texto original, consigue tu ejemplar y súbelo.", "muted"));
            foreach (var (role, title) in Roles)
            {
                var books = curriculum.Books.Where(b => b.Role == role).ToList();
                if (books.Count == 0)
                    continue;
                Root.Append(El.Text(title, "subsection-title"));
                var list = Root.Append(El.Div("books"));
                foreach (var info in books)
                    list.Add(BibliographyCard(info));
            }
        }

        private VisualElement UserBookCard(UserBook book)
        {
            var info = S.Curriculum.Book(book.BibliographyId);
            var card = El.Div("book-card");
            card.Append(new Icon(IconKind.Book)).AddToClassList("book-card__icon");
            var body = card.Append(El.Div("book-card__body"));
            body.Append(El.Text(book.Title, "book-card__title"));
            body.Append(El.Text($"{book.Pages} páginas · {book.Size / (1024f * 1024f):0.#} MB · {book.OriginalName}", "book-card__meta"));
            body.Append(info != null
                ? El.Text("Enlazado con: " + Refs.Short(info.Id, S.Curriculum), "pill", "pill--jade")
                : El.Text("Sin enlazar con la bibliografía", "pill"));
            if (book.LastPage > 1)
                body.Append(El.Text($"Ibas por la página {book.LastPage}", "muted"));
            var actions = card.Append(El.Div("book-card__actions"));
            actions.Append(El.Btn("Leer", () => Shell.Push(new ReaderPage(book, book.LastPage, null)), "btn", "btn--small"));
            actions.Append(El.IconBtn(IconKind.Link, null, () => ChooseLink(Shell, book, Build), "btn--ghost", "icon-btn--small"));
            actions.Append(El.IconBtn(IconKind.Trash, null, () => Shell.Confirm("Eliminar libro",
                $"Se borrará «{book.Title}» de esta app (el archivo original no se toca).", "Eliminar", () =>
                {
                    S.Library.Remove(book);
                    Build();
                }), "btn--ghost", "icon-btn--small"));
            return card;
        }

        private VisualElement BibliographyCard(BookInfo info)
        {
            var owned = S.Library.Library.ForBibliography(info.Id);
            var card = El.Div("book-card", "book-card--bib");
            var body = card.Append(El.Div("book-card__body"));
            body.Append(El.Text(info.Title, "book-card__title"));
            var meta = string.Join(" · ", new[] { info.Author, info.Publisher, info.Year }.Where(s => !string.IsNullOrEmpty(s)));
            if (meta.Length > 0)
                body.Append(El.Text(meta, "book-card__meta"));
            if (!string.IsNullOrEmpty(info.Description))
                body.Append(El.Text(info.Description, "book-card__text"));
            if (_citations.TryGetValue(info.Id, out var count) && count > 0)
                body.Append(El.Text($"{count} cita{(count == 1 ? "" : "s")} en el temario", "muted"));
            var actions = card.Append(El.Div("book-card__actions"));
            if (owned != null)
                actions.Append(El.Btn("Abrir", () => Shell.Push(new ReaderPage(owned, owned.LastPage, null)), "btn", "btn--small"));
            else
                actions.Append(El.IconBtn(IconKind.Upload, "Subir", () => Shell.ImportPdf(added =>
                {
                    if (added.BibliographyId == null)
                        S.Library.Link(added, info.Id);
                    Build();
                }), "btn--ghost", "btn--small"));
            return card;
        }

        /// <summary>Diálogo para enlazar un PDF con un libro de la bibliografía.</summary>
        public static void ChooseLink(AppShell shell, UserBook book, Action done)
        {
            var services = shell.Services;
            var body = shell.Dialog("Enlazar con la bibliografía",
                "Elige a qué libro corresponde este PDF. Las citas de ese libro se abrirán aquí.");
            var list = body.Append(El.Div("link-list"));
            list.Append(El.Btn("Ninguno", () =>
            {
                shell.CloseOverlay();
                services.Library.Link(book, null);
                done?.Invoke();
            }, "option", book.BibliographyId == null ? "option--chosen" : null));
            foreach (var info in services.Curriculum.Books)
                list.Append(El.Btn(info.Title + (string.IsNullOrEmpty(info.Author) ? "" : " — " + info.Author), () =>
                {
                    shell.CloseOverlay();
                    services.Library.Link(book, info.Id);
                    done?.Invoke();
                }, "option", book.BibliographyId == info.Id ? "option--chosen" : null));
        }

        private static Dictionary<string, int> CountCitations(Curriculum curriculum)
        {
            var counts = new Dictionary<string, int>();
            void Count(Reference r)
            {
                if (r?.Book == null)
                    return;
                counts.TryGetValue(r.Book, out var n);
                counts[r.Book] = n + 1;
            }
            foreach (var lesson in curriculum.AllLessons)
            {
                lesson.References.ForEach(Count);
                foreach (var block in lesson.Blocks)
                    Count(block.Source);
            }
            foreach (var diagram in curriculum.Diagrams)
                diagram.References.ForEach(Count);
            return counts;
        }
    }
}
