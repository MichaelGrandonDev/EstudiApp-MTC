using System;
using System.Collections.Generic;
using System.Linq;

namespace EstudiApp.Domain
{
    public enum ChallengeKind
    {
        /// <summary>La evaluación de una lección.</summary>
        Lesson,
        /// <summary>Desafío al final de una unidad: mezcla preguntas de sus lecciones.</summary>
        Unit,
        /// <summary>Examen final del eje: al aprobarlo se gana su sello.</summary>
        Axis,
        /// <summary>Mini test rápido con lo ya estudiado del eje; se puede repetir siempre.</summary>
        Practice
    }

    public enum ChallengeStatus
    {
        Locked,
        Available,
        InProgress,
        Passed
    }

    public class Challenge
    {
        public string Id;
        public ChallengeKind Kind;
        public Axis Axis;
        public Unit Unit;
        public Lesson Lesson;
        public string Title;
        /// <summary>Número de preguntas (o ejercicios) del desafío.</summary>
        public int Size;
    }

    /// <summary>Un ejercicio de un desafío: una pregunta del temario o emparejar términos del glosario de una lección.</summary>
    public class TestItem
    {
        public string LessonId;
        public Question Question;
        public List<Term> Pairs;

        public bool IsMatch => Pairs != null;
        public string Key => Question != null ? Question.Id : "match:" + LessonId;
    }

    public class TestAnswer
    {
        public TestItem Item;
        public bool Correct;
    }

    /// <summary>
    /// La ruta como progresión de desafíos: cada lección es un desafío con estrellas, cada unidad termina con un
    /// desafío que mezcla sus lecciones y cada eje con un examen que otorga su sello. Además, un mini test por eje
    /// para medirse con lo ya estudiado. No cambia las reglas de desbloqueo de <see cref="ProgressService"/>.
    /// </summary>
    public class ChallengeService
    {
        public const int UnitSize = 8;
        public const int AxisSize = 12;
        public const int PracticeSize = 5;
        public const int MatchPairs = 4;
        /// <summary>Las unidades de una sola lección no tienen desafío propio (sería repetir su evaluación).</summary>
        public const int MinUnitLessons = 2;

        private readonly Curriculum _curriculum;
        private readonly ProgressService _progress;

        public ChallengeService(Curriculum curriculum, ProgressService progress)
        {
            _curriculum = curriculum;
            _progress = progress;
        }

        private ProgressState State => _progress.State;

        // ---- Catálogo -----------------------------------------------------------------------------------------

        public static string UnitId(Axis axis, Unit unit) => $"unit:{axis.Id}/{unit.Id}";
        public static string AxisId(Axis axis) => "axis:" + axis.Id;
        public static string PracticeId(Axis axis) => "practice:" + axis.Id;

        public static bool HasBoss(Unit unit) => unit != null && unit.Lessons.Count >= MinUnitLessons;

        public Challenge ForLesson(Lesson lesson)
        {
            var axis = _curriculum.AxisOf(lesson.Id);
            return new Challenge
            {
                Id = "lesson:" + lesson.Id,
                Kind = ChallengeKind.Lesson,
                Axis = axis,
                Unit = axis?.Units.FirstOrDefault(u => u.Lessons.Contains(lesson)),
                Lesson = lesson,
                Title = lesson.Title,
                Size = lesson.Quiz.Count
            };
        }

        public Challenge ForUnit(Axis axis, Unit unit)
        {
            if (!HasBoss(unit))
                return null;
            return new Challenge
            {
                Id = UnitId(axis, unit),
                Kind = ChallengeKind.Unit,
                Axis = axis,
                Unit = unit,
                Title = "Desafío: " + unit.Title,
                Size = Math.Min(UnitSize, unit.Lessons.Sum(l => l.Quiz.Count))
            };
        }

        public Challenge ForAxis(Axis axis) => new Challenge
        {
            Id = AxisId(axis),
            Kind = ChallengeKind.Axis,
            Axis = axis,
            Title = "Examen: " + axis.Title,
            Size = Math.Min(AxisSize, axis.Lessons.Sum(l => l.Quiz.Count))
        };

        public Challenge Practice(Axis axis) => new Challenge
        {
            Id = PracticeId(axis),
            Kind = ChallengeKind.Practice,
            Axis = axis,
            Title = "Pon a prueba tu eje",
            Size = PracticeSize
        };

        /// <summary>Etapas del eje en orden: las lecciones de cada unidad seguidas de su desafío, y el examen al final.</summary>
        public List<Challenge> Stages(Axis axis)
        {
            var stages = new List<Challenge>();
            foreach (var unit in axis.Units)
            {
                stages.AddRange(unit.Lessons.Select(ForLesson));
                var boss = ForUnit(axis, unit);
                if (boss != null)
                    stages.Add(boss);
            }
            if (axis.Lessons.Any())
                stages.Add(ForAxis(axis));
            return stages;
        }

        // ---- Estado -------------------------------------------------------------------------------------------

        public ChallengeProgress Progress(string id) =>
            id != null && State.Challenges.TryGetValue(id, out var progress) ? progress : null;

        public float BestScore(Challenge challenge) => challenge.Kind == ChallengeKind.Lesson
            ? _progress.Progress(challenge.Lesson.Id)?.BestScore ?? 0f
            : Progress(challenge.Id)?.BestScore ?? 0f;

        public int Attempts(Challenge challenge) => challenge.Kind == ChallengeKind.Lesson
            ? _progress.Progress(challenge.Lesson.Id)?.Attempts ?? 0
            : Progress(challenge.Id)?.Attempts ?? 0;

        public bool IsPassed(Challenge challenge) => challenge.Kind == ChallengeKind.Lesson
            ? _progress.IsCompleted(challenge.Lesson.Id)
            : Progress(challenge.Id)?.Passed == true;

        public int Stars(Challenge challenge) => IsPassed(challenge) ? Rewards.Stars(BestScore(challenge)) : 0;

        public ChallengeStatus Status(Challenge challenge)
        {
            if (challenge.Kind == ChallengeKind.Lesson)
            {
                switch (_progress.Status(challenge.Lesson))
                {
                    case LessonStatus.Completed: return ChallengeStatus.Passed;
                    case LessonStatus.Read: return ChallengeStatus.InProgress;
                    case LessonStatus.Available: return ChallengeStatus.Available;
                    default: return ChallengeStatus.Locked;
                }
            }
            if (!_progress.Entitlements.CanStudy(challenge.Axis))
                return ChallengeStatus.Locked;
            if (IsPassed(challenge))
                return ChallengeStatus.Passed;
            if (!IsOpen(challenge))
                return ChallengeStatus.Locked;
            return Attempts(challenge) > 0 ? ChallengeStatus.InProgress : ChallengeStatus.Available;
        }

        public bool CanStart(Challenge challenge) => Status(challenge) != ChallengeStatus.Locked;

        private bool IsOpen(Challenge challenge)
        {
            var axis = challenge.Axis;
            if (challenge.Kind == ChallengeKind.Practice)
                return _progress.IsAxisUnlocked(axis) && StudiedLessons(axis).Count > 0;
            if (!_progress.IsAxisUnlocked(axis))
                return false;
            if (State.FreeMode)
                return true;
            if (challenge.Kind == ChallengeKind.Unit)
                return challenge.Unit.Lessons.All(l => _progress.IsCompleted(l.Id));
            return axis.Lessons.All(l => _progress.IsCompleted(l.Id)) &&
                   axis.Units.Where(HasBoss).All(u => IsPassed(ForUnit(axis, u)));
        }

        /// <summary>Qué falta para abrir un desafío de unidad o el examen del eje (texto para el estudiante).</summary>
        public string LockReason(Challenge challenge)
        {
            switch (challenge.Kind)
            {
                case ChallengeKind.Unit:
                    return "Domina todas las lecciones de la unidad para enfrentarte a su desafío.";
                case ChallengeKind.Axis:
                    return "Domina todas las lecciones del eje y supera los desafíos de cada unidad para presentarte al examen.";
                case ChallengeKind.Practice:
                    return "Estudia al menos una lección del eje para poder ponerte a prueba.";
                default:
                    return "Aprueba la evaluación de la lección anterior para abrir esta.";
            }
        }

        /// <summary>Lecciones del eje ya estudiadas: leídas o con alguna evaluación hecha.</summary>
        public List<Lesson> StudiedLessons(Axis axis) => axis.Lessons.Where(l =>
        {
            var progress = _progress.Progress(l.Id);
            return progress != null && (progress.Read || progress.Completed || progress.Attempts > 0);
        }).ToList();

        public bool HasBadge(Axis axis) => Progress(AxisId(axis))?.Passed == true;

        public IEnumerable<Axis> Badges() => _curriculum.Axes.Where(HasBadge);

        public int AxisStars(Axis axis) => Stages(axis).Sum(Stars);

        public int MaxAxisStars(Axis axis) => Stages(axis).Count * 3;

        public int TotalStars() => _curriculum.Axes.Sum(AxisStars);

        public int MaxStars() => _curriculum.Axes.Sum(MaxAxisStars);

        /// <summary>
        /// El próximo desafío de la ruta: la primera etapa abierta sin superar, empezando por el eje de la última lección
        /// abierta y siguiendo por niveles (así el desafío de una unidad aparece antes que la unidad o el eje siguientes).
        /// </summary>
        public Challenge NextChallenge()
        {
            var axes = _curriculum.Axes.OrderBy(a => a.Level).ToList();
            var current = _curriculum.AxisOf(State.LastLessonId);
            if (current != null)
            {
                axes.Remove(current);
                axes.Insert(0, current);
            }
            foreach (var axis in axes)
                foreach (var stage in Stages(axis))
                {
                    var status = Status(stage);
                    if (status == ChallengeStatus.Available || status == ChallengeStatus.InProgress)
                        return stage;
                }
            return null;
        }

        // ---- Preguntas ----------------------------------------------------------------------------------------

        /// <summary>
        /// Ejercicios del desafío, elegidos con <paramref name="random"/> (con la misma semilla, el mismo test).
        /// Unidad y eje: reparto equitativo entre sus lecciones. Mini test: lo estudiado, con prioridad a las preguntas
        /// falladas o nuevas, y un ejercicio de emparejar términos si el glosario lo permite.
        /// </summary>
        public List<TestItem> BuildTest(Challenge challenge, Random random)
        {
            switch (challenge.Kind)
            {
                case ChallengeKind.Lesson:
                    return challenge.Lesson.Quiz.Select(q => new TestItem { LessonId = challenge.Lesson.Id, Question = q }).ToList();
                case ChallengeKind.Unit:
                    return Mix(challenge.Unit.Lessons, UnitSize, random);
                case ChallengeKind.Axis:
                    return Mix(challenge.Axis.Lessons, AxisSize, random);
                default:
                    return PracticeItems(challenge.Axis, random);
            }
        }

        private static List<TestItem> Mix(IEnumerable<Lesson> lessons, int size, Random random)
        {
            var queues = Shuffle(lessons.Where(l => l.Quiz.Count > 0)
                    .Select(l => new Queue<TestItem>(Shuffle(l.Quiz.Select(q => new TestItem { LessonId = l.Id, Question = q }).ToList(), random)))
                    .ToList(), random);
            var items = new List<TestItem>();
            while (items.Count < size && queues.Any(q => q.Count > 0))
                foreach (var queue in queues)
                {
                    if (items.Count >= size)
                        break;
                    if (queue.Count > 0)
                        items.Add(queue.Dequeue());
                }
            return Shuffle(items, random);
        }

        private List<TestItem> PracticeItems(Axis axis, Random random)
        {
            var lessons = StudiedLessons(axis);
            if (lessons.Count == 0)
                return new List<TestItem>();
            var match = MatchItem(lessons, random);
            var weighted = lessons
                .SelectMany(l => l.Quiz.Select(q => new TestItem { LessonId = l.Id, Question = q }))
                .Select(item => (item, weight: random.NextDouble() + Weakness(item.Question.Id)))
                .ToList();
            var items = weighted.OrderByDescending(x => x.weight)
                .Take(PracticeSize - (match != null ? 1 : 0))
                .Select(x => x.item)
                .ToList();
            if (match != null)
                items.Insert(random.Next(items.Count + 1), match);
            return items;
        }

        /// <summary>Peso extra para lo que más conviene repasar: preguntas falladas o todavía sin tarjeta.</summary>
        private double Weakness(string questionId)
        {
            if (!State.Cards.TryGetValue(questionId, out var card))
                return 0.3;
            return card.Box <= 1 ? 0.8 : card.Box == 2 ? 0.4 : 0.0;
        }

        /// <summary>Emparejar hanzi/pinyin con su significado, con términos del glosario de una lección estudiada.</summary>
        private static TestItem MatchItem(List<Lesson> lessons, Random random)
        {
            var candidates = lessons
                .Select(l => (lesson: l, terms: UsableTerms(l)))
                .Where(x => x.terms.Count >= MatchPairs)
                .ToList();
            if (candidates.Count == 0)
                return null;
            var (lesson, terms) = candidates[random.Next(candidates.Count)];
            return new TestItem { LessonId = lesson.Id, Pairs = Shuffle(terms, random).Take(MatchPairs).ToList() };
        }

        private static List<Term> UsableTerms(Lesson lesson) => lesson.Terms
            .Where(t => (!string.IsNullOrWhiteSpace(t.Hanzi) || !string.IsNullOrWhiteSpace(t.Pinyin)) && !string.IsNullOrWhiteSpace(t.Es))
            .GroupBy(t => t.Es.Trim().ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

        private static List<T> Shuffle<T>(List<T> list, Random random)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        // ---- Resultado ----------------------------------------------------------------------------------------

        /// <summary>
        /// Registra un desafío: mejor nota, estrellas, experiencia y sello del eje. Las preguntas pasan al repaso
        /// espaciado (las falladas vuelven hoy). Las evaluaciones de lección se delegan en <see cref="ProgressService.RecordQuiz"/>.
        /// </summary>
        public QuizResult Record(Challenge challenge, IList<TestAnswer> answers, int bestCombo = 0, bool timed = false)
        {
            if (challenge.Kind == ChallengeKind.Lesson)
                return _progress.RecordQuiz(challenge.Lesson.Id,
                    answers.Where(a => a.Item.Question != null).ToDictionary(a => a.Item.Question.Id, a => a.Correct), bestCombo);

            var result = new QuizResult
            {
                Total = answers.Count,
                Correct = answers.Count(a => a.Correct),
                BestCombo = bestCombo,
                LevelBefore = _progress.Level.Level,
                PreviousStars = Stars(challenge)
            };
            if (!State.Challenges.TryGetValue(challenge.Id, out var progress))
            {
                progress = new ChallengeProgress();
                State.Challenges[challenge.Id] = progress;
            }
            var wasPassed = progress.Passed;
            result.Passed = result.Score >= ProgressService.PassScore - 1e-4f;
            result.FirstPass = result.Passed && !wasPassed;
            progress.Attempts++;
            progress.BestScore = Math.Max(progress.BestScore, result.Score);
            progress.Passed |= result.Passed;
            progress.LastPlayed = _progress.Now;
            result.Stars = Stars(challenge);
            result.BadgeEarned = challenge.Kind == ChallengeKind.Axis && result.FirstPass;
            result.WeakLessons = answers.Where(a => !a.Correct)
                .GroupBy(a => a.Item.LessonId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .ToList();

            foreach (var answer in answers.Where(a => a.Item.Question != null))
                _progress.FeedCard(answer.Item.LessonId, answer.Item.Question.Id, answer.Correct, true);

            var firstPassBonus = !result.FirstPass ? 0
                : challenge.Kind == ChallengeKind.Unit ? Rewards.XpUnitPass
                : challenge.Kind == ChallengeKind.Axis ? Rewards.XpAxisPass
                : 0;
            result.XpGained = result.Correct * (Rewards.XpPerCorrect + (timed ? Rewards.XpTimedBonus : 0)) +
                              Rewards.ComboBonus(bestCombo) + firstPassBonus +
                              Math.Max(0, result.Stars - result.PreviousStars) * Rewards.XpPerStar +
                              (result.Total > 0 && result.Correct == result.Total ? Rewards.XpPerfect : 0);
            _progress.AddXp(result.XpGained);
            State.BestCombo = Math.Max(State.BestCombo, bestCombo);
            result.LevelAfter = _progress.Level.Level;
            _progress.RegisterStudyDay();
            _progress.NotifyChanged();
            return result;
        }
    }
}
