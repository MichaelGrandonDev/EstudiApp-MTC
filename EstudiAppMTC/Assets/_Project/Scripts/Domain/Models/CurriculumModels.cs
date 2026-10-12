using System.Collections.Generic;
using System.Linq;

namespace EstudiApp.Domain
{
    /// <summary>Todo el contenido de estudio: ejes temáticos, diagramas, bibliografía y glosario.</summary>
    public class Curriculum
    {
        public List<Axis> Axes = new List<Axis>();
        public List<DiagramSpec> Diagrams = new List<DiagramSpec>();
        public List<BookInfo> Books = new List<BookInfo>();
        public List<Term> Glossary = new List<Term>();

        private Dictionary<string, Lesson> _lessons;
        private Dictionary<string, Axis> _axisOfLesson;

        public IEnumerable<Lesson> AllLessons => Axes.SelectMany(a => a.Lessons);

        public Axis Axis(string id) => Axes.FirstOrDefault(a => a.Id == id);

        public Lesson Lesson(string id)
        {
            Index();
            return id != null && _lessons.TryGetValue(id, out var lesson) ? lesson : null;
        }

        public Axis AxisOf(string lessonId)
        {
            Index();
            return lessonId != null && _axisOfLesson.TryGetValue(lessonId, out var axis) ? axis : null;
        }

        public DiagramSpec Diagram(string id) => Diagrams.FirstOrDefault(d => d.Id == id);

        public BookInfo Book(string id) => Books.FirstOrDefault(b => b.Id == id);

        /// <summary>Lecciones que muestran este diagrama (para enlazar desde la galería).</summary>
        public IEnumerable<Lesson> LessonsWithDiagram(string diagramId) =>
            AllLessons.Where(l => l.DiagramIds.Contains(diagramId));

        private void Index()
        {
            if (_lessons != null)
                return;
            _lessons = new Dictionary<string, Lesson>();
            _axisOfLesson = new Dictionary<string, Axis>();
            foreach (var axis in Axes)
                foreach (var lesson in axis.Lessons)
                {
                    _lessons[lesson.Id] = lesson;
                    _axisOfLesson[lesson.Id] = axis;
                }
        }
    }

    /// <summary>Eje temático (materia): una ruta de lecciones con sus prerrequisitos.</summary>
    public class Axis
    {
        public string Id;
        public string Title;
        public string Hanzi;
        public string Pinyin;
        public string Subtitle;
        public string Description;
        public string Color = "#2E7D6B";
        /// <summary>1 = cimientos, 2 = intermedio, 3 = clínico/avanzado.</summary>
        public int Level = 1;
        public bool Premium;
        /// <summary>Ejes que hay que dominar antes de empezar este.</summary>
        public List<string> Requires = new List<string>();
        public List<Unit> Units = new List<Unit>();

        public IEnumerable<Lesson> Lessons => Units.SelectMany(u => u.Lessons);
    }

    public class Unit
    {
        public string Id;
        public string Title;
        public List<Lesson> Lessons = new List<Lesson>();
    }

    public class Lesson
    {
        public string Id;
        public string Title;
        public string Hanzi;
        public string Pinyin;
        public int Minutes = 10;
        /// <summary>Idea central en una o dos frases.</summary>
        public string Summary;
        public List<LessonBlock> Blocks = new List<LessonBlock>();
        public List<string> KeyPoints = new List<string>();
        public List<Term> Terms = new List<Term>();
        public List<string> DiagramIds = new List<string>();
        public List<Reference> References = new List<Reference>();
        public List<Question> Quiz = new List<Question>();
    }

    /// <summary>Bloque de una lección. Kind: text, list, table, quote, note, clinical, diagram.</summary>
    public class LessonBlock
    {
        public string Kind = "text";
        public string Title;
        public string Text;
        public List<string> Items = new List<string>();
        /// <summary>Tablas: la primera fila es el encabezado.</summary>
        public List<List<string>> Rows = new List<List<string>>();
        public string DiagramId;
        public Reference Source;
    }

    public class Term
    {
        public string Hanzi;
        public string Pinyin;
        public string Es;
        public string Definition;
    }

    /// <summary>Cita a un libro de la bibliografía. Pages: páginas impresas ("19-21"); Pdf: página del PDF original.</summary>
    public class Reference
    {
        public string Book;
        public string Pages;
        public int Pdf;
        public string Note;
    }

    /// <summary>Kind: single (una correcta), multi (varias), truefalse.</summary>
    public class Question
    {
        public string Id;
        public string Kind = "single";
        public string Prompt;
        public List<string> Options = new List<string>();
        public List<int> Answer = new List<int>();
        public string Explain;

        public bool IsCorrect(IEnumerable<int> chosen)
        {
            var set = new HashSet<int>(chosen ?? Enumerable.Empty<int>());
            return set.SetEquals(Answer);
        }
    }

    public class BookInfo
    {
        public string Id;
        public string Title;
        public string Author;
        public string Publisher;
        public string Year;
        /// <summary>principal, complementaria o clasico.</summary>
        public string Role = "complementaria";
        public string Description;
        /// <summary>Palabras del nombre de archivo o del título que identifican el PDF del usuario.</summary>
        public List<string> Match = new List<string>();
        /// <summary>Páginas del PDF de referencia (el que se usó para citar); sirve para reconocer el mismo archivo.</summary>
        public int PdfPages;
    }
}
