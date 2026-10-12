using System.Linq;
using EstudiApp.Domain;
using EstudiApp.UI.Diagrams;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Pages
{
    /// <summary>Galería de todos los diagramas interactivos, filtrable por eje.</summary>
    public class DiagramsPage : Page
    {
        private string _axisFilter;

        public override string Title => "Diagramas";

        public override void Build()
        {
            Root.Clear();
            var curriculum = S.Curriculum;
            Root.Append(El.Text("Diagramas interactivos", "page-title"));
            Root.Append(El.Text("Toca, enciende capas y sigue los recorridos guiados. Cada diagrama enlaza con sus lecciones y con las páginas de los libros.", "page-lead"));

            var chips = Root.Append(El.Div("chips"));
            chips.Add(El.Chip("Todos", () => SetFilter(null), _axisFilter == null));
            foreach (var axis in curriculum.Axes)
                chips.Add(El.Chip(axis.Title, () => SetFilter(axis.Id), _axisFilter == axis.Id, El.Hex(axis.Color)));

            var grid = Root.Append(El.Div("gallery"));
            foreach (var spec in curriculum.Diagrams)
            {
                var lessons = curriculum.LessonsWithDiagram(spec.Id).ToList();
                var axis = lessons.Count > 0 ? curriculum.AxisOf(lessons[0].Id) : null;
                if (_axisFilter != null && (axis == null || axis.Id != _axisFilter))
                    continue;
                var card = grid.Append(El.Div("gallery__item"));
                var canvas = card.Append(DiagramCanvas.Create(spec));
                canvas.AddToClassList("gallery__canvas");
                canvas.MakeStatic();
                card.Append(El.Text(spec.Title, "gallery__title"));
                if (axis != null)
                    card.Append(El.Text(axis.Title, "gallery__meta")).Ink(El.Hex(axis.Color));
                card.RegisterCallback<ClickEvent>(_ => Shell.Push(new DiagramPage(spec)));
            }
        }

        private void SetFilter(string axisId)
        {
            _axisFilter = axisId;
            Build();
        }
    }

    /// <summary>Un diagrama a pantalla completa con las lecciones donde aparece.</summary>
    public class DiagramPage : Page
    {
        private readonly DiagramSpec _spec;
        private bool _built;

        public DiagramPage(DiagramSpec spec)
        {
            _spec = spec;
        }

        public override string Title => _spec.Title;

        public override void Build()
        {
            if (_built)
                return;
            _built = true;
            Root.Clear();
            Root.AddToClassList("page--diagram");
            Root.Append(El.Text(_spec.Title, "page-title"));
            var panel = Root.Append(new DiagramPanel(_spec, S.Curriculum, Shell.OpenReference, showHeader: false));
            panel.AddToClassList("diagram--full");

            var lessons = S.Curriculum.LessonsWithDiagram(_spec.Id).ToList();
            if (lessons.Count == 0)
                return;
            Root.Append(El.Text("Aparece en", "section-title"));
            var list = Root.Append(El.Div("lesson-list"));
            foreach (var lesson in lessons)
            {
                var axis = S.Curriculum.AxisOf(lesson.Id);
                var row = list.Append(El.Div("lesson-row"));
                var badge = row.Append(El.Div("lesson-row__badge"));
                badge.Append(new Icon(IconKind.Book));
                badge.style.color = Theme.Readable(El.Hex(axis.Color));
                var body = row.Append(El.Div("lesson-row__body"));
                body.Append(El.Text(lesson.Title, "lesson-row__title"));
                body.Append(El.Text(axis.Title, "lesson-row__meta"));
                row.Append(new Icon(IconKind.Forward)).AddToClassList("lesson-row__chevron");
                row.RegisterCallback<ClickEvent>(_ => Shell.OpenLesson(lesson));
            }
        }
    }
}
