using System.Collections.Generic;
using System.Linq;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Pages
{
    /// <summary>Repaso espaciado (Leitner): las preguntas vuelven a los 1, 3, 7, 16 y 35 días si se aciertan.</summary>
    public class ReviewPage : Page
    {
        private List<ReviewCard> _session;
        private int _index;
        private int _right;

        public override string Title => "Repaso";

        public override void Build()
        {
            if (_session != null && _index < _session.Count)
                return;
            _session = null;
            Overview();
        }

        private void Overview()
        {
            Root.Clear();
            var progress = S.Progress;
            var due = progress.DueCards();
            Root.Append(El.Text("Repaso espaciado", "page-title"));
            Root.Append(El.Text("Cada pregunta que respondes en una evaluación se convierte en una tarjeta. Si la aciertas, vuelve más tarde (1, 3, 7, 16 y 35 días); si fallas, vuelve mañana. Así lo aprendido se asienta.", "page-lead"));

            var cards = progress.State.Cards.Values.ToList();
            var boxes = Root.Append(El.Div("boxes"));
            string[] names = { "", "Nuevas o falladas", "En curso", "Afianzándose", "Sólidas", "Dominadas" };
            for (var box = 1; box <= ProgressService.MaxBox; box++)
            {
                var count = cards.Count(c => c.Box == box);
                var item = boxes.Append(El.Div("box"));
                item.Append(El.Text(count.ToString(), "box__count"));
                item.Append(El.Text(names[box], "box__label"));
                item.Append(El.Bar(cards.Count == 0 ? 0f : (float)count / cards.Count, Color.Lerp(Theme.Gold, Theme.Jade, (box - 1) / 4f), "box__bar"));
            }

            var card = Root.Append(El.Div("card", "review-start"));
            if (due.Count > 0)
            {
                card.Append(El.Text($"{due.Count} tarjeta{(due.Count == 1 ? "" : "s")} para hoy", "card__title"));
                card.Append(El.Text("Unos minutos al día bastan. Empieza por las más débiles.", "card__text"));
                card.Append(El.Row("card__footer")).Append(El.Btn("Empezar repaso", () => Start(due), "btn", "btn--gold"));
            }
            else if (cards.Count == 0)
            {
                card.Append(El.Text("Todavía no hay tarjetas", "card__title"));
                card.Append(El.Text("Haz la evaluación de tu primera lección y sus preguntas aparecerán aquí.", "card__text"));
                var next = progress.NextLesson();
                if (next != null)
                    card.Append(El.Row("card__footer")).Append(El.Btn("Ir a la lección", () => Shell.Open(Tab.Path, new LessonPage(next)), "btn"));
            }
            else
            {
                var nextDue = cards.Min(c => c.Due);
                card.Append(El.Text("¡Al día!", "card__title"));
                card.Append(El.Text($"No hay tarjetas pendientes. La próxima vuelve el {nextDue:dd/MM}.", "card__text"));
                card.Append(El.Row("card__footer")).Append(El.Btn("Repasar igualmente (10 al azar)", () =>
                    Start(cards.OrderBy(_ => Random.value).Take(10).ToList()), "btn", "btn--ghost"));
            }
        }

        private void Start(List<ReviewCard> cards)
        {
            _session = cards.Take(30).ToList();
            _index = 0;
            _right = 0;
            ShowCard();
        }

        private void ShowCard()
        {
            Root.Clear();
            if (_index >= _session.Count)
            {
                Finish();
                return;
            }
            var card = _session[_index];
            var question = S.Progress.QuestionOf(card);
            if (question == null)
            {
                _index++;
                ShowCard();
                return;
            }
            var lesson = S.Curriculum.Lesson(card.LessonId);
            var axis = S.Curriculum.AxisOf(card.LessonId);
            var color = El.Hex(axis.Color);
            var quiz = Root.Append(El.Div("quiz"));
            var top = quiz.Append(El.Row("quiz__top"));
            top.Append(El.Text($"Tarjeta {_index + 1} de {_session.Count}", "quiz__count"));
            top.Append(El.Spacer());
            top.Append(El.Text(lesson.Title, "muted")).Ink(color);
            quiz.Append(El.Bar((float)_index / _session.Count, Theme.Gold, "quiz__bar"));

            VisualElement next = null;
            quiz.Add(new QuestionView(question, correct =>
            {
                S.Progress.AnswerCard(card, correct);
                if (correct)
                    _right++;
                next.SetVisible(true);
            }));
            var row = quiz.Append(El.Row("quiz__footer"));
            row.Append(El.Btn("Ver la lección", () => Shell.OpenLesson(lesson), "btn", "btn--ghost"));
            row.Append(El.Spacer());
            next = row.Append(El.Btn(_index == _session.Count - 1 ? "Terminar" : "Siguiente", () =>
            {
                _index++;
                ShowCard();
            }, "btn"));
            next.SetVisible(false);
        }

        private void Finish()
        {
            var box = Root.Append(El.Div("result"));
            box.Append(new ProgressRing(_session.Count == 0 ? 1f : (float)_right / _session.Count, Theme.Gold)).AddToClassList("result__ring");
            box.Append(El.Text("Repaso terminado", "result__title"));
            box.Append(El.Text($"{_right} de {_session.Count} correctas. Las falladas vuelven mañana; las acertadas, más adelante.", "result__text"));
            var xp = box.Append(El.Row("xp-chip", "xp-chip--big"));
            xp.Append(new Icon(IconKind.Bolt));
            Anim.CountUp(xp.Append(El.Text("+0 XP", "xp-chip__text")), 0, _right * Rewards.XpReview, 700, v => $"+{v} XP", 300);
            box.Append(El.Row("result__actions")).Append(El.Btn("Volver", () =>
            {
                _session = null;
                Overview();
            }, "btn"));
        }
    }
}
