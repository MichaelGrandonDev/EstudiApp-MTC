using System.Collections.Generic;
using System.Linq;

namespace EstudiApp.Domain
{
    public enum SearchKind
    {
        Lesson,
        Term,
        Diagram
    }

    public class SearchHit
    {
        public SearchKind Kind;
        public string Id;
        public string Title;
        public string Detail;
        public int Score;
    }

    /// <summary>Búsqueda en lecciones, glosario y diagramas (sin tildes ni mayúsculas, todas las palabras).</summary>
    public class SearchService
    {
        private readonly List<(SearchHit hit, string title, string body)> _entries = new List<(SearchHit, string, string)>();

        public SearchService(Curriculum curriculum)
        {
            foreach (var axis in curriculum.Axes)
                foreach (var lesson in axis.Lessons)
                {
                    var body = string.Join(" ", new[] { lesson.Summary, lesson.Pinyin, lesson.Hanzi }
                        .Concat(lesson.KeyPoints)
                        .Concat(lesson.Blocks.SelectMany(b => new[] { b.Title, b.Text }.Concat(b.Items).Concat(b.Rows.SelectMany(r => r))))
                        .Concat(lesson.Terms.SelectMany(t => new[] { t.Pinyin, t.Es, t.Hanzi })));
                    Add(new SearchHit { Kind = SearchKind.Lesson, Id = lesson.Id, Title = lesson.Title, Detail = axis.Title }, lesson.Title, body);
                }
            foreach (var term in curriculum.Glossary)
                Add(new SearchHit { Kind = SearchKind.Term, Id = term.Pinyin, Title = $"{term.Pinyin} · {term.Es}", Detail = term.Definition },
                    term.Pinyin + " " + term.Es, term.Hanzi + " " + term.Definition);
            foreach (var diagram in curriculum.Diagrams)
                Add(new SearchHit { Kind = SearchKind.Diagram, Id = diagram.Id, Title = diagram.Title, Detail = diagram.Intro },
                    diagram.Title, diagram.Intro + " " + string.Join(" ", diagram.Nodes.Select(n => n.Label + " " + n.Detail)));
        }

        private void Add(SearchHit hit, string title, string body) =>
            _entries.Add((hit, BookMatcher.Normalize(title), BookMatcher.Normalize(body)));

        public List<SearchHit> Search(string query, int limit = 40)
        {
            var words = BookMatcher.Normalize(query).Split(' ').Where(w => w.Length > 1).ToArray();
            if (words.Length == 0)
                return new List<SearchHit>();
            var hits = new List<SearchHit>();
            foreach (var (hit, title, body) in _entries)
            {
                var score = 0;
                var all = true;
                foreach (var word in words)
                {
                    if (title.Contains(word))
                        score += title.StartsWith(word) ? 6 : 4;
                    else if (body.Contains(word))
                        score += 1;
                    else
                    {
                        all = false;
                        break;
                    }
                }
                if (!all)
                    continue;
                hits.Add(new SearchHit { Kind = hit.Kind, Id = hit.Id, Title = hit.Title, Detail = hit.Detail, Score = score + (hit.Kind == SearchKind.Term ? 1 : 0) });
            }
            return hits.OrderByDescending(h => h.Score).ThenBy(h => h.Kind).Take(limit).ToList();
        }
    }
}
