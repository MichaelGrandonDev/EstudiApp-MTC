using System.Linq;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Pages
{
    /// <summary>
    /// Un eje temático como mapa de desafíos: un camino con un nodo por lección, el desafío de cada unidad al final de
    /// su tramo y el examen del eje (que da su sello) como meta. Arriba, el dominio del eje y el mini test.
    /// </summary>
    public class AxisPage : Page
    {
        private readonly Axis _axis;

        public AxisPage(Axis axis)
        {
            _axis = axis;
        }

        public Axis Axis => _axis;

        public override string Title => _axis.Title;

        public override void Build()
        {
            Root.Clear();
            var progress = S.Progress;
            var challenges = S.Challenges;
            var color = El.Hex(_axis.Color);
            var lessons = _axis.Lessons.ToList();

            var hero = Root.Append(El.Div("axis-hero"));
            hero.style.backgroundColor = color;
            hero.Append(El.Text(_axis.Hanzi, "axis-hero__hanzi", "hanzi"));
            var text = hero.Append(El.Div("axis-hero__text"));
            text.Append(El.Text(_axis.Pinyin, "axis-hero__pinyin"));
            text.Append(El.Text(_axis.Title, "axis-hero__title"));
            if (!string.IsNullOrEmpty(_axis.Subtitle))
                text.Append(El.Text(_axis.Subtitle, "axis-hero__subtitle"));
            var facts = text.Append(El.Row("axis-hero__facts"));
            var stars = facts.Append(El.Row("axis-hero__fact"));
            stars.Append(new Icon(IconKind.StarFilled));
            stars.Append(El.Text($"{challenges.AxisStars(_axis)} / {challenges.MaxAxisStars(_axis)}", "axis-hero__fact-text"));
            var done = facts.Append(El.Row("axis-hero__fact"));
            done.Append(new Icon(IconKind.Check));
            done.Append(El.Text($"{lessons.Count(l => progress.IsCompleted(l.Id))} / {lessons.Count} lecciones", "axis-hero__fact-text"));
            var badge = challenges.HasBadge(_axis);
            if (badge)
                hero.Append(ChallengeUi.Seal(_axis, true, "seal--hero")).PopIn(400);
            hero.Append(new ProgressRing(progress.AxisProgress(_axis), Color.white).AnimateTo(progress.AxisProgress(_axis))).AddToClassList("axis-hero__ring");

            Root.Append(El.Text(_axis.Description, "page-lead"));

            if (!progress.IsAxisUnlocked(_axis))
            {
                var missing = progress.MissingRequirements(_axis).ToList();
                var note = Root.Append(El.Div("callout", "callout--lock"));
                note.Append(new Icon(IconKind.Lock));
                note.Append(El.Text("Para abrir este eje, domina al menos el 70 % de: " + string.Join(", ", missing.Select(a => a.Title)) +
                                    ". Si ya tienes esa base, activa el modo libre en Ajustes.", "callout__text"));
            }

            Root.Add(PracticeCard(color));

            Root.Append(El.Text("Mapa de desafíos", "section-title"));
            Root.Append(El.Text("Supera cada lección: 1 estrella con 70 %, 2 con 85 % y 3 sin fallos. Al final de cada tramo te espera el desafío de la unidad, y al final del camino, el examen que te da el sello del eje.", "page-lead"));
            var next = challenges.NextChallenge();
            var map = Root.Append(new ChallengeMap(color));
            var step = 0;
            var lessonNumber = 0;
            foreach (var unit in _axis.Units)
            {
                var unitDone = unit.Lessons.Count(l => progress.IsCompleted(l.Id));
                var head = map.Append(El.Row("cmap__unit")).Enter(step * 35);
                head.Append(El.Text(unit.Title, "cmap__unit-title"));
                head.Append(El.Spacer());
                head.Append(El.Text($"{unitDone}/{unit.Lessons.Count}", "cmap__unit-meta"));
                foreach (var lesson in unit.Lessons)
                {
                    lessonNumber++;
                    map.Add(NodeRow(map, challenges.ForLesson(lesson), step++, lessonNumber.ToString(), color, next));
                }
                var boss = challenges.ForUnit(_axis, unit);
                if (boss != null)
                    map.Add(NodeRow(map, boss, step++, null, color, next));
            }
            if (lessons.Count > 0)
                map.Add(NodeRow(map, challenges.ForAxis(_axis), step, null, color, next));
        }

        private VisualElement PracticeCard(Color color)
        {
            var challenges = S.Challenges;
            var practice = challenges.Practice(_axis);
            var status = challenges.Status(practice);
            var card = El.Div("card", "card--action", "practice-card");
            card.style.borderLeftColor = color;
            var row = card.Append(El.Row("practice-card__row"));
            var emblem = row.Append(El.Div("practice-card__emblem"));
            emblem.style.backgroundColor = color;
            emblem.Append(new Icon(IconKind.Target));
            var body = row.Append(El.Div("practice-card__body"));
            body.Append(El.Text("Mini test · " + ChallengeService.PracticeSize + " ejercicios", "card__eyebrow")).Ink(color);
            body.Append(El.Text("Pon a prueba tu eje", "card__title"));
            var studied = challenges.StudiedLessons(_axis).Count;
            body.Append(El.Text(status == ChallengeStatus.Locked
                ? challenges.LockReason(practice)
                : $"Preguntas al azar de las {studied} lecciones que ya estudiaste (primero las que te cuestan) y emparejar términos del glosario.", "card__text"));
            var side = row.Append(El.Div("practice-card__side"));
            var attempts = challenges.Attempts(practice);
            if (attempts > 0)
            {
                side.Append(ChallengeUi.Stars(challenges.Stars(practice)));
                side.Append(El.Text($"Mejor {Mathf.RoundToInt(challenges.BestScore(practice) * 100)} % · {attempts} intento{(attempts == 1 ? "" : "s")}", "muted"));
            }
            var button = side.Append(El.IconBtn(IconKind.Play, "Empezar", () => Shell.OpenChallenge(practice), "btn"));
            button.SetEnabled(status != ChallengeStatus.Locked);
            return card;
        }

        private VisualElement NodeRow(ChallengeMap map, Challenge challenge, int step, string number, Color color, Challenge next)
        {
            var challenges = S.Challenges;
            var status = challenges.Status(challenge);
            var kind = challenge.Kind;
            var isNext = next != null && next.Id == challenge.Id;
            var row = El.Row("cmap__row", "cmap__row--" + kind.ToString().ToLowerInvariant()).Enter(step * 35);

            // Zigzag suave: el camino serpentea de un lado a otro.
            var swing = (Mathf.Sin(step * 0.95f) + 1f) / 2f;
            var lead = row.Append(El.Div("cmap__lead"));
            lead.style.width = Length.Percent(kind == ChallengeKind.Axis ? 24f : (Shell.Narrow ? 4f : 6f) + swing * (Shell.Narrow ? 22f : 34f));

            var node = row.Append(El.Div("cnode", "cnode--" + status.ToString().ToLowerInvariant(),
                kind == ChallengeKind.Unit ? "cnode--boss" : kind == ChallengeKind.Axis ? "cnode--exam" : null));
            if (isNext)
            {
                var halo = node.Append(El.Div("cnode__halo"));
                halo.style.borderTopColor = halo.style.borderBottomColor = halo.style.borderLeftColor = halo.style.borderRightColor = color;
                Anim.Pulse(halo, "cnode__halo--big");
            }
            var face = node.Append(El.Div("cnode__face"));
            switch (status)
            {
                case ChallengeStatus.Passed:
                    face.style.backgroundColor = kind == ChallengeKind.Axis ? Theme.Cinnabar : color;
                    face.Append(new Icon(kind == ChallengeKind.Lesson ? IconKind.Check : ChallengeUi.KindIcon(kind)));
                    break;
                case ChallengeStatus.Locked:
                    face.Append(new Icon(kind == ChallengeKind.Lesson ? IconKind.Lock : ChallengeUi.KindIcon(kind)));
                    break;
                default:
                    face.style.borderTopColor = face.style.borderBottomColor = face.style.borderLeftColor = face.style.borderRightColor = color;
                    if (status == ChallengeStatus.InProgress)
                        face.style.backgroundColor = color.Alpha(0.16f);
                    if (number != null)
                        face.Append(El.Text(number, "cnode__number")).Ink(color);
                    else
                        face.Append(new Icon(ChallengeUi.KindIcon(kind))).Ink(color);
                    break;
            }
            map.Track(node, status != ChallengeStatus.Locked);

            var label = row.Append(El.Div("cmap__label"));
            var titleRow = label.Append(El.Row("cmap__title-row"));
            var title = kind == ChallengeKind.Lesson ? challenge.Lesson.Title
                : kind == ChallengeKind.Unit ? "Desafío de la unidad"
                : "Examen del eje";
            titleRow.Append(El.Text(title, "cmap__title"));
            if (isNext)
                titleRow.Append(El.Text(status == ChallengeStatus.InProgress ? "En curso" : "¡Siguiente!", "pill", "pill--jade", "cmap__next"));
            label.Append(El.Text(Meta(challenge, status), "cmap__meta"));
            if (status == ChallengeStatus.Passed || kind != ChallengeKind.Lesson)
                label.Append(ChallengeUi.Stars(challenges.Stars(challenge), 3, "stars--small"));
            if (kind == ChallengeKind.Lesson && !string.IsNullOrEmpty(challenge.Lesson.Hanzi))
                row.Append(El.Text(challenge.Lesson.Hanzi, "cmap__hanzi", "hanzi"));

            row.RegisterCallback<ClickEvent>(_ => Shell.OpenChallenge(challenge));
            return row;
        }

        private string Meta(Challenge challenge, ChallengeStatus status)
        {
            var challenges = S.Challenges;
            var attempts = challenges.Attempts(challenge);
            var best = attempts > 0 ? $" · mejor {Mathf.RoundToInt(challenges.BestScore(challenge) * 100)} %" : string.Empty;
            switch (challenge.Kind)
            {
                case ChallengeKind.Lesson:
                    var lesson = challenge.Lesson;
                    var state = status == ChallengeStatus.Passed ? "Superada" : status == ChallengeStatus.InProgress ? "En curso" : status == ChallengeStatus.Locked ? "Bloqueada" : "Disponible";
                    return $"{state} · {lesson.Minutes} min · {lesson.Quiz.Count} preguntas{best}";
                case ChallengeKind.Unit:
                    return status == ChallengeStatus.Locked
                        ? $"Domina las {challenge.Unit.Lessons.Count} lecciones del tramo para enfrentarlo"
                        : $"{challenge.Size} preguntas mezcladas de {challenge.Unit.Lessons.Count} lecciones{best}";
                default:
                    return status == ChallengeStatus.Passed
                        ? $"Sello de {challenge.Axis.Title} obtenido{best}"
                        : status == ChallengeStatus.Locked
                            ? "Supera todas las lecciones y los desafíos de unidad para presentarte"
                            : $"{challenge.Size} preguntas de todo el eje · gana el sello{best}";
            }
        }
    }
}
