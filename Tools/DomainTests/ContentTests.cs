using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EstudiApp.Domain;
using Xunit;

namespace EstudiApp.Tests
{
    /// <summary>Valida el temario generado (curriculum.json): ids, respuestas, citas y diagramas coherentes.</summary>
    public class ContentTests
    {
        private static readonly Lazy<Curriculum> Loaded = new Lazy<Curriculum>(() =>
            ContentParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "curriculum.json"))));

        private static Curriculum C => Loaded.Value;

        [Fact]
        public void HasTheCoreAxesInOrder()
        {
            Assert.Equal(new[] { "fundamentos", "fisiologia", "canales" }, C.Axes.Take(3).Select(a => a.Id));
            Assert.True(C.AllLessons.Count() >= 30);
            Assert.Contains(C.Books, b => b.Id == "liping" && b.Role == "principal");
            Assert.Contains(C.Books, b => b.Id == "maciocia" && b.Role == "principal");
        }

        [Fact]
        public void IdsAreUnique()
        {
            AssertUnique(C.Axes.Select(a => a.Id), "eje");
            AssertUnique(C.AllLessons.Select(l => l.Id), "lección");
            AssertUnique(C.AllLessons.SelectMany(l => l.Quiz).Select(q => q.Id), "pregunta");
            AssertUnique(C.Diagrams.Select(d => d.Id), "diagrama");
            AssertUnique(C.Books.Select(b => b.Id), "libro");
        }

        [Fact]
        public void AxisRequirementsExistAndHaveNoCycles()
        {
            foreach (var axis in C.Axes)
            {
                foreach (var id in axis.Requires)
                    Assert.True(C.Axis(id) != null, $"{axis.Id} requiere el eje inexistente {id}");
                var seen = new HashSet<string>();
                var pending = new Stack<string>(axis.Requires);
                while (pending.Count > 0)
                {
                    var id = pending.Pop();
                    Assert.True(id != axis.Id, $"Ciclo de requisitos en {axis.Id}");
                    if (seen.Add(id))
                        foreach (var next in C.Axis(id)?.Requires ?? new List<string>())
                            pending.Push(next);
                }
            }
        }

        [Fact]
        public void EveryLessonHasSummaryReferencesAndAQuiz()
        {
            foreach (var lesson in C.AllLessons)
            {
                Assert.False(string.IsNullOrWhiteSpace(lesson.Summary), $"{lesson.Id} sin idea central");
                Assert.True(lesson.Blocks.Count > 0, $"{lesson.Id} sin contenido");
                Assert.True(lesson.References.Count > 0, $"{lesson.Id} sin citas");
                Assert.True(lesson.Quiz.Count >= 3, $"{lesson.Id} con menos de 3 preguntas");
            }
        }

        [Fact]
        public void AnswersPointToExistingOptions()
        {
            foreach (var lesson in C.AllLessons)
                foreach (var q in lesson.Quiz)
                {
                    var where = $"{lesson.Id}/{q.Id}";
                    Assert.True(q.Options.Count >= 2, $"{where}: menos de 2 opciones");
                    Assert.True(q.Answer.Count >= 1, $"{where}: sin respuesta");
                    Assert.All(q.Answer, i => Assert.InRange(i, 0, q.Options.Count - 1));
                    Assert.Equal(q.Answer.Count, q.Answer.Distinct().Count());
                    if (q.Kind == "single" || q.Kind == "truefalse")
                        Assert.True(q.Answer.Count == 1, $"{where}: {q.Kind} con varias respuestas");
                    else
                        Assert.Equal("multi", q.Kind);
                    Assert.False(string.IsNullOrWhiteSpace(q.Explain), $"{where}: sin explicación");
                }
        }

        [Fact]
        public void ReferencesCiteKnownBooks()
        {
            var references = C.AllLessons.SelectMany(l => l.References.Select(r => (l.Id, r)))
                .Concat(C.AllLessons.SelectMany(l => l.Blocks.Where(b => b.Source != null).Select(b => (l.Id, b.Source))))
                .Concat(C.Diagrams.SelectMany(d => d.References.Select(r => (d.Id, r))))
                .Concat(C.Diagrams.SelectMany(d => d.Nodes.Where(n => n.Ref != null).Select(n => (d.Id, n.Ref))))
                .Concat(C.Diagrams.SelectMany(d => d.States.Where(s => s.Ref != null).Select(s => (d.Id, s.Ref))));
            foreach (var (owner, reference) in references)
            {
                var book = C.Book(reference.Book);
                Assert.True(book != null, $"{owner} cita el libro inexistente {reference.Book}");
                if (book.PdfPages > 0)
                    Assert.True(reference.Pdf <= book.PdfPages, $"{owner}: página {reference.Pdf} fuera de {book.Id} ({book.PdfPages})");
            }
        }

        [Fact]
        public void LessonsUseExistingDiagrams()
        {
            foreach (var lesson in C.AllLessons)
            {
                foreach (var id in lesson.DiagramIds)
                    Assert.True(C.Diagram(id) != null, $"{lesson.Id} usa el diagrama inexistente {id}");
                foreach (var block in lesson.Blocks.Where(b => b.Kind == "diagram"))
                    Assert.True(C.Diagram(block.DiagramId) != null, $"{lesson.Id} incrusta el diagrama inexistente {block.DiagramId}");
            }
        }

        [Fact]
        public void EveryDiagramIsUsedByALesson()
        {
            foreach (var diagram in C.Diagrams)
                Assert.True(C.LessonsWithDiagram(diagram.Id).Any(), $"El diagrama {diagram.Id} no aparece en ninguna lección");
        }

        [Fact]
        public void DiagramPartsAreConsistent()
        {
            foreach (var d in C.Diagrams)
            {
                Assert.Contains(d.Kind, new[] { "graph", "ring", "balance", "tongue" });
                var layers = new HashSet<string>(d.Layers.Select(l => l.Id));
                var parts = new HashSet<string>(d.Nodes.Select(n => n.Id).Concat(d.Segments.Select(s => s.Id)).Concat(d.Zones.Select(z => z.Id)));
                foreach (var edge in d.Edges)
                {
                    Assert.True(d.Node(edge.From) != null && d.Node(edge.To) != null, $"{d.Id}: flecha {edge.Key} con nodo inexistente");
                    Assert.True(edge.Layer == null || layers.Contains(edge.Layer), $"{d.Id}: flecha {edge.Key} en capa inexistente");
                }
                foreach (var node in d.Nodes)
                    Assert.True(node.Layer == null || layers.Contains(node.Layer), $"{d.Id}: nodo {node.Id} en capa inexistente");
                if (d.Layout != null)
                    Assert.All(d.Layout.Order, id => Assert.True(d.Node(id) != null, $"{d.Id}: {id} del orden circular no existe"));
                foreach (var step in d.Steps)
                {
                    Assert.All(step.Focus, id => Assert.True(parts.Contains(id), $"{d.Id}: el paso «{step.Title}» resalta {id}, que no existe"));
                    Assert.All(step.Layers ?? new List<string>(), id => Assert.True(layers.Contains(id), $"{d.Id}: el paso «{step.Title}» usa la capa {id}"));
                }
                if (d.Kind == "ring")
                    Assert.True(d.Segments.Count >= 2, $"{d.Id}: anillo sin segmentos");
                if (d.Kind == "balance")
                    Assert.Contains(d.States, s => s.Id == "equilibrio");
            }
        }

        [Fact]
        public void BalanceDiagramCoversEveryClassification()
        {
            var ids = new HashSet<string>(C.Diagrams.Where(d => d.Kind == "balance").SelectMany(d => d.States).Select(s => s.Id));
            var values = new[] { 0.4f, 1f, 1.6f };
            foreach (var yin in values)
                foreach (var yang in values)
                    Assert.Contains(YinYangBalance.Classify(yin, yang), ids);
        }

        [Fact]
        public void OrganClockFollowsTheTwoHourCycle()
        {
            var clock = C.Diagram("reloj-organos");
            Assert.NotNull(clock);
            Assert.True(clock.ShowClock);
            Assert.Equal(12, clock.Segments.Count);
            for (var i = 0; i < 12; i++)
                Assert.Equal((clock.StartHour + 2 * i) % 24, clock.Segments[i].Hour);
        }

        [Fact]
        public void SearchFindsLessonsTermsAndDiagrams()
        {
            var search = new SearchService(C);
            var hits = search.Search("wuxing");
            Assert.Contains(hits, h => h.Kind == SearchKind.Diagram);
            Assert.Contains(search.Search("pulmon"), h => h.Kind == SearchKind.Lesson);
            Assert.Contains(search.Search("qi"), h => h.Kind == SearchKind.Term);
            Assert.Empty(search.Search(""));
        }

        private static void AssertUnique(IEnumerable<string> ids, string what)
        {
            var duplicated = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(duplicated.Count == 0, $"Ids de {what} repetidos: {string.Join(", ", duplicated)}");
        }
    }
}
