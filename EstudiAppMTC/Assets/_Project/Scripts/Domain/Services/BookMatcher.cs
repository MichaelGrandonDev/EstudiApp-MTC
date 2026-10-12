using System.Globalization;
using System.Linq;
using System.Text;

namespace EstudiApp.Domain
{
    /// <summary>Reconoce a qué libro de la bibliografía corresponde un PDF subido, por nombre y número de páginas.</summary>
    public static class BookMatcher
    {
        public static BookInfo Match(Curriculum curriculum, string fileName, int pages)
        {
            var name = Normalize(fileName);
            BookInfo best = null;
            var bestScore = 0;
            foreach (var book in curriculum.Books)
            {
                var score = 0;
                foreach (var keyword in book.Match)
                {
                    var words = Normalize(keyword).Split(' ').Where(w => w.Length > 0).ToArray();
                    if (words.Length > 0 && words.All(w => name.Contains(w)))
                        score += 2 * words.Length;
                }
                if (score > 0 && book.PdfPages > 0 && pages == book.PdfPages)
                    score += 3;
                if (score > bestScore)
                {
                    best = book;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>Minúsculas sin tildes ni signos, palabras separadas por un espacio.</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            var lastSpace = true;
            foreach (var c in decomposed)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark)
                    continue;
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                    lastSpace = false;
                }
                else if (!lastSpace)
                {
                    sb.Append(' ');
                    lastSpace = true;
                }
            }
            return sb.ToString().Trim();
        }
    }
}
