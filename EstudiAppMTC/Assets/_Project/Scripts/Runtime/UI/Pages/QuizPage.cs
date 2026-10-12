using System.Collections.Generic;
using System.Linq;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Pages
{
    /// <summary>Evaluación de una lección, pregunta por pregunta, con explicación inmediata.</summary>
    public class QuizPage : Page
    {
        private readonly Lesson _lesson;
        private readonly Dictionary<string, bool> _answers = new Dictionary<string, bool>();
        private int _index;
        private int _combo;
        private int _bestCombo;

        public QuizPage(Lesson lesson)
        {
            _lesson = lesson;
        }

        public override string Title => "Evaluación";

        public override void Build()
        {
            if (Root.childCount == 0)
                ShowQuestion();
        }

        private void ShowQuestion()
        {
            Root.Clear();
            var color = El.Hex(S.Curriculum.AxisOf(_lesson.Id).Color);
            var quiz = Root.Append(El.Div("quiz"));
            quiz.Append(El.Text(_lesson.Title, "quiz__lesson")).Ink(color);
            var top = quiz.Append(El.Row("quiz__top"));
            top.Append(El.Text($"Pregunta {_index + 1} de {_lesson.Quiz.Count}", "quiz__count"));
            top.Append(El.Spacer());
            var combo = top.Append(El.Row("combo-chip", _combo >= Rewards.ComboStart ? "combo-chip--hot" : null));
            combo.Append(new Icon(IconKind.Flame));
            combo.Append(El.Text($"×{_combo}", "combo-chip__text"));
            combo.SetVisible(_combo >= 2);
            var count = top.Append(El.Text($"{_answers.Count(a => a.Value)} correctas", "muted"));
            quiz.Append(El.Bar((float)_index / _lesson.Quiz.Count, color, "quiz__bar"));

            var question = _lesson.Quiz[_index];
            VisualElement next = null;
            quiz.Append(new QuestionView(question, correct =>
            {
                _answers[question.Id] = correct;
                _combo = correct ? _combo + 1 : 0;
                _bestCombo = Mathf.Max(_bestCombo, _combo);
                count.text = $"{_answers.Count(a => a.Value)} correctas";
                combo.Q<Label>().text = $"×{_combo}";
                combo.SetVisible(_combo >= 2);
                combo.EnableInClassList("combo-chip--hot", _combo >= Rewards.ComboStart);
                if (correct)
                    Anim.FloatUp(top, $"+{Rewards.XpPerCorrect} XP", "float-text--xp");
                if (_combo >= 2)
                    Anim.Pop(combo);
                next.SetVisible(true);
                next.Enter();
            })).Enter(40);
            var last = _index == _lesson.Quiz.Count - 1;
            next = quiz.Append(El.Btn(last ? "Ver resultado" : "Siguiente", () =>
            {
                if (last)
                    Finish();
                else
                {
                    _index++;
                    ShowQuestion();
                }
            }, "btn", "quiz__next"));
            next.SetVisible(false);
        }

        private void Finish()
        {
            var result = S.Progress.RecordQuiz(_lesson.Id, _answers, _bestCombo);
            Shell.Replace(new QuizResultPage(_lesson, result));
        }
    }

    public class QuizResultPage : Page
    {
        private readonly Lesson _lesson;
        private readonly QuizResult _result;

        public QuizResultPage(Lesson lesson, QuizResult result)
        {
            _lesson = lesson;
            _result = result;
        }

        public override string Title => "Resultado";

        public override void Build()
        {
            Root.Clear();
            var curriculum = S.Curriculum;
            var axis = curriculum.AxisOf(_lesson.Id);
            var box = ResultView.Build(Shell, Root, _result, El.Hex(axis.Color),
                _result.Passed ? "¡Lección dominada!" : "Casi: repasa y vuelve a intentarlo",
                $"{_result.Correct} de {_result.Total} respuestas correctas. " +
                (_result.Passed
                    ? "Las preguntas pasan a tu repaso espaciado para que no se olviden."
                    : $"Necesitas {Mathf.RoundToInt(ProgressService.PassScore * 100)} % para avanzar. Relee la lección (sobre todo «Para recordar») y repite la evaluación."));

            var delay = 1200L;
            foreach (var id in _result.UnlockedAxes)
            {
                var unlocked = curriculum.Axis(id);
                if (unlocked != null)
                    box.Append(El.Text($"Se abrió un nuevo eje: {unlocked.Title}", "pill", "pill--gold")).PopIn(delay += 150);
            }
            foreach (var id in _result.UnlockedLessons.Take(3))
            {
                var unlocked = curriculum.Lesson(id);
                if (unlocked != null)
                    box.Append(El.Text($"Nuevo desafío disponible: {unlocked.Title}", "pill", "pill--jade")).PopIn(delay += 150);
            }

            var actions = box.Append(El.Row("result__actions"));
            actions.Append(El.Btn("Volver a la lección", () => Shell.Replace(new LessonPage(_lesson)), "btn", "btn--ghost"));
            if (!_result.Passed)
                actions.Append(El.Btn("Reintentar", () => Shell.Replace(new QuizPage(_lesson)), "btn"));
            else
            {
                var next = S.Challenges.NextChallenge();
                if (next != null && next.Lesson != _lesson)
                    actions.Append(El.Btn((next.Kind == ChallengeKind.Lesson ? "Siguiente: " : "¡Nuevo desafío! ") + next.Title, () =>
                    {
                        if (next.Kind == ChallengeKind.Lesson)
                            Shell.Replace(new LessonPage(next.Lesson));
                        else
                        {
                            Shell.Back();
                            Shell.OpenChallenge(next);
                        }
                    }, "btn"));
            }
        }
    }
}
