using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EstudiApp.Domain;
using EstudiApp.Pdf;
using UnityEngine;

namespace EstudiApp.Core
{
    public class ImportResult
    {
        public UserBook Book;
        public BookInfo Matched;
        public string Error;
    }

    /// <summary>Los PDF del estudiante: se copian a la carpeta de la app y se enlazan con la bibliografía.</summary>
    public class LibraryService
    {
        private readonly Curriculum _curriculum;
        private readonly JsonStore<UserLibrary> _store = new JsonStore<UserLibrary>("library.json");

        public UserLibrary Library { get; }
        public string Folder { get; }
        public event Action Changed;

        public LibraryService(Curriculum curriculum)
        {
            _curriculum = curriculum;
            Folder = Path.Combine(Application.persistentDataPath, "library");
            Directory.CreateDirectory(Folder);
            Library = _store.Load();
            Library.Books.RemoveAll(b => !File.Exists(FullPath(b)));
        }

        public string FullPath(UserBook book) => Path.Combine(Folder, book.FileName ?? string.Empty);

        public void Save()
        {
            _store.Save(Library);
            Changed?.Invoke();
        }

        /// <summary>Copia el PDF elegido, cuenta sus páginas y lo asocia al libro de la bibliografía que corresponda.</summary>
        public Task<ImportResult> Import(string sourcePath, string originalName, bool deleteSource)
        {
            var folder = Folder;
            return Task.Run(() =>
            {
                var id = Guid.NewGuid().ToString("N").Substring(0, 12);
                var fileName = id + ".pdf";
                var target = Path.Combine(folder, fileName);
                try
                {
                    File.Copy(sourcePath, target, true);
                    if (deleteSource)
                        TryDelete(sourcePath);
                    int pages;
                    string title;
                    using (var document = PdfDocument.Open(target))
                    {
                        pages = document.PageCount;
                        title = document.Title;
                    }
                    var name = string.IsNullOrEmpty(originalName) ? Path.GetFileName(sourcePath) : originalName;
                    var matched = BookMatcher.Match(_curriculum, name + " " + title, pages);
                    var book = new UserBook
                    {
                        Id = id,
                        FileName = fileName,
                        OriginalName = name,
                        Title = matched?.Title ?? CleanTitle(title, name),
                        Size = new FileInfo(target).Length,
                        Pages = pages,
                        BibliographyId = matched?.Id,
                        Added = DateTime.Now
                    };
                    return new ImportResult { Book = book, Matched = matched };
                }
                catch (Exception e)
                {
                    TryDelete(target);
                    return new ImportResult { Error = e is DllNotFoundException || e is EntryPointNotFoundException
                        ? "El lector de PDF no está disponible en esta plataforma."
                        : e.Message };
                }
            });
        }

        /// <summary>Se llama en el hilo principal con el resultado de <see cref="Import"/>.</summary>
        public void Add(UserBook book)
        {
            if (book.BibliographyId != null && Library.ForBibliography(book.BibliographyId) is UserBook previous)
                previous.BibliographyId = null;
            Library.Books.Add(book);
            Save();
        }

        public void Remove(UserBook book)
        {
            TryDelete(FullPath(book));
            Library.Books.Remove(book);
            Save();
        }

        public void Link(UserBook book, string bibliographyId)
        {
            foreach (var other in Library.Books.Where(b => b != book && bibliographyId != null && b.BibliographyId == bibliographyId))
                other.BibliographyId = null;
            book.BibliographyId = bibliographyId;
            var info = _curriculum.Book(bibliographyId);
            if (info != null)
                book.Title = info.Title;
            Save();
        }

        /// <summary>Página (desde 1) del PDF del estudiante para una cita; 0 si la cita no tiene página de PDF.</summary>
        public static int PageFor(Reference reference, UserBook book)
        {
            if (reference == null || reference.Pdf <= 0)
                return 0;
            var page = reference.Pdf + (book?.PageOffset ?? 0);
            return book != null && book.Pages > 0 ? Mathf.Clamp(page, 1, book.Pages) : Math.Max(1, page);
        }

        private static string CleanTitle(string title, string fileName)
        {
            if (!string.IsNullOrWhiteSpace(title) && title.Length > 3)
                return title;
            var name = Path.GetFileNameWithoutExtension(fileName ?? "Libro").Replace('_', ' ').Replace('-', ' ').Trim();
            return name.Length == 0 ? "Libro" : name;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
                // Si no se puede borrar una copia temporal no pasa nada.
            }
        }
    }
}
