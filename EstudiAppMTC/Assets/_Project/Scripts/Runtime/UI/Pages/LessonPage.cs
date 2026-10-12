using System.Collections.Generic;
using System.Linq;
using EstudiApp.Domain;
using EstudiApp.UI.Diagrams;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Pages
{
    /// <summary>Una lección: idea central, contenido, diagramas, puntos clave, términos, fuentes, notas y evaluación.</summary>
    public class LessonPage : Page
    {
        private readonly Lesson _lesson;
        private bool _built;
        private TextField _note;

        public LessonPage(Lesson lesson)
        {
            _lesson = lesson;
        }

        public override string Title => _lesson.Title;

        public override void Build()
        {
            if (_built)
            {
                RefreshFooter();
                return;
            }
            _built = true;
            Root.Clear();
            S.Progress.MarkOpened(_lesson.Id);
            var curriculum = S.Curriculum;
            var axis = curriculum.AxisOf(_lesson.Id);
            var unit = axis.Units.FirstOrDefault(u => u.Lessons.Contains(_lesson));
            var color = El.Hex(axis.Color);

            var article = Root.Append(El.Div("lesson"));
            var head = article.Append(El.Div("lesson__head"));
            head.Append(El.Text($"{axis.Title}{(unit != null ? " · " + unit.Title : "")}", "lesson__crumb")).Ink(color);
            head.Append(El.Text(_lesson.Title, "lesson__title"));
            if (!string.IsNullOrEmpty(_lesson.Hanzi))
            {
                var line = head.Append(El.Row("lesson__hanzi-line"));
                line.Append(El.Text(_lesson.Hanzi, "lesson__hanzi", "hanzi")).Ink(color);
                line.Append(El.Text(_lesson.Pinyin, "lesson__pinyin"));
            }
            head.Append(El.Text($"{_lesson.Minutes} min de lectura · {_lesson.Quiz.Count} preguntas", "muted"));

            var summary = article.Append(El.Div("summary"));
            summary.style.borderLeftColor = color;
            summary.Append(El.Text("Idea central", "summary__label")).Ink(color);
            summary.Append(El.Text(_lesson.Summary, "summary__text"));

            var shownDiagrams = new HashSet<string>();
            foreach (var block in _lesson.Blocks)
                article.Add(BlockView(block, shownDiagrams));
            foreach (var id in _lesson.DiagramIds.Where(id => !shownDiagrams.Contains(id)))
            {
                var spec = curriculum.Diagram(id);
                if (spec != null)
                    article.Add(new DiagramPanel(spec, curriculum, Shell.OpenReference));
            }

            if (_lesson.KeyPoints.Count > 0)
            {
                var keys = article.Append(El.Div("keypoints"));
                keys.Append(El.Text("Para recordar", "keypoints__title"));
                foreach (var point in _lesson.KeyPoints)
                {
                    var row = keys.Append(El.Row("keypoints__item"));
                    row.Append(new Icon(IconKind.Check)).Ink(color);
                    row.Append(El.Text(point, "keypoints__text"));
                }
            }

            if (_lesson.Terms.Count > 0)
            {
                article.Append(El.Text("Términos", "block__title"));
                var terms = article.Append(El.Div("terms"));
                foreach (var term in _lesson.Terms)
                {
                    var row = terms.Append(El.Div("term"));
                    row.Append(El.Text(term.Hanzi, "term__hanzi", "hanzi")).Ink(color);
                    var body = row.Append(El.Div("term__body"));
                    body.Append(El.Text($"<b>{term.Pinyin}</b> · {term.Es}", "term__name"));
                    body.Append(El.Text(term.Definition, "term__definition"));
                }
            }

            if (_lesson.References.Count > 0)
            {
                article.Append(El.Text("Para profundizar", "block__title"));
                article.Append(El.Text("Toca una cita para abrir tu PDF en esa página.", "muted"));
                var refs = article.Append(El.Div("refs"));
                foreach (var reference in _lesson.References)
                    refs.Add(Refs.Button(reference, curriculum, Shell.OpenReference, withNote: true));
            }

            article.Append(El.Text("Mis notas", "block__title"));
            _note = article.Append(new TextField { multiline = true, value = S.Progress.Note(_lesson.Id) });
            _note.AddToClassList("note-field");
            _note.textEdition.placeholder = "Escribe aquí tus apuntes, dudas o asociaciones…";
            _note.RegisterCallback<FocusOutEvent>(_ => SaveNote());

            article.Append(El.Div("lesson__footer"));
            RefreshFooter();
        }

        private void SaveNote()
        {
            if (_note != null && _note.value != S.Progress.Note(_lesson.Id))
                S.Progress.SetNote(_lesson.Id, _note.value);
        }

        public override void OnHide() => SaveNote();

        private void RefreshFooter()
        {
            var footer = Root.Q(className: "lesson__footer");
            if (footer == null)
                return;
            footer.Clear();
            var progress = S.Progress;
            var state = progress.Progress(_lesson.Id);
            var status = footer.Append(El.Row("lesson__status"));
            if (state != null && state.Completed)
            {
                status.Append(El.Text($"Lección dominada · mejor nota {Mathf.RoundToInt(state.BestScore * 100)}%", "pill", "pill--jade"));
                status.Add(ChallengeUi.Stars(Rewards.Stars(state.BestScore), 3, "stars--small"));
            }
            else if (state != null && state.Attempts > 0)
                status.Append(El.Text($"Mejor nota {Mathf.RoundToInt(state.BestScore * 100)}% · necesitas 70 % para avanzar", "pill", "pill--gold"));

            var actions = footer.Append(El.Row("lesson__actions"));
            if (state == null || !state.Read)
                actions.Append(El.Btn("Marcar como leída", () =>
                {
                    progress.MarkRead(_lesson.Id);
                    RefreshFooter();
                }, "btn", "btn--ghost"));
            actions.Append(El.Spacer());
            if (_lesson.Quiz.Count > 0)
                actions.Append(El.Btn(state != null && state.Completed ? "Repetir evaluación" : $"Hacer la evaluación ({_lesson.Quiz.Count})",
                    () => Shell.Push(new QuizPage(_lesson)), "btn"));

            var all = S.Curriculum.AxisOf(_lesson.Id).Lessons.ToList();
            var index = all.IndexOf(_lesson);
            var nav = footer.Append(El.Row("lesson__nav"));
            if (index > 0)
            {
                var prev = all[index - 1];
                nav.Append(El.IconBtn(IconKind.Back, prev.Title, () => Shell.Replace(new LessonPage(prev)), "btn--ghost", "lesson__nav-btn"));
            }
            nav.Append(El.Spacer());
            if (index >= 0 && index < all.Count - 1)
            {
                var next = all[index + 1];
                var button = nav.Append(El.Btn(string.Empty, () =>
                {
                    if (progress.CanOpen(next))
                        Shell.Replace(new LessonPage(next));
                    else
                        Shell.OpenLesson(next);
                }, "btn--ghost", "lesson__nav-btn", "lesson__nav-btn--next"));
                button.Append(El.Text(next.Title, "icon-btn__label")).pickingMode = PickingMode.Ignore;
                button.Append(new Icon(progress.CanOpen(next) ? IconKind.Forward : IconKind.Lock));
            }
        }

        private VisualElement BlockView(LessonBlock block, HashSet<string> shownDiagrams)
        {
            var curriculum = S.Curriculum;
            var view = El.Div("block", "block--" + block.Kind);
            if (!string.IsNullOrEmpty(block.Title))
                view.Append(El.Text(block.Title, "block__title"));
            switch (block.Kind)
            {
                case "list":
                    foreach (var item in block.Items)
                    {
                        var row = view.Append(El.Row("bullet"));
                        row.Append(El.Div("bullet__dot"));
                        row.Append(El.Text(item, "bullet__text"));
                    }
                    break;
                case "table":
                    var table = view.Append(El.Div("table"));
                    for (var r = 0; r < block.Rows.Count; r++)
                    {
                        var row = table.Append(El.Row("table__row", r == 0 ? "table__row--head" : null));
                        foreach (var cell in block.Rows[r])
                            row.Append(El.Text(cell, "table__cell"));
                    }
                    break;
                case "quote":
                    var quote = view.Append(El.Div("quote"));
                    quote.Append(El.Text("“", "quote__mark"));
                    quote.Append(El.Text(block.Text, "quote__text"));
                    if (block.Source != null && !string.IsNullOrEmpty(block.Source.Book))
                        quote.Add(Refs.Button(block.Source, curriculum, Shell.OpenReference, withNote: true));
                    break;
                case "note":
                case "clinical":
                    var clinical = block.Kind == "clinical";
                    var callout = view.Append(El.Div("callout", clinical ? "callout--clinical" : "callout--note"));
                    callout.Append(new Icon(clinical ? IconKind.Info : IconKind.Note));
                    var body = callout.Append(El.Div("callout__body"));
                    body.Append(El.Text(clinical ? "En la clínica" : "Nota", "callout__title"));
                    body.Append(El.Text(block.Text, "callout__text"));
                    break;
                case "diagram":
                    var spec = curriculum.Diagram(block.DiagramId);
                    if (spec != null)
                    {
                        shownDiagrams.Add(spec.Id);
                        view.Add(new DiagramPanel(spec, curriculum, Shell.OpenReference));
                    }
                    break;
                default:
                    view.Append(El.Text(block.Text, "paragraph"));
                    break;
            }
            return view;
        }
    }
}
