using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EstudiApp.Domain
{
    /// <summary>
    /// Ruta de aprendizaje con cimientos firmes: dentro de un eje las lecciones se abren en orden al aprobar la
    /// anterior, y un eje se abre cuando sus ejes previos están dominados en <see cref="AxisUnlockRatio"/>.
    /// </summary>
    public class ProgressService
    {
        /// <summary>Nota mínima de la evaluación para dar una lección por dominada.</summary>
        public const float PassScore = 0.7f;
        /// <summary>Parte de las lecciones de cada eje previo que hay que dominar para abrir el siguiente.</summary>
        public const float AxisUnlockRatio = 0.7f;
        /// <summary>Días hasta el próximo repaso según la caja Leitner (índice = caja).</summary>
        public static readonly int[] BoxIntervals = { 0, 1, 3, 7, 16, 35 };
        public const int MaxBox = 5;

        private readonly Curriculum _curriculum;
        private readonly Func<DateTime> _now;

        public ProgressState State { get; }
        public EntitlementPolicy Entitlements { get; }

        /// <summary>Se dispara cuando cambia algo que hay que guardar o redibujar.</summary>
        public event Action Changed;

        public ProgressService(Curriculum curriculum, ProgressState state, EntitlementPolicy entitlements, Func<DateTime> now = null)
        {
            _curriculum = curriculum;
            State = state ?? new ProgressState();
            Entitlements = entitlements ?? new EntitlementPolicy(AccessMode.Open);
            _now = now ?? (() => DateTime.Now);
            Migrate();
        }

        internal DateTime Now => _now();
        private DateTime Today => _now().Date;

        /// <summary>Progreso de la versión 1 (sin experiencia): se acredita lo que ya se había dominado.</summary>
        private void Migrate()
        {
            State.Lessons ??= new Dictionary<string, LessonProgress>();
            State.Cards ??= new Dictionary<string, ReviewCard>();
            State.Notes ??= new Dictionary<string, string>();
            State.StudyDays ??= new List<string>();
            State.Challenges ??= new Dictionary<string, ChallengeProgress>();
            if (State.Version >= ProgressState.CurrentVersion)
                return;
            foreach (var progress in State.Lessons.Values.Where(p => p != null && p.Completed))
                State.Xp += Rewards.XpLessonPass + Rewards.Stars(progress.BestScore) * Rewards.XpPerStar;
            State.Version = ProgressState.CurrentVersion;
        }

        public LevelInfo Level => Rewards.Level(State.Xp);

        internal void AddXp(int xp) => State.Xp = Math.Max(0, State.Xp + xp);

        internal void NotifyChanged() => Changed?.Invoke();

        // ---- Desbloqueo ---------------------------------------------------------------------------------------

        public bool IsAxisUnlocked(Axis axis)
        {
            if (axis == null)
                return false;
            if (State.FreeMode)
                return true;
            return axis.Requires.All(id => AxisProgress(_curriculum.Axis(id)) >= AxisUnlockRatio - 1e-4f);
        }

        /// <summary>Ejes previos que todavía faltan dominar para abrir este.</summary>
        public IEnumerable<Axis> MissingRequirements(Axis axis) =>
            axis.Requires.Select(_curriculum.Axis).Where(a => a != null && AxisProgress(a) < AxisUnlockRatio - 1e-4f);

        public LessonStatus Status(Lesson lesson)
        {
            var axis = _curriculum.AxisOf(lesson.Id);
            if (!Entitlements.CanStudy(axis))
                return LessonStatus.PremiumLocked;
            var progress = Progress(lesson.Id);
            if (progress != null && progress.Completed)
                return LessonStatus.Completed;
            if (!IsLessonUnlocked(lesson, axis))
                return LessonStatus.Locked;
            return progress != null && progress.Read ? LessonStatus.Read : LessonStatus.Available;
        }

        public bool CanOpen(Lesson lesson)
        {
            var status = Status(lesson);
            return status != LessonStatus.Locked && status != LessonStatus.PremiumLocked;
        }

        private bool IsLessonUnlocked(Lesson lesson, Axis axis)
        {
            if (State.FreeMode)
                return true;
            if (!IsAxisUnlocked(axis))
                return false;
            var lessons = axis.Lessons.ToList();
            var index = lessons.IndexOf(lesson);
            return index <= 0 || IsCompleted(lessons[index - 1].Id);
        }

        public LessonProgress Progress(string lessonId) =>
            lessonId != null && State.Lessons.TryGetValue(lessonId, out var progress) ? progress : null;

        public bool IsCompleted(string lessonId) => Progress(lessonId)?.Completed == true;

        public float AxisProgress(Axis axis)
        {
            if (axis == null)
                return 0f;
            var lessons = axis.Lessons.ToList();
            return lessons.Count == 0 ? 0f : lessons.Count(l => IsCompleted(l.Id)) / (float)lessons.Count;
        }

        public float OverallProgress()
        {
            var lessons = _curriculum.AllLessons.ToList();
            return lessons.Count == 0 ? 0f : lessons.Count(l => IsCompleted(l.Id)) / (float)lessons.Count;
        }

        /// <summary>La lección para "continuar": la última abierta sin dominar, o la primera disponible de la ruta.</summary>
        public Lesson NextLesson()
        {
            var last = _curriculum.Lesson(State.LastLessonId);
            if (last != null && !IsCompleted(last.Id) && CanOpen(last))
                return last;
            if (last != null)
            {
                var axis = _curriculum.AxisOf(last.Id);
                var next = axis.Lessons.FirstOrDefault(l => !IsCompleted(l.Id) && CanOpen(l));
                if (next != null)
                    return next;
            }
            return _curriculum.Axes.OrderBy(a => a.Level)
                .SelectMany(a => a.Lessons)
                .FirstOrDefault(l => !IsCompleted(l.Id) && CanOpen(l));
        }

        // ---- Estudio ------------------------------------------------------------------------------------------

        public void MarkOpened(string lessonId)
        {
            State.LastLessonId = lessonId;
            Touch(lessonId);
            RegisterStudyDay();
            Changed?.Invoke();
        }

        public void MarkRead(string lessonId)
        {
            var progress = Touch(lessonId);
            if (progress.Read)
                return;
            progress.Read = true;
            Changed?.Invoke();
        }

        /// <summary>
        /// Registra una evaluación. <paramref name="answers"/>: id de pregunta → acertada; <paramref name="bestCombo"/>:
        /// mejor racha de aciertos seguidos, que da experiencia extra.
        /// </summary>
        public QuizResult RecordQuiz(string lessonId, IDictionary<string, bool> answers, int bestCombo = 0)
        {
            var lesson = _curriculum.Lesson(lessonId);
            var lockedBefore = LockedSnapshot();
            var progress = Touch(lessonId);
            var result = new QuizResult
            {
                Total = answers.Count,
                Correct = answers.Count(a => a.Value),
                BestCombo = bestCombo,
                LevelBefore = Level.Level
            };
            result.Passed = result.Score >= PassScore - 1e-4f;
            result.FirstPass = result.Passed && !progress.Completed;
            result.PreviousStars = progress.Completed ? Rewards.Stars(progress.BestScore) : 0;
            progress.Attempts++;
            progress.Read = true;
            progress.BestScore = Math.Max(progress.BestScore, result.Score);
            if (result.Passed)
                progress.Completed = true;
            result.Stars = progress.Completed ? Rewards.Stars(progress.BestScore) : 0;
            if (!result.Passed)
                result.WeakLessons.Add(lessonId);

            if (lesson != null)
                foreach (var question in lesson.Quiz)
                    if (answers.TryGetValue(question.Id, out var ok))
                        FeedCard(lessonId, question.Id, ok, false);

            result.XpGained = result.Correct * Rewards.XpPerCorrect + Rewards.ComboBonus(bestCombo) +
                              (result.FirstPass ? Rewards.XpLessonPass : 0) +
                              Math.Max(0, result.Stars - result.PreviousStars) * Rewards.XpPerStar +
                              (result.Total > 0 && result.Correct == result.Total ? Rewards.XpPerfect : 0);
            AddXp(result.XpGained);
            State.BestCombo = Math.Max(State.BestCombo, bestCombo);
            result.LevelAfter = Level.Level;

            RegisterStudyDay();
            foreach (var l in _curriculum.AllLessons)
                if (lockedBefore.Contains(l.Id) && CanOpen(l))
                    result.UnlockedLessons.Add(l.Id);
            foreach (var axis in _curriculum.Axes)
                if (lockedBefore.Contains("axis:" + axis.Id) && IsAxisUnlocked(axis))
                    result.UnlockedAxes.Add(axis.Id);
            Changed?.Invoke();
            return result;
        }

        /// <summary>
        /// Lleva una pregunta respondida al repaso espaciado: si no tenía tarjeta se crea; con
        /// <paramref name="penalizeMiss"/>, un fallo devuelve la tarjeta existente a la primera caja para hoy.
        /// </summary>
        internal void FeedCard(string lessonId, string questionId, bool ok, bool penalizeMiss)
        {
            if (!State.Cards.TryGetValue(questionId, out var card))
            {
                State.Cards[questionId] = new ReviewCard
                {
                    QuestionId = questionId,
                    LessonId = lessonId,
                    Box = ok ? 2 : 1,
                    Due = Today.AddDays(ok ? BoxIntervals[2] : 0)
                };
                return;
            }
            if (penalizeMiss && !ok)
            {
                card.Box = 1;
                card.Lapses++;
                card.Due = Today;
            }
        }

        private HashSet<string> LockedSnapshot()
        {
            var locked = new HashSet<string>(_curriculum.AllLessons.Where(l => !CanOpen(l)).Select(l => l.Id));
            foreach (var axis in _curriculum.Axes.Where(a => !IsAxisUnlocked(a)))
                locked.Add("axis:" + axis.Id);
            return locked;
        }

        private LessonProgress Touch(string lessonId)
        {
            if (!State.Lessons.TryGetValue(lessonId, out var progress))
            {
                progress = new LessonProgress();
                State.Lessons[lessonId] = progress;
            }
            progress.LastStudied = _now();
            return progress;
        }

        // ---- Repaso espaciado ---------------------------------------------------------------------------------

        public List<ReviewCard> DueCards() =>
            State.Cards.Values.Where(c => c.Due <= Today && _curriculum.Lesson(c.LessonId) != null)
                .OrderBy(c => c.Box).ThenBy(c => c.Due).ToList();

        public int DueCount => DueCards().Count;

        public void AnswerCard(ReviewCard card, bool correct)
        {
            card.Reviews++;
            if (correct)
            {
                card.Box = Math.Min(MaxBox, card.Box + 1);
                AddXp(Rewards.XpReview);
            }
            else
            {
                card.Box = 1;
                card.Lapses++;
            }
            card.Due = Today.AddDays(correct ? BoxIntervals[card.Box] : 1);
            RegisterStudyDay();
            Changed?.Invoke();
        }

        public Question QuestionOf(ReviewCard card) =>
            _curriculum.Lesson(card.LessonId)?.Quiz.FirstOrDefault(q => q.Id == card.QuestionId);

        // ---- Constancia y notas -------------------------------------------------------------------------------

        internal void RegisterStudyDay()
        {
            var key = Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!State.StudyDays.Contains(key))
                State.StudyDays.Add(key);
        }

        /// <summary>Días seguidos con estudio hasta hoy (o hasta ayer, si hoy todavía no estudió).</summary>
        public int Streak()
        {
            var days = new HashSet<string>(State.StudyDays);
            var day = Today;
            if (!days.Contains(Key(day)))
                day = day.AddDays(-1);
            var streak = 0;
            while (days.Contains(Key(day)))
            {
                streak++;
                day = day.AddDays(-1);
            }
            return streak;
        }

        private static string Key(DateTime day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public string Note(string lessonId) => State.Notes.TryGetValue(lessonId, out var note) ? note : string.Empty;

        public void SetNote(string lessonId, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                State.Notes.Remove(lessonId);
            else
                State.Notes[lessonId] = text;
            Changed?.Invoke();
        }

        public void SetFreeMode(bool value)
        {
            State.FreeMode = value;
            Changed?.Invoke();
        }

        public void Reset()
        {
            State.Lessons.Clear();
            State.Cards.Clear();
            State.StudyDays.Clear();
            State.Challenges.Clear();
            State.Xp = 0;
            State.BestCombo = 0;
            State.LastLessonId = null;
            Changed?.Invoke();
        }
    }
}
