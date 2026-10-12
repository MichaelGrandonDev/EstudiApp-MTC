using System.Linq;
using EstudiApp.Domain;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Pages
{
    /// <summary>Búsqueda en lecciones, términos y diagramas; sin texto muestra el glosario completo.</summary>
    public class SearchPage : Page
    {
        private TextField _field;
        private VisualElement _results;
        private string _query = string.Empty;

        public override string Title => "Buscar";

        public override void Build()
        {
            if (_field != null)
                return;
            Root.Clear();
            _field = Root.Append(new TextField { value = _query });
            _field.AddToClassList("search-field");
            _field.AddToClassList("search-field--big");
            _field.textEdition.placeholder = "Buscar: Qi, Shen, 肝, Bazo, cinco elementos…";
            _field.RegisterValueChangedCallback(e =>
            {
                _query = e.newValue;
                Results();
            });
            _results = Root.Append(El.Div("search-results"));
            Results();
            _field.schedule.Execute(() => _field.Focus());
        }

        private void Results()
        {
            _results.Clear();
            var curriculum = S.Curriculum;
            if (_query.Trim().Length < 2)
            {
                _results.Append(El.Text($"Glosario · {curriculum.Glossary.Count} términos", "section-title"));
                var terms = _results.Append(El.Div("terms"));
                foreach (var term in curriculum.Glossary.OrderBy(t => BookMatcher.Normalize(t.Pinyin)))
                    terms.Add(TermView(term));
                return;
            }
            var hits = S.Search.Search(_query, 60);
            if (hits.Count == 0)
            {
                _results.Append(El.Text("Sin resultados. Prueba con otra palabra o con el pinyin sin tildes.", "muted"));
                return;
            }
            foreach (var group in hits.GroupBy(h => h.Kind))
            {
                _results.Append(El.Text(group.Key == SearchKind.Lesson ? "Lecciones" : group.Key == SearchKind.Diagram ? "Diagramas" : "Términos", "section-title"));
                var list = _results.Append(El.Div(group.Key == SearchKind.Term ? "terms" : "lesson-list"));
                foreach (var hit in group)
                {
                    switch (hit.Kind)
                    {
                        case SearchKind.Term:
                            var term = curriculum.Glossary.FirstOrDefault(t => t.Pinyin == hit.Id);
                            if (term != null)
                                list.Add(TermView(term));
                            break;
                        default:
                            var row = list.Append(El.Div("lesson-row"));
                            var badge = row.Append(El.Div("lesson-row__badge"));
                            badge.Append(new Icon(hit.Kind == SearchKind.Lesson ? IconKind.Book : IconKind.Diagram));
                            var body = row.Append(El.Div("lesson-row__body"));
                            body.Append(El.Text(hit.Title, "lesson-row__title"));
                            if (!string.IsNullOrEmpty(hit.Detail))
                                body.Append(El.Text(hit.Detail, "lesson-row__meta"));
                            row.Append(new Icon(IconKind.Forward)).AddToClassList("lesson-row__chevron");
                            var id = hit.Id;
                            var kind = hit.Kind;
                            row.RegisterCallback<ClickEvent>(_ =>
                            {
                                if (kind == SearchKind.Lesson)
                                    Shell.OpenLesson(curriculum.Lesson(id));
                                else
                                    Shell.OpenDiagram(id);
                            });
                            break;
                    }
                }
            }
        }

        private static VisualElement TermView(Term term)
        {
            var row = El.Div("term");
            row.Append(El.Text(term.Hanzi, "term__hanzi", "hanzi"));
            var body = row.Append(El.Div("term__body"));
            body.Append(El.Text($"<b>{term.Pinyin}</b> · {term.Es}", "term__name"));
            body.Append(El.Text(term.Definition, "term__definition"));
            return row;
        }
    }
}
