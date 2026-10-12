using System;
using System.Collections.Generic;
using System.Linq;

namespace EstudiApp.Domain
{
    /// <summary>Libros PDF que el estudiante subió a la app (se guardan en su dispositivo).</summary>
    public class UserLibrary
    {
        public int Version = 1;
        public List<UserBook> Books = new List<UserBook>();

        public UserBook Book(string id) => Books.FirstOrDefault(b => b.Id == id);

        /// <summary>El PDF del estudiante asociado a un libro de la bibliografía, si lo subió.</summary>
        public UserBook ForBibliography(string bookId) =>
            bookId == null ? null : Books.FirstOrDefault(b => b.BibliographyId == bookId);
    }

    public class UserBook
    {
        public string Id;
        public string Title;
        /// <summary>Nombre del archivo dentro de la carpeta de la biblioteca.</summary>
        public string FileName;
        public string OriginalName;
        public long Size;
        public int Pages;
        public int LastPage = 1;
        /// <summary>Libro de la bibliografía al que corresponde (para abrir las citas de las lecciones).</summary>
        public string BibliographyId;
        /// <summary>Diferencia entre la página del PDF de referencia y la de este PDF (si es otra edición o escaneo).</summary>
        public int PageOffset;
        public DateTime Added;
        public List<Bookmark> Bookmarks = new List<Bookmark>();
    }

    public class Bookmark
    {
        public int Page;
        public string Label;
        public DateTime Created;
    }
}
