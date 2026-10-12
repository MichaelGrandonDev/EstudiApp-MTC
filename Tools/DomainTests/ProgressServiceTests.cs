using System;
using System.Collections.Generic;
using System.Linq;
using EstudiApp.Domain;
using Xunit;

namespace EstudiApp.Tests
{
    public class ProgressServiceTests
    {
        private static readonly DateTime Monday = new DateTime(2026, 10, 12, 10, 0, 0);

        private DateTime _now = Monday;

        /// <summary>Dos ejes: "base" (3 lecciones) y "clinica" (premium, requiere "base"), dos preguntas por lección.</summary>
        private static Curriculum Sample()
        {
            Lesson Make(string id) => new Lesson
            {
                Id = id,
                Title = id,
                Quiz =
                {
                    new Question { Id = id + "-q1", Options = { "a", "b" }, Answer = { 0 } },
                    new Question { Id = id + "-q2", Options = { "a", "b" }, Answer = { 1 } }
                }
            };
            return new Curriculum
            {
                Axes =
                {
                    new Axis { Id = "base", Level = 1, Units = { new Unit { Id = "u1", Lessons = { Make("b1"), Make("b2"), Make("b3") } } } },
                    new Axis { Id = "clinica", Level = 2, Premium = true, Requires = { "base" }, Units = { new Unit { Id = "u2", Lessons = { Make("c1"), Make("c2") } } } }
                }
            };
        }

        private ProgressService Service(Curriculum curriculum, AccessMode mode = AccessMode.Open, ProgressState state = null) =>
            new ProgressService(curriculum, state ?? new ProgressState(), new EntitlementPolicy(mode), () => _now);

        private static Dictionary<string, bool> Answers(string lessonId, bool first, bool second) =>
            new Dictionary<string, bool> { [lessonId + "-q1"] = first, [lessonId + "-q2"] = second };

        [Fact]
        public void OnlyTheFirstLessonOfTheFirstAxisStartsOpen()
        {
            var c = Sample();
            var p = Service(c);
            Assert.Equal(LessonStatus.Available, p.Status(c.Lesson("b1")));
            Assert.Equal(LessonStatus.Locked, p.Status(c.Lesson("b2")));
            Assert.False(p.IsAxisUnlocked(c.Axis("clinica")));
            Assert.Equal("b1", p.NextLesson().Id);
        }

        [Fact]
        public void PassingAQuizCompletesTheLessonAndOpensTheNextOne()
        {
            var c = Sample();
            var p = Service(c);
            var result = p.RecordQuiz("b1", Answers("b1", true, true));
            Assert.True(result.Passed);
            Assert.True(result.FirstPass);
            Assert.Equal(new[] { "b2" }, result.UnlockedLessons);
            Assert.Equal(LessonStatus.Completed, p.Status(c.Lesson("b1")));
            Assert.Equal(LessonStatus.Available, p.Status(c.Lesson("b2")));
        }

        [Fact]
        public void ScoreBelowSeventyPercentDoesNotPass()
        {
            var c = Sample();
            var p = Service(c);
            var result = p.RecordQuiz("b1", Answers("b1", true, false));
            Assert.Equal(0.5f, result.Score);
            Assert.False(result.Passed);
            Assert.Equal(LessonStatus.Read, p.Status(c.Lesson("b1")));
            Assert.Equal(LessonStatus.Locked, p.Status(c.Lesson("b2")));
        }

        [Fact]
        public void NextAxisOpensAtSeventyPercentOfItsRequirements()
        {
            var c = Sample();
            var p = Service(c);
            p.RecordQuiz("b1", Answers("b1", true, true));
            Assert.False(p.IsAxisUnlocked(c.Axis("clinica")));
            Assert.Equal(new[] { "base" }, p.MissingRequirements(c.Axis("clinica")).Select(a => a.Id));
            var result = p.RecordQuiz("b2", Answers("b2", true, true));
            Assert.Equal(2f / 3f, p.AxisProgress(c.Axis("base")), 3);
            Assert.False(p.IsAxisUnlocked(c.Axis("clinica")));
            Assert.Empty(result.UnlockedAxes);
            result = p.RecordQuiz("b3", Answers("b3", true, true));
            Assert.Contains("clinica", result.UnlockedAxes);
            Assert.Equal(LessonStatus.Available, p.Status(c.Lesson("c1")));
        }

        [Fact]
        public void FreeModeOpensEverything()
        {
            var c = Sample();
            var p = Service(c);
            p.SetFreeMode(true);
            Assert.True(c.AllLessons.All(p.CanOpen));
        }

        [Fact]
        public void PremiumAxesAreClosedOnlyInTheFreePlan()
        {
            var c = Sample();
            var state = new ProgressState { FreeMode = true };
            Assert.Equal(LessonStatus.PremiumLocked, Service(c, AccessMode.Free, state).Status(c.Lesson("c1")));
            Assert.True(Service(c, AccessMode.Premium, state).CanOpen(c.Lesson("c1")));
            Assert.True(Service(c, AccessMode.Open, state).CanOpen(c.Lesson("c1")));
        }

        [Fact]
        public void FreePlanLimitsImportedBooks()
        {
            var free = new EntitlementPolicy(AccessMode.Free);
            Assert.True(free.CanImportPdf(EntitlementPolicy.FreePdfLimit - 1));
            Assert.False(free.CanImportPdf(EntitlementPolicy.FreePdfLimit));
            Assert.True(new EntitlementPolicy(AccessMode.Open).CanImportPdf(100));
            Assert.False(new EntitlementPolicy(AccessMode.Open).ShowsPlans);
        }

        [Fact]
        public void QuizCreatesReviewCardsAndMissedOnesAreDueToday()
        {
            var c = Sample();
            var p = Service(c);
            p.RecordQuiz("b1", Answers("b1", true, false));
            Assert.Equal(2, p.State.Cards.Count);
            var due = p.DueCards();
            Assert.Single(due);
            Assert.Equal("b1-q2", due[0].QuestionId);
            Assert.Equal(Monday.Date.AddDays(ProgressService.BoxIntervals[2]), p.State.Cards["b1-q1"].Due);
        }

        [Fact]
        public void LeitnerBoxesGrowWithHitsAndResetOnMisses()
        {
            var c = Sample();
            var p = Service(c);
            p.RecordQuiz("b1", Answers("b1", true, false));
            var card = p.State.Cards["b1-q2"];
            p.AnswerCard(card, true);
            Assert.Equal(2, card.Box);
            Assert.Equal(Monday.Date.AddDays(3), card.Due);
            for (var i = 0; i < 10; i++)
                p.AnswerCard(card, true);
            Assert.Equal(ProgressService.MaxBox, card.Box);
            Assert.Equal(Monday.Date.AddDays(35), card.Due);
            p.AnswerCard(card, false);
            Assert.Equal(1, card.Box);
            Assert.Equal(1, card.Lapses);
            Assert.Equal(Monday.Date.AddDays(1), card.Due);
            Assert.Equal("b1-q2", p.QuestionOf(card).Id);
        }

        [Fact]
        public void StreakCountsConsecutiveStudyDays()
        {
            var c = Sample();
            var p = Service(c);
            p.MarkOpened("b1");
            _now = Monday.AddDays(1);
            p.MarkOpened("b1");
            _now = Monday.AddDays(2);
            Assert.Equal(2, p.Streak());
            p.MarkOpened("b1");
            Assert.Equal(3, p.Streak());
            _now = Monday.AddDays(5);
            Assert.Equal(0, p.Streak());
        }

        [Fact]
        public void ResetKeepsNotes()
        {
            var c = Sample();
            var p = Service(c);
            p.SetNote("b1", "Repasar el Taiji");
            p.RecordQuiz("b1", Answers("b1", true, true));
            p.Reset();
            Assert.Empty(p.State.Lessons);
            Assert.Empty(p.State.Cards);
            Assert.Equal("Repasar el Taiji", p.Note("b1"));
        }

        [Fact]
        public void QuestionsCompareAnswerSets()
        {
            var multi = new Question { Kind = "multi", Options = { "a", "b", "c" }, Answer = { 0, 2 } };
            Assert.True(multi.IsCorrect(new[] { 2, 0 }));
            Assert.False(multi.IsCorrect(new[] { 0 }));
            Assert.False(multi.IsCorrect(null));
        }
    }
}
