using System;
using System.Collections.Generic;
using System.Linq;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Pages
{
    /// <summary>Portada de un desafío de unidad, examen de eje o mini test: qué incluye, récord, reloj opcional y empezar.</summary>
    public class ChallengeIntroPage : Page
    {
        private readonly Challenge _challenge;
        private bool _timed;

        public ChallengeIntroPage(Challenge challenge)
        {
            _challenge = challenge;
        }

        public override string Title => ChallengeUi.KindName(_challenge.Kind);

        public override void Build()
        {
            Root.Clear();
            var challenges = S.Challenges;
            var axis = _challenge.Axis;
            var color = El.Hex(axis.Color);
            var status = challenges.Status(_challenge);

            var box = Root.Append(El.Div("challenge-intro")).Enter();
            var emblem = box.Append(El.Div("challenge-intro__emblem")).PopIn(80);
            emblem.style.backgroundColor = color;
            emblem.Append(new Icon(ChallengeUi.KindIcon(_challenge.Kind)));
            box.Append(El.Text($"{ChallengeUi.KindName(_challenge.Kind)} · {axis.Title}", "challenge-intro__eyebrow")).Ink(color);
            box.Append(El.Text(_challenge.Kind == ChallengeKind.Practice ? $"Pon a prueba tu eje: {axis.Title}" : _challenge.Title, "challenge-intro__title"));
            box.Append(El.Text(Description(), "challenge-intro__text"));

            var chips = box.Append(El.Row("challenge-intro__chips"));
            chips.Append(El.Text($"{_challenge.Size} {(_challenge.Kind == ChallengeKind.Practice ? "ejercicios" : "preguntas")}", "pill"));
            chips.Append(El.Text($"Superar: {Mathf.RoundToInt(ProgressService.PassScore * 100)} %", "pill"));
            var attempts = challenges.Attempts(_challenge);
            if (attempts > 0)
                chips.Append(El.Text($"Tu mejor nota: {Mathf.RoundToInt(challenges.BestScore(_challenge) * 100)} %", "pill", "pill--jade"));
            if (_challenge.Kind == ChallengeKind.Axis && challenges.HasBadge(axis))
                chips.Append(El.Text("Sello obtenido", "pill", "pill--gold"));
            box.Append(ChallengeUi.Stars(challenges.Stars(_challenge), 3, "stars--big", animate: true, 300));

            var rewards = box.Append(El.Div("challenge-intro__rewards"));
            rewards.Append(El.Text("Recompensas", "challenge-intro__subtitle"));
            rewards.Append(El.Text("1 estrella con 70 % · 2 estrellas con 85 % · 3 estrellas sin fallos", "challenge-intro__line"));
            var bonus = _challenge.Kind == ChallengeKind.Unit ? $"+{Rewards.XpUnitPass} XP la primera vez que lo superas"
                : _challenge.Kind == ChallengeKind.Axis ? $"+{Rewards.XpAxisPass} XP y el sello del eje la primera vez que apruebas"
                : "Repite cuando quieras: prioriza lo que fallaste";
            rewards.Append(El.Text($"+{Rewards.XpPerCorrect} XP por acierto · racha de {Rewards.ComboStart}+ aciertos seguidos suma extra · {bonus}", "challenge-intro__line"));

            if (_challenge.Kind == ChallengeKind.Unit || _challenge.Kind == ChallengeKind.Axis)
            {
                var lessons = _challenge.Kind == ChallengeKind.Unit ? _challenge.Unit.Lessons : axis.Lessons.ToList();
                box.Append(El.Text("Incluye preguntas de: " + string.Join(" · ", lessons.Select(l => l.Title)), "challenge-intro__line", "muted"));
            }

            if (status == ChallengeStatus.Locked)
            {
                var note = box.Append(El.Div("callout", "callout--lock"));
                note.Append(new Icon(IconKind.Lock));
                note.Append(El.Text(S.Challenges.LockReason(_challenge), "callout__text"));
                return;
            }

            var timer = box.Append(new Toggle($"Con reloj: {ChallengeTestPage.SecondsPerQuestion} s por pregunta (+{Rewards.XpTimedBonus} XP por acierto)") { value = _timed });
            timer.AddToClassList("setting-toggle");
            timer.RegisterValueChangedCallback(e => _timed = e.newValue);
            var actions = box.Append(El.Row("result__actions"));
            actions.Append(El.IconBtn(IconKind.Play, attempts > 0 ? "Intentar de nuevo" : "¡Empezar!", () => Shell.Push(new ChallengeTestPage(_challenge, _timed)), "btn", "btn--big"));
        }

        private string Description()
        {
            switch (_challenge.Kind)
            {
                case ChallengeKind.Unit:
                    return $"El cierre de la unidad «{_challenge.Unit.Title}»: preguntas mezcladas de todas sus lecciones para comprobar que lo aprendido encaja.";
                case ChallengeKind.Axis:
                    return "El examen final del eje: preguntas de todas sus lecciones. Apruébalo para ganar el sello del eje.";
                default:
                    return "Un test rápido con lo que ya estudiaste en este eje: preguntas al azar (primero las que te cuestan) y un ejercicio de emparejar términos con su significado.";
            }
        }
    }

    /// <summary>Desafío en curso: ejercicio a ejercicio, con puntos de progreso, racha de aciertos, experiencia y reloj opcional.</summary>
    public class ChallengeTestPage : Page
    {
        public const int SecondsPerQuestion = 30;
        public const int SecondsPerMatch = 60;

        private readonly Challenge _challenge;
        private readonly bool _timed;
        private readonly int _seed;
        private readonly List<TestAnswer> _answers = new List<TestAnswer>();
        private List<TestItem> _items;
        private int _index;
        private int _combo;
        private int _bestCombo;
        private int _xpPreview;
        private IVisualElementScheduledItem _timer;

        internal QuestionView CurrentQuestion { get; private set; }
        internal MatchView CurrentMatch { get; private set; }
        internal TestItem CurrentItem => _items != null && _index < _items.Count ? _items[_index] : null;

        public ChallengeTestPage(Challenge challenge, bool timed, int? seed = null)
        {
            _challenge = challenge;
            _timed = timed;
            _seed = seed ?? Environment.TickCount;
        }

        public override string Title => _challenge.Kind == ChallengeKind.Practice ? "Mini test" : _challenge.Title;

        public override void Build()
        {
            if (_items == null)
                _items = S.Challenges.BuildTest(_challenge, new System.Random(_seed));
            if (Root.childCount == 0)
                ShowItem();
        }

        public override void OnClose() => _timer?.Pause();

        private void ShowItem()
        {
            Root.Clear();
            _timer?.Pause();
            CurrentQuestion = null;
            CurrentMatch = null;
            var color = El.Hex(_challenge.Axis.Color);
            var quiz = Root.Append(El.Div("quiz", "challenge-run"));
            if (_items.Count == 0)
            {
                quiz.Append(El.Text("No hay preguntas disponibles todavía. Estudia alguna lección del eje y vuelve.", "card__text"));
                return;
            }

            var eyebrow = quiz.Append(El.Row("challenge-run__eyebrow"));
            eyebrow.Append(new Icon(ChallengeUi.KindIcon(_challenge.Kind))).Ink(color);
            eyebrow.Append(El.Text(ChallengeUi.KindName(_challenge.Kind) + " · " + _challenge.Axis.Title, "quiz__lesson")).Ink(color);

            var top = quiz.Append(El.Row("quiz__top"));
            top.Append(El.Text($"Ejercicio {_index + 1} de {_items.Count}", "quiz__count"));
            top.Append(El.Spacer());
            var combo = top.Append(El.Row("combo-chip"));
            combo.Append(new Icon(IconKind.Flame));
            combo.Append(El.Text($"×{_combo}", "combo-chip__text"));
            combo.EnableInClassList("combo-chip--hot", _combo >= Rewards.ComboStart);
            combo.SetVisible(_combo >= 2);
            var xp = top.Append(El.Row("xp-chip"));
            xp.Append(new Icon(IconKind.Bolt));
            var xpLabel = xp.Append(El.Text($"+{_xpPreview} XP", "xp-chip__text"));

            var dots = quiz.Append(El.Row("run-dots"));
            for (var i = 0; i < _items.Count; i++)
            {
                var dot = dots.Append(El.Div("run-dot"));
                if (i < _answers.Count)
                    dot.AddToClassList(_answers[i].Correct ? "run-dot--right" : "run-dot--wrong");
                else if (i == _index)
                    dot.AddToClassList("run-dot--current");
            }

            VisualElement timerBar = null;
            if (_timed)
            {
                timerBar = quiz.Append(El.Div("timer"));
                timerBar.Append(El.Div("timer__fill")).style.backgroundColor = color;
            }

            var item = _items[_index];
            var holder = quiz.Append(El.Div("challenge-run__item")).Enter(60);
            Button next = null;
            void Answered(bool correct)
            {
                _timer?.Pause();
                _answers.Add(new TestAnswer { Item = item, Correct = correct });
                _combo = correct ? _combo + 1 : 0;
                _bestCombo = Math.Max(_bestCombo, _combo);
                var dot = dots[_index];
                dot.RemoveFromClassList("run-dot--current");
                dot.AddToClassList(correct ? "run-dot--right" : "run-dot--wrong");
                Anim.Pop(dot);
                if (correct)
                {
                    var gained = Rewards.XpPerCorrect + (_timed ? Rewards.XpTimedBonus : 0) + (_combo >= Rewards.ComboStart ? Rewards.XpComboStep : 0);
                    Anim.CountUp(xpLabel, _xpPreview, _xpPreview + gained, 500, v => $"+{v} XP");
                    _xpPreview += gained;
                    Anim.FloatUp(top, $"+{gained} XP", "float-text--xp");
                    Anim.Pop(xp);
                }
                combo.Q<Label>().text = $"×{_combo}";
                combo.SetVisible(_combo >= 2);
                combo.EnableInClassList("combo-chip--hot", _combo >= Rewards.ComboStart);
                if (_combo >= 2)
                    Anim.Pop(combo);
                if (_combo == Rewards.ComboStart || (_combo > Rewards.ComboStart && _combo % 2 == 1))
                    Anim.FloatUp(top, $"¡Racha ×{_combo}!", "float-text--combo");
                next.SetVisible(true);
                next.Enter();
            }

            if (item.IsMatch)
            {
                CurrentMatch = holder.Append(new MatchView(item.Pairs, Answered, new System.Random(_seed + _index)));
                var lesson = S.Curriculum.Lesson(item.LessonId);
                if (lesson != null)
                    holder.Insert(0, El.Text("Glosario de: " + lesson.Title, "muted"));
            }
            else
                CurrentQuestion = holder.Append(new QuestionView(item.Question, Answered, new System.Random(_seed + _index)));

            next = quiz.Append(El.Btn(_index == _items.Count - 1 ? "Ver resultado" : "Siguiente", Advance, "btn", "quiz__next"));
            next.SetVisible(false);

            if (timerBar != null)
                StartTimer(timerBar, item.IsMatch ? SecondsPerMatch : SecondsPerQuestion);
        }

        /// <summary>Pasa al siguiente ejercicio (o al resultado, tras el último) una vez respondido el actual.</summary>
        internal void Advance()
        {
            if (_answers.Count <= _index)
                return;
            if (_index >= _items.Count - 1)
                Finish();
            else
            {
                _index++;
                ShowItem();
            }
        }

        private void StartTimer(VisualElement bar, int seconds)
        {
            var fill = bar.Q(className: "timer__fill");
            var label = bar.Append(El.Text(seconds + " s", "timer__label"));
            long start = -1;
            var total = seconds * 1000f;
            _timer = bar.schedule.Execute(t =>
            {
                if (start < 0)
                    start = t.now;
                var left = Mathf.Max(0f, total - (t.now - start));
                fill.style.width = Length.Percent(left / total * 100f);
                label.text = Mathf.CeilToInt(left / 1000f) + " s";
                bar.EnableInClassList("timer--low", left < 8000f);
                if (left > 0f)
                    return;
                _timer?.Pause();
                CurrentQuestion?.TimeUp();
                CurrentMatch?.TimeUp();
            }).Every(100);
        }

        private void Finish()
        {
            _timer?.Pause();
            var result = S.Challenges.Record(_challenge, _answers, _bestCombo, _timed);
            Shell.Replace(new ChallengeResultPage(_challenge, result));
        }
    }

    /// <summary>Resultado de un desafío: nota, estrellas, experiencia, sello y qué lecciones repasar.</summary>
    public class ChallengeResultPage : Page
    {
        private readonly Challenge _challenge;
        private readonly QuizResult _result;

        public ChallengeResultPage(Challenge challenge, QuizResult result)
        {
            _challenge = challenge;
            _result = result;
        }

        public override string Title => "Resultado";

        public override void Build()
        {
            Root.Clear();
            var axis = _challenge.Axis;
            var practice = _challenge.Kind == ChallengeKind.Practice;
            var title = _result.BadgeEarned ? "¡Eje superado!"
                : _result.Passed ? (practice ? "¡Buen trabajo!" : "¡Desafío superado!")
                : "Casi: repasa y vuelve a intentarlo";
            var perfect = _result.Correct == _result.Total;
            var text = $"{_result.Correct} de {_result.Total} correctas. " + (_result.Passed
                ? practice && !perfect ? "Lo que fallaste vuelve hoy a tu repaso espaciado."
                : practice ? "Sin un solo fallo: este eje está firme."
                : "Las preguntas siguen en tu repaso espaciado para que no se olviden."
                : $"Necesitas {Mathf.RoundToInt(ProgressService.PassScore * 100)} % para superarlo. Repasa las lecciones de abajo y vuelve a intentarlo.");
            var box = ResultView.Build(Shell, Root, _result, El.Hex(axis.Color), title, text);
            if (_result.BadgeEarned)
            {
                var seal = box.Append(El.Div("result__seal"));
                seal.Add(ChallengeUi.Seal(axis, true, "seal--big"));
                seal.PopIn(1300);
                box.Append(El.Text($"Ganaste el sello de {axis.Title}", "pill", "pill--gold")).Enter(1500);
            }

            ResultView.WeakLessons(Shell, box, _result, S.Curriculum);

            var actions = box.Append(El.Row("result__actions"));
            actions.Append(El.Btn("Ver el mapa del eje", () =>
            {
                Leave();
                if (!(Shell.Current is AxisPage page && page.Axis == axis))
                    Shell.Push(new AxisPage(axis));
            }, "btn", "btn--ghost"));
            if (!_result.Passed || practice)
                actions.Append(El.Btn(practice ? "Otro mini test" : "Reintentar", () => Shell.Replace(new ChallengeTestPage(_challenge, false)), "btn"));
            else
            {
                var next = S.Challenges.NextChallenge();
                if (next != null && next.Id != _challenge.Id)
                    actions.Append(El.Btn("Siguiente desafío: " + next.Title, () =>
                    {
                        Leave();
                        Shell.OpenChallenge(next);
                    }, "btn"));
            }
        }

        /// <summary>Sale del resultado y de la portada del desafío.</summary>
        private void Leave()
        {
            Shell.Back();
            if (Shell.Current is ChallengeIntroPage)
                Shell.Back();
        }
    }

    /// <summary>Partes comunes de las pantallas de resultado (evaluaciones de lección y desafíos), animadas.</summary>
    public static class ResultView
    {
        public static VisualElement Build(AppShell shell, VisualElement root, QuizResult result, Color color, string title, string text)
        {
            var ringColor = result.Passed ? color : Theme.Gold;
            var box = root.Append(El.Div("result"));
            box.Append(new ProgressRing(result.Score, ringColor).AnimateTo(result.Score)).AddToClassList("result__ring");
            box.Append(El.Text(title, "result__title")).Enter(200);
            box.Append(ChallengeUi.Stars(result.Stars, 3, "stars--big", animate: true, delayMs: 700));
            if (result.Stars > result.PreviousStars && result.PreviousStars > 0)
                box.Append(El.Text($"¡Mejoraste de {result.PreviousStars} a {result.Stars} estrellas!", "pill", "pill--gold")).Enter(1200);
            box.Append(El.Text(text, "result__text")).Enter(350);

            var gains = box.Append(El.Row("result__gains")).Enter(500);
            var xp = gains.Append(El.Row("xp-chip", "xp-chip--big"));
            xp.Append(new Icon(IconKind.Bolt));
            Anim.CountUp(xp.Append(El.Text("+0 XP", "xp-chip__text")), 0, result.XpGained, 1000, v => $"+{v} XP", 600);
            if (result.BestCombo >= 2)
            {
                var combo = gains.Append(El.Row("combo-chip", result.BestCombo >= Rewards.ComboStart ? "combo-chip--hot" : null));
                combo.Append(new Icon(IconKind.Flame));
                combo.Append(El.Text($"Mejor racha ×{result.BestCombo}", "combo-chip__text"));
            }

            var level = shell.Services.Progress.Level;
            var progress = box.Append(El.Div("result__level")).Enter(700);
            progress.Append(El.Text($"Nivel {level.Level} · {level.Title}", "result__level-title"));
            progress.Append(El.Bar(level.Fraction, Theme.Gold, "result__level-bar")).Grow(level.Fraction, 900);
            progress.Append(El.Text($"{level.Xp} XP · faltan {Math.Max(0, level.Next - level.Xp)} para el nivel {level.Level + 1}", "muted"));
            if (result.LevelAfter > result.LevelBefore)
                box.Append(El.Text($"¡Subiste al nivel {result.LevelAfter}: {Rewards.Title(result.LevelAfter)}!", "pill", "pill--level")).PopIn(1600);
            return box;
        }

        /// <summary>Enlaces a las lecciones con fallos, para repasarlas.</summary>
        public static void WeakLessons(AppShell shell, VisualElement box, QuizResult result, Curriculum curriculum)
        {
            var lessons = result.WeakLessons.Select(curriculum.Lesson).Where(l => l != null).Take(4).ToList();
            if (lessons.Count == 0)
                return;
            var panel = box.Append(El.Div("weak")).Enter(900);
            panel.Append(El.Text("Para repasar", "weak__title"));
            foreach (var lesson in lessons)
            {
                var row = panel.Append(El.Btn(string.Empty, () => shell.OpenLesson(lesson), "weak__item"));
                row.Append(new Icon(IconKind.Book)).Ink(El.Hex(curriculum.AxisOf(lesson.Id).Color));
                row.Append(El.Text(lesson.Title, "weak__text")).pickingMode = PickingMode.Ignore;
                row.Append(new Icon(IconKind.Forward)).AddToClassList("weak__chevron");
            }
        }
    }
}
