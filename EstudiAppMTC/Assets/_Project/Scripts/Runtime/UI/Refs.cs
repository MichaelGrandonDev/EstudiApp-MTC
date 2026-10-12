using System;
using System.Collections.Generic;
using EstudiApp.Domain;
using UnityEngine.UIElements;

namespace EstudiApp.UI
{
    /// <summary>Cómo se muestran las citas a la bibliografía.</summary>
    public static class Refs
    {
        private static readonly Dictionary<string, string> ShortNames = new Dictionary<string, string>
        {
            { "liping", "Li Ping" },
            { "maciocia", "Maciocia" },
            { "suwen", "Su Wen" },
            { "focks", "Focks" },
            { "practica", "Maciocia · Práctica" },
            { "lengua", "Maciocia · Lengua" },
            { "psique", "Maciocia · Psique" },
            { "compendio", "Marié" },
            { "pulso", "Guía del pulso" },
            { "acupmoxi", "Acupuntura y moxibustión" }
        };

        public static string Short(string bookId, Curriculum curriculum)
        {
            if (bookId == null)
                return string.Empty;
            if (ShortNames.TryGetValue(bookId, out var name))
                return name;
            var book = curriculum?.Book(bookId);
            if (book == null)
                return bookId;
            var title = book.Title.Replace("(apuntes)", string.Empty).Trim();
            return bookId.StartsWith("neidan") ? "Neidan · " + title : title;
        }

        public static string Pages(Reference reference)
        {
            if (string.IsNullOrEmpty(reference.Pages))
                return reference.Pdf > 0 ? "pág. PDF " + reference.Pdf : string.Empty;
            return reference.Pages.StartsWith("cap") ? reference.Pages : "p. " + reference.Pages;
        }

        public static string Format(Reference reference, Curriculum curriculum)
        {
            var pages = Pages(reference);
            return Short(reference.Book, curriculum) + (pages.Length > 0 ? ", " + pages : string.Empty);
        }

        /// <summary>Botón de cita: abre el PDF del estudiante en esa página (o le ofrece subirlo).</summary>
        public static Button Button(Reference reference, Curriculum curriculum, Action<Reference> open, bool withNote = false)
        {
            var text = Format(reference, curriculum);
            if (withNote && !string.IsNullOrEmpty(reference.Note))
                text += $"  <color={Theme.MutedHex}>— {reference.Note}</color>";
            var button = El.Btn(string.Empty, () => open?.Invoke(reference), "ref-btn");
            button.Add(new Icon(IconKind.Book));
            button.Append(El.Text(text, "ref-btn__text")).pickingMode = PickingMode.Ignore;
            return button;
        }
    }
}
