using System;
using System.Globalization;
using System.Linq;
using EstudiApp.Domain;
using EstudiApp.UI.Diagrams;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Pages
{
    /// <summary>Inicio: progreso, continuar donde quedaste, repaso del día, diagrama destacado y la ruta.</summary>
    public class HomePage : Page
    {
        private static readonly string[] MainBooks = { "liping", "maciocia", "suwen", "focks" };

        public override string Title => "Inicio";

        public override void Build()
        {
            Root.Clear();
            var progress = S.Progress;
            var curriculum = S.Curriculum;

            var hero = Root.Append(El.Div("hero"));
            var heroText = hero.Append(El.Div("hero__text"));
            var date = DateTime.Now.ToString("dddd d 'de' MMMM", new CultureInfo("es-ES"));
            heroText.Append(El.Text(char.ToUpper(date[0]) + date.Substring(1), "hero__date"));
            heroText.Append(El.Text(Greeting(), "hero__title"));
            heroText.Append(El.Text("Cimientos firmes en Medicina Tradicional China: estudia en orden, comprende con diagramas y consolida con el repaso.", "hero__text-body"));
            if (!S.Entitlements.ShowsPlans)
                heroText.Append(El.Text("Versión de prueba · todo el contenido abierto", "pill", "pill--gold"));
            hero.Append(new ProgressRing(progress.OverallProgress(), Theme.Jade, "del temario")).AddToClassList("hero__ring");

            Root.Add(ChallengeUi.LevelCard(Shell).Enter());

            var challenges = S.Challenges;
            var stats = Root.Append(El.Div("stats"));
            var completed = curriculum.AllLessons.Count(l => progress.IsCompleted(l.Id));
            Stat(stats, IconKind.Flame, progress.Streak().ToString(), progress.Streak() == 1 ? "día seguido" : "días seguidos", Theme.Cinnabar);
            Stat(stats, IconKind.Check, $"{completed}/{curriculum.AllLessons.Count()}", "lecciones dominadas", Theme.Jade);
            Stat(stats, IconKind.StarFilled, challenges.TotalStars().ToString(), "estrellas ganadas", Theme.Gold);
            Stat(stats, IconKind.Review, progress.DueCount.ToString(), "tarjetas para hoy", Theme.Night);

            var grid = Root.Append(El.Div("home-grid"));
            var left = grid.Append(El.Div("home-grid__col"));
            var right = grid.Append(El.Div("home-grid__col"));

            var next = challenges.NextChallenge();
            if (next != null)
                left.Add(NextChallengeCard(next).Enter(80));
            else
            {
                var card = left.Append(El.Div("card"));
                card.Append(El.Text("¡Ruta completa!", "card__title"));
                card.Append(El.Text("Superaste todos los desafíos disponibles. Mantén el repaso diario y ponte a prueba con los mini tests.", "card__text"));
            }

            var practiceAxis = curriculum.AxisOf(progress.State.LastLessonId) ?? curriculum.Axes.FirstOrDefault(a => challenges.StudiedLessons(a).Count > 0);
            if (practiceAxis != null && challenges.CanStart(challenges.Practice(practiceAxis)))
            {
                var practice = challenges.Practice(practiceAxis);
                var color = El.Hex(practiceAxis.Color);
                var card = left.Append(El.Div("card", "card--action")).Enter(140);
                card.style.borderLeftColor = color;
                card.Append(El.Text("Mini test · 2 minutos", "card__eyebrow")).Ink(color);
                card.Append(El.Text("Pon a prueba: " + practiceAxis.Title, "card__title"));
                card.Append(El.Text($"{ChallengeService.PracticeSize} ejercicios al azar de lo que ya estudiaste, con racha de aciertos y emparejar términos.", "card__text"));
                var footer = card.Append(El.Row("card__footer"));
                if (challenges.Attempts(practice) > 0)
                    footer.Append(El.Text($"Tu mejor nota: {Mathf.RoundToInt(challenges.BestScore(practice) * 100)} %", "muted"));
                footer.Append(El.Spacer());
                footer.Append(El.IconBtn(IconKind.Target, "Ponerme a prueba", () => Shell.OpenChallenge(practice), "btn"));
            }

            var due = progress.DueCount;
            var review = left.Append(El.Div("card", "card--action"));
            review.Append(El.Text("Repaso espaciado", "card__eyebrow")).Ink(Theme.Gold);
            review.Append(El.Text(due > 0 ? $"Tienes {due} tarjeta{(due == 1 ? "" : "s")} para repasar hoy" : "Nada pendiente por hoy", "card__title"));
            review.Append(El.Text(due > 0
                ? "Las preguntas vuelven justo antes de olvidarlas: 1, 3, 7, 16 y 35 días."
                : "Las preguntas de cada evaluación vuelven aquí en los días siguientes para fijarlas en la memoria.", "card__text"));
            if (due > 0)
                review.Append(El.Row("card__footer")).Append(El.Btn("Repasar ahora", () => Shell.SwitchTab(Tab.Review), "btn", "btn--gold"));

            var seals = right.Append(El.Div("card", "seals-card")).Enter(120);
            seals.Append(El.Text("Tus sellos", "card__eyebrow")).Ink(Theme.Cinnabar);
            seals.Append(El.Text($"{challenges.Badges().Count()} de {curriculum.Axes.Count} ejes superados", "card__title"));
            var sealRow = seals.Append(El.Row("seals-card__row"));
            var sealIndex = 0;
            foreach (var axis in curriculum.Axes.OrderBy(a => a.Level))
            {
                var earned = challenges.HasBadge(axis);
                var item = sealRow.Append(El.Div("seals-card__item"));
                item.Add(ChallengeUi.Seal(axis, earned).PopIn(200 + sealIndex++ * 120));
                item.Append(El.Text(axis.Title, "seals-card__label"));
                item.Append(El.Text($"{challenges.AxisStars(axis)}/{challenges.MaxAxisStars(axis)} estrellas", "seals-card__meta"));
                item.RegisterCallback<ClickEvent>(_ => Shell.Open(Tab.Path, new AxisPage(axis)));
            }
            seals.Append(El.Text("Aprueba el examen final de cada eje para estampar su sello.", "card__text"));

            // Diagrama del día: cambia cada día entre los del temario.
            var diagrams = curriculum.Diagrams;
            if (diagrams.Count > 0)
            {
                var spec = diagrams[DateTime.Now.DayOfYear % diagrams.Count];
                var card = right.Append(El.Div("card", "card--action", "featured"));
                card.Append(El.Text("Diagrama del día", "card__eyebrow")).Ink(Theme.Jade);
                card.Append(El.Text(spec.Title, "card__title"));
                var canvas = card.Append(DiagramCanvas.Create(spec));
                canvas.AddToClassList("featured__canvas");
                canvas.MakeStatic();
                card.RegisterCallback<ClickEvent>(_ => Shell.Open(Tab.Diagrams, new DiagramPage(spec)));
            }

            var missing = MainBooks.Where(id => S.Library.Library.ForBibliography(id) == null && curriculum.Book(id) != null).ToList();
            if (missing.Count > 0)
            {
                var card = right.Append(El.Div("card"));
                card.Append(El.Text("Tu biblioteca", "card__eyebrow")).Ink(Theme.Night);
                card.Append(El.Text("Sube tus libros de referencia", "card__title"));
                card.Append(El.Text("Las lecciones citan página por página a " +
                                    string.Join(", ", missing.Select(id => Refs.Short(id, curriculum))) +
                                    ". Si subes tus PDF, cada cita se abre en la página exacta.", "card__text"));
                card.Append(El.Row("card__footer")).Append(El.IconBtn(IconKind.Upload, "Subir PDF", () => Shell.ImportPdf(), "btn"));
            }

            Root.Append(El.Text("Tu ruta", "section-title"));
            var path = Root.Append(El.Div("mini-path"));
            foreach (var axis in curriculum.Axes.OrderBy(a => a.Level))
                path.Add(PathPage.AxisCard(Shell, axis, compact: true));

            var credit = Root.Append(El.Btn(string.Empty, Core.AppConfig.OpenAuthorPage, "home-credit"));
            credit.tooltip = Core.AppConfig.AuthorUrl;
            credit.Append(El.Text($"{Core.AppConfig.AppName} · {Core.AppConfig.Credit}", "home-credit__text")).pickingMode = PickingMode.Ignore;
            credit.Append(El.Text(Core.AppConfig.AuthorUrl.Replace("https://", ""), "home-credit__url")).pickingMode = PickingMode.Ignore;
        }

        private VisualElement NextChallengeCard(Challenge next)
        {
            var axis = next.Axis;
            var color = El.Hex(axis.Color);
            var challenges = S.Challenges;
            var status = challenges.Status(next);
            var card = El.Div("card", "card--action", "continue");
            card.style.borderLeftColor = color;
            var head = card.Append(El.Row("continue__head"));
            var emblem = head.Append(El.Div("continue__emblem"));
            emblem.style.backgroundColor = color;
            emblem.Append(new Icon(next.Kind == ChallengeKind.Lesson ? IconKind.Play : ChallengeUi.KindIcon(next.Kind)));
            Anim.Pulse(emblem, "continue__emblem--beat", 1100);
            var headText = head.Append(El.Div("continue__head-text"));
            var eyebrow = next.Kind == ChallengeKind.Lesson
                ? (status == ChallengeStatus.InProgress ? "Continuar desafío" : "Siguiente desafío") + " · " + (next.Unit?.Title ?? axis.Title)
                : "Siguiente desafío · " + ChallengeUi.KindName(next.Kind);
            headText.Append(El.Text(eyebrow, "card__eyebrow")).Ink(color);
            headText.Append(El.Text(next.Kind == ChallengeKind.Lesson ? next.Lesson.Title : next.Title, "card__title"));

            Action open = () => Shell.OpenChallenge(next);
            if (next.Kind == ChallengeKind.Lesson)
            {
                var lesson = next.Lesson;
                if (!string.IsNullOrEmpty(lesson.Hanzi))
                    card.Append(El.Text($"{lesson.Hanzi}  ·  {lesson.Pinyin}", "hanzi-line"));
                card.Append(El.Text(lesson.Summary, "card__text"));
            }
            else
                card.Append(El.Text(next.Kind == ChallengeKind.Unit
                    ? $"Terminaste las lecciones de «{next.Unit.Title}». Demuestra que lo dominas con {next.Size} preguntas mezcladas y gana +{Rewards.XpUnitPass} XP."
                    : $"Llegaste al final de {axis.Title}. Aprueba el examen ({next.Size} preguntas) para ganar el sello del eje y +{Rewards.XpAxisPass} XP.", "card__text"));

            var row = card.Append(El.Row("card__footer"));
            row.Append(El.Text(next.Kind == ChallengeKind.Lesson ? $"{axis.Title} · {next.Lesson.Minutes} min · {next.Size} preguntas" : axis.Title, "muted"));
            row.Append(El.Spacer());
            row.Append(El.Btn(next.Kind == ChallengeKind.Lesson ? "Estudiar" : "Aceptar el desafío", open, "btn"));
            card.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button)
                    return;
                open();
            });
            return card;
        }

        private static string Greeting()
        {
            var hour = DateTime.Now.Hour;
            return hour < 6 ? "Buenas noches" : hour < 13 ? "Buenos días" : hour < 20 ? "Buenas tardes" : "Buenas noches";
        }

        private static void Stat(VisualElement parent, IconKind icon, string value, string label, Color color)
        {
            var stat = parent.Append(El.Div("stat"));
            stat.Append(new Icon(icon)).Ink(color);
            var text = stat.Append(El.Div("stat__text"));
            text.Append(El.Text(value, "stat__value"));
            text.Append(El.Text(label, "stat__label"));
        }
    }
}
