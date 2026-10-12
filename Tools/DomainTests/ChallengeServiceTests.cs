using System;
using System.Collections.Generic;
using System.Linq;
using EstudiApp.Domain;
using Xunit;

namespace EstudiApp.Tests
{
    public class ChallengeServiceTests
    {
        private static readonly DateTime Monday = new DateTime(2026, 10, 12, 10, 0, 0);

        private DateTime _now = Monday;

        private static Lesson Make(string id, int questions = 5, int terms = 0)
        {
            var lesson = new Lesson { Id = id, Title = id };
            for (var i = 1; i <= questions; i++)
                lesson.Quiz.Add(new Question { Id = $"{id}-q{i}", Options = { "a", "b" }, Answer = { 0 } });
            for (var i = 1; i <= terms; i++)
                lesson.Terms.Add(new Term { Hanzi = "字" + i, Pinyin = "zi" + i, Es = $"{id} término {i}" });
            return lesson;
        }

        /// <summary>
        /// "base": unidad "intro" de una lección (sin desafío) y unidad "taiji" de tres lecciones (con desafío).
        /// "canales" requiere "base" y tiene una unidad de dos lecciones.
        /// </summary>
        private static Curriculum Sample() => new Curriculum
        {
            Axes =
            {
                new Axis
                {
                    Id = "base", Title = "Base", Level = 1,
                    Units =
                    {
                        new Unit { Id = "intro", Title = "Intro", Lessons = { Make("b0", 5, 4) } },
                        new Unit { Id = "taiji", Title = "Taiji", Lessons = { Make("b1"), Make("b2"), Make("b3", 4) } }
                    }
                },
                new Axis
                {
                    Id = "canales", Title = "Canales", Level = 2, Requires = { "base" },
                    Units = { new Unit { Id = "sistema", Title = "Sistema", Lessons = { Make("c1"), Make("c2") } } }
                }
            }
        };

        private (Curriculum c, ProgressService p, ChallengeService ch) Setup(ProgressState state = null)
        {
            var c = Sample();
            var p = new ProgressService(c, state ?? new ProgressState(), new EntitlementPolicy(AccessMode.Open), () => _now);
            return (c, p, new ChallengeService(c, p));
        }

        private static Dictionary<string, bool> All(Lesson lesson, int wrong = 0) =>
            lesson.Quiz.Select((q, i) => (q, i)).ToDictionary(x => x.q.Id, x => x.i >= wrong);

        private static List<TestAnswer> Answer(List<TestItem> items, int wrong = 0) =>
            items.Select((item, i) => new TestAnswer { Item = item, Correct = i >= wrong }).ToList();

        private static void Pass(ProgressService p, Curriculum c, params string[] lessonIds)
        {
            foreach (var id in lessonIds)
                p.RecordQuiz(id, All(c.Lesson(id)));
        }

        [Theory]
        [InlineData(0.69f, 0)]
        [InlineData(0.7f, 1)]
        [InlineData(0.8f, 1)]
        [InlineData(0.85f, 2)]
        [InlineData(6f / 7f, 2)]
        [InlineData(0.99f, 2)]
        [InlineData(1f, 3)]
        public void StarsFollowTheScoreThresholds(float score, int stars)
        {
            Assert.Equal(stars, Rewards.Stars(score));
        }

        [Fact]
        public void LessonQuizGivesStarsAndExperienceOnlyForImprovements()
        {
            var (c, p, ch) = Setup();
            var first = p.RecordQuiz("b0", All(c.Lesson("b0"), wrong: 1));
            Assert.Equal(1, first.Stars);
            Assert.Equal(0, first.PreviousStars);
            Assert.Equal(4 * Rewards.XpPerCorrect + Rewards.XpLessonPass + Rewards.XpPerStar, first.XpGained);

            var second = p.RecordQuiz("b0", All(c.Lesson("b0")), bestCombo: 5);
            Assert.Equal(3, second.Stars);
            Assert.Equal(1, second.PreviousStars);
            Assert.False(second.FirstPass);
            Assert.Equal(5 * Rewards.XpPerCorrect + Rewards.ComboBonus(5) + 2 * Rewards.XpPerStar + Rewards.XpPerfect, second.XpGained);
            Assert.Equal(first.XpGained + second.XpGained, p.State.Xp);
            Assert.Equal(5, p.State.BestCombo);
            Assert.Equal(3, ch.Stars(ch.ForLesson(c.Lesson("b0"))));
        }

        [Fact]
        public void FailedLessonQuizHasNoStars()
        {
            var (c, p, ch) = Setup();
            var result = p.RecordQuiz("b0", All(c.Lesson("b0"), wrong: 2));
            Assert.Equal(0, result.Stars);
            Assert.Equal(ChallengeStatus.InProgress, ch.Status(ch.ForLesson(c.Lesson("b0"))));
            Assert.Equal(new[] { "b0" }, result.WeakLessons);
        }

        [Fact]
        public void ComboBonusStartsAtThreeInARow()
        {
            Assert.Equal(0, Rewards.ComboBonus(2));
            Assert.Equal(Rewards.XpComboStep, Rewards.ComboBonus(3));
            Assert.Equal(3 * Rewards.XpComboStep, Rewards.ComboBonus(5));
        }

        [Fact]
        public void LevelsGrowWithExperience()
        {
            Assert.Equal(1, Rewards.Level(0).Level);
            Assert.Equal(1, Rewards.Level(99).Level);
            Assert.Equal(2, Rewards.Level(100).Level);
            Assert.Equal(3, Rewards.Level(300).Level);
            var info = Rewards.Level(200);
            Assert.Equal(100, info.Floor);
            Assert.Equal(300, info.Next);
            Assert.Equal(0.5f, info.Fraction, 3);
            Assert.Equal("Semilla", Rewards.Title(1));
            Assert.Equal("Maestro 2", Rewards.Title(9));
        }

        [Fact]
        public void StagesListLessonsThenUnitChallengesThenTheAxisExam()
        {
            var (c, _, ch) = Setup();
            var stages = ch.Stages(c.Axis("base"));
            Assert.Equal(new[] { "lesson:b0", "lesson:b1", "lesson:b2", "lesson:b3", "unit:base/taiji", "axis:base" }, stages.Select(s => s.Id));
            Assert.Null(ch.ForUnit(c.Axis("base"), c.Axis("base").Units[0]));
            Assert.Equal(Math.Min(ChallengeService.UnitSize, 14), stages[4].Size);
            Assert.Equal(6 * 3, ch.MaxAxisStars(c.Axis("base")));
        }

        [Fact]
        public void UnitChallengeOpensWhenEveryLessonOfTheUnitIsMastered()
        {
            var (c, p, ch) = Setup();
            var axis = c.Axis("base");
            var boss = ch.ForUnit(axis, axis.Units[1]);
            Pass(p, c, "b0", "b1", "b2");
            Assert.Equal(ChallengeStatus.Locked, ch.Status(boss));
            Pass(p, c, "b3");
            Assert.Equal(ChallengeStatus.Available, ch.Status(boss));
            Assert.Equal("unit:base/taiji", ch.NextChallenge().Id);

            var items = ch.BuildTest(boss, new Random(1));
            Assert.Equal(ChallengeService.UnitSize, items.Count);
            Assert.All(new[] { "b1", "b2", "b3" }, id => Assert.True(items.Count(i => i.LessonId == id) >= 2));
            Assert.Equal(items.Count, items.Select(i => i.Key).Distinct().Count());

            var failed = ch.Record(boss, Answer(items, wrong: 4));
            Assert.False(failed.Passed);
            Assert.Equal(ChallengeStatus.InProgress, ch.Status(boss));
            var xpBefore = p.State.Xp;
            var passed = ch.Record(boss, Answer(items));
            Assert.True(passed.FirstPass);
            Assert.Equal(3, passed.Stars);
            Assert.Equal(ChallengeStatus.Passed, ch.Status(boss));
            Assert.Equal(8 * Rewards.XpPerCorrect + Rewards.XpUnitPass + 3 * Rewards.XpPerStar + Rewards.XpPerfect, passed.XpGained);
            Assert.Equal(xpBefore + passed.XpGained, p.State.Xp);
        }

        [Fact]
        public void AxisExamNeedsTheUnitChallengesAndAwardsTheBadge()
        {
            var (c, p, ch) = Setup();
            var axis = c.Axis("base");
            var exam = ch.ForAxis(axis);
            Pass(p, c, "b0", "b1", "b2", "b3");
            Assert.Equal(ChallengeStatus.Locked, ch.Status(exam));
            var boss = ch.ForUnit(axis, axis.Units[1]);
            ch.Record(boss, Answer(ch.BuildTest(boss, new Random(2))));
            Assert.Equal(ChallengeStatus.Available, ch.Status(exam));

            var items = ch.BuildTest(exam, new Random(3));
            Assert.Equal(ChallengeService.AxisSize, items.Count);
            Assert.All(axis.Lessons, l => Assert.Contains(items, i => i.LessonId == l.Id));
            var result = ch.Record(exam, Answer(items, wrong: 2), bestCombo: 10, timed: true);
            Assert.True(result.Passed);
            Assert.True(result.BadgeEarned);
            Assert.Equal(1, result.Stars);
            Assert.True(ch.HasBadge(axis));
            Assert.Equal(new[] { "base" }, ch.Badges().Select(a => a.Id));
            Assert.Equal(10 * (Rewards.XpPerCorrect + Rewards.XpTimedBonus) + Rewards.ComboBonus(10) + Rewards.XpAxisPass + Rewards.XpPerStar, result.XpGained);
            Assert.False(ch.Record(exam, Answer(items)).BadgeEarned);
        }

        [Fact]
        public void ChallengesDoNotChangeLessonUnlockRules()
        {
            var (c, p, ch) = Setup();
            Pass(p, c, "b0", "b1", "b2");
            Assert.True(p.IsAxisUnlocked(c.Axis("canales")));
            Assert.Equal(ChallengeStatus.Available, ch.Status(ch.ForLesson(c.Lesson("c1"))));
            Assert.Equal(ChallengeStatus.Locked, ch.Status(ch.ForAxis(c.Axis("canales"))));
        }

        [Fact]
        public void FreeModeOpensChallengesToo()
        {
            var (c, p, ch) = Setup();
            p.SetFreeMode(true);
            Assert.Equal(ChallengeStatus.Available, ch.Status(ch.ForAxis(c.Axis("canales"))));
            Assert.Equal(ChallengeStatus.Available, ch.Status(ch.ForUnit(c.Axis("base"), c.Axis("base").Units[1])));
        }

        [Fact]
        public void SelectionIsDeterministicForTheSameSeed()
        {
            var (c, p, ch) = Setup();
            p.MarkRead("b0");
            p.MarkRead("b1");
            p.MarkRead("b2");
            foreach (var challenge in new[] { ch.Practice(c.Axis("base")), ch.ForAxis(c.Axis("base")), ch.ForUnit(c.Axis("base"), c.Axis("base").Units[1]) })
            {
                var a = ch.BuildTest(challenge, new Random(42)).Select(i => i.Key + string.Join(",", i.Pairs?.Select(t => t.Es) ?? new string[0]));
                var b = ch.BuildTest(challenge, new Random(42)).Select(i => i.Key + string.Join(",", i.Pairs?.Select(t => t.Es) ?? new string[0]));
                Assert.Equal(a, b);
            }
            var orders = Enumerable.Range(0, 8)
                .Select(seed => string.Join("|", ch.BuildTest(ch.ForAxis(c.Axis("base")), new Random(seed)).Select(i => i.Key)))
                .Distinct();
            Assert.True(orders.Count() > 1);
        }

        [Fact]
        public void PracticeUsesOnlyStudiedLessonsAndAddsAMatchingExercise()
        {
            var (c, p, ch) = Setup();
            var practice = ch.Practice(c.Axis("base"));
            Assert.Equal(ChallengeStatus.Locked, ch.Status(practice));
            Assert.Empty(ch.BuildTest(practice, new Random(1)));

            p.MarkRead("b0");
            Assert.Equal(ChallengeStatus.Available, ch.Status(practice));
            var items = ch.BuildTest(practice, new Random(7));
            Assert.Equal(ChallengeService.PracticeSize, items.Count);
            Assert.All(items, i => Assert.Equal("b0", i.LessonId));
            var match = Assert.Single(items, i => i.IsMatch);
            Assert.Equal(ChallengeService.MatchPairs, match.Pairs.Count);
            Assert.Equal(4, items.Count(i => !i.IsMatch));
        }

        [Fact]
        public void PracticeWithoutGlossaryUsesOnlyQuestions()
        {
            var (c, p, ch) = Setup();
            p.MarkRead("b1");
            var items = ch.BuildTest(ch.Practice(c.Axis("base")), new Random(5));
            Assert.Equal(ChallengeService.PracticeSize, items.Count);
            Assert.DoesNotContain(items, i => i.IsMatch);
        }

        [Fact]
        public void PracticePrefersMissedQuestions()
        {
            var (c, p, ch) = Setup();
            p.MarkRead("b1");
            p.MarkRead("b2");
            p.RecordQuiz("b1", All(c.Lesson("b1")));
            p.RecordQuiz("b2", All(c.Lesson("b2"), wrong: 5));
            var fromMissed = Enumerable.Range(0, 40)
                .SelectMany(seed => ch.BuildTest(ch.Practice(c.Axis("base")), new Random(seed)))
                .Count(i => i.LessonId == "b2");
            Assert.True(fromMissed > 40 * ChallengeService.PracticeSize * 0.7, $"Sólo {fromMissed} de las falladas");
        }

        [Fact]
        public void ChallengeMissesSendQuestionsBackToReviewToday()
        {
            var (c, p, ch) = Setup();
            Pass(p, c, "b0", "b1", "b2", "b3");
            var card = p.State.Cards["b1-q1"];
            card.Box = 4;
            card.Due = Monday.AddDays(20);
            var boss = ch.ForUnit(c.Axis("base"), c.Axis("base").Units[1]);
            var items = ch.BuildTest(boss, new Random(9));
            var answers = items.Select(i => new TestAnswer { Item = i, Correct = i.Key != "b1-q1" }).ToList();
            if (!items.Any(i => i.Key == "b1-q1"))
                answers.Add(new TestAnswer { Item = new TestItem { LessonId = "b1", Question = c.Lesson("b1").Quiz[0] }, Correct = false });
            var result = ch.Record(boss, answers);
            Assert.Equal(1, card.Box);
            Assert.Equal(Monday.Date, card.Due);
            Assert.Contains(p.DueCards(), x => x.QuestionId == "b1-q1");
            Assert.Equal("b1", result.WeakLessons.First());
        }

        [Fact]
        public void ReviewHitsGiveExperience()
        {
            var (c, p, _) = Setup();
            p.RecordQuiz("b0", All(c.Lesson("b0"), wrong: 5));
            var xp = p.State.Xp;
            p.AnswerCard(p.State.Cards["b0-q1"], true);
            p.AnswerCard(p.State.Cards["b0-q2"], false);
            Assert.Equal(xp + Rewards.XpReview, p.State.Xp);
        }

        [Fact]
        public void ResetClearsChallengesAndExperience()
        {
            var (c, p, ch) = Setup();
            p.MarkRead("b0");
            ch.Record(ch.Practice(c.Axis("base")), Answer(ch.BuildTest(ch.Practice(c.Axis("base")), new Random(1))), 4);
            Assert.True(p.State.Xp > 0);
            p.Reset();
            Assert.Empty(p.State.Challenges);
            Assert.Equal(0, p.State.Xp);
            Assert.Equal(0, p.State.BestCombo);
        }

        [Fact]
        public void OldProgressFilesLoadAndGetExperienceForWhatWasMastered()
        {
            const string json = @"{
              ""Version"": 1,
              ""Lessons"": {
                ""b0"": { ""Read"": true, ""Completed"": true, ""BestScore"": 1.0, ""Attempts"": 2, ""LastStudied"": ""2026-10-01T10:00:00"" },
                ""b1"": { ""Read"": true, ""Completed"": true, ""BestScore"": 0.8, ""Attempts"": 1, ""LastStudied"": ""2026-10-02T10:00:00"" },
                ""b2"": { ""Read"": true, ""Completed"": false, ""BestScore"": 0.4, ""Attempts"": 1, ""LastStudied"": ""2026-10-03T10:00:00"" }
              },
              ""Cards"": {},
              ""Notes"": { ""b0"": ""nota"" },
              ""StudyDays"": [ ""2026-10-01"" ],
              ""LastLessonId"": ""b2"",
              ""FreeMode"": false
            }";
            var state = ContentParser.Read<ProgressState>(json);
            Assert.NotNull(state.Challenges);
            Assert.Equal(0, state.Xp);

            var (c, p, ch) = Setup(state);
            Assert.Equal(ProgressState.CurrentVersion, p.State.Version);
            Assert.Equal(2 * Rewards.XpLessonPass + 3 * Rewards.XpPerStar + 1 * Rewards.XpPerStar, p.State.Xp);
            Assert.Equal(3, ch.Stars(ch.ForLesson(c.Lesson("b0"))));
            Assert.Equal(ChallengeStatus.InProgress, ch.Status(ch.ForLesson(c.Lesson("b2"))));
            Assert.Equal("nota", p.Note("b0"));

            var reloaded = ContentParser.Read<ProgressState>(ContentParser.Write(p.State));
            var again = new ProgressService(c, reloaded, new EntitlementPolicy(AccessMode.Open), () => _now);
            Assert.Equal(p.State.Xp, again.State.Xp);
        }

        [Fact]
        public void NewProgressStartsAtTheCurrentVersionWithoutExperience()
        {
            var (_, p, _) = Setup();
            Assert.Equal(ProgressState.CurrentVersion, p.State.Version);
            Assert.Equal(0, p.State.Xp);
            Assert.Equal(1, p.Level.Level);
        }

        [Fact]
        public void RealCurriculumHasChallengesForEveryAxis()
        {
            var c = ContentParser.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "curriculum.json")));
            var p = new ProgressService(c, new ProgressState { FreeMode = true }, new EntitlementPolicy(AccessMode.Open), () => _now);
            var ch = new ChallengeService(c, p);
            foreach (var axis in c.Axes)
            {
                var stages = ch.Stages(axis);
                Assert.Contains(stages, s => s.Kind == ChallengeKind.Unit);
                Assert.Equal(ChallengeKind.Axis, stages.Last().Kind);
                foreach (var stage in stages.Where(s => s.Kind != ChallengeKind.Lesson))
                {
                    var items = ch.BuildTest(stage, new Random(1));
                    Assert.Equal(stage.Size, items.Count);
                    Assert.All(items, i => Assert.NotNull(c.Lesson(i.LessonId)));
                }
                foreach (var lesson in axis.Lessons)
                    p.MarkRead(lesson.Id);
                Assert.Equal(ChallengeService.PracticeSize, ch.BuildTest(ch.Practice(axis), new Random(1)).Count);
            }
        }
    }
}
