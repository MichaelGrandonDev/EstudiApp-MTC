using System.Linq;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Pages
{
    /// <summary>Temario: los ejes temáticos en orden de niveles, como una ruta de aprendizaje.</summary>
    public class PathPage : Page
    {
        private static readonly string[] LevelNames = { "", "Nivel 1 · Cimientos", "Nivel 2 · Fisiología y canales", "Nivel 3 · Diagnóstico y clínica", "Nivel 4 · Terapéutica y cultivo" };

        /// <summary>Ejes previstos que todavía no están en el temario.</summary>
        private static readonly (string id, string title, string hanzi, int level)[] Upcoming =
        {
            ("etiologia", "Etiología: causas de enfermedad", "病因", 2),
            ("diagnostico", "Diagnóstico: los cuatro métodos", "四诊", 3),
            ("sindromes", "Síndromes de los Zang Fu", "辨证", 3),
            ("tecnicas", "Técnicas: acupuntura, moxa y ventosas", "针灸", 4),
            ("qigong", "Qigong y Neidan", "气功", 4),
            ("clasicos", "Los clásicos: Su Wen", "素问", 4)
        };

        public override string Title => "Temario";

        public override void Build()
        {
            Root.Clear();
            var curriculum = S.Curriculum;
            Root.Append(El.Text("Ruta de desafíos", "page-title"));
            Root.Append(El.Text("Cada eje es un camino de desafíos: supera las lecciones en orden (con 70 %), vence el desafío de cada unidad y aprueba el examen del eje para ganar su sello. Un eje nuevo se abre cuando dominas el 70 % de los que necesita.", "page-lead"));
            if (S.Progress.State.FreeMode)
                Root.Append(El.Text("Modo libre activo: puedes estudiar cualquier lección.", "pill", "pill--jade"));
            Root.Add(ChallengeUi.LevelCard(Shell));

            var path = Root.Append(El.Div("path"));
            var levels = curriculum.Axes.Select(a => a.Level).Concat(Upcoming.Where(u => curriculum.Axis(u.id) == null).Select(u => u.level))
                .Distinct().OrderBy(l => l);
            foreach (var level in levels)
            {
                path.Append(El.Text(level < LevelNames.Length ? LevelNames[level] : "Nivel " + level, "path__level"));
                foreach (var axis in curriculum.Axes.Where(a => a.Level == level))
                    path.Add(AxisCard(Shell, axis, false));
                foreach (var upcoming in Upcoming.Where(u => u.level == level && curriculum.Axis(u.id) == null))
                {
                    var card = path.Append(El.Div("axis-card", "axis-card--soon"));
                    card.Append(El.Text(upcoming.hanzi, "axis-card__hanzi", "hanzi"));
                    var body = card.Append(El.Div("axis-card__body"));
                    body.Append(El.Text(upcoming.title, "axis-card__title"));
                    body.Append(El.Text("Próximamente", "pill"));
                }
            }
        }

        public static VisualElement AxisCard(AppShell shell, Axis axis, bool compact)
        {
            var services = shell.Services;
            var progress = services.Progress;
            var unlocked = progress.IsAxisUnlocked(axis);
            var premiumLocked = !services.Entitlements.CanStudy(axis);
            var color = El.Hex(axis.Color);
            var value = progress.AxisProgress(axis);
            var lessons = axis.Lessons.ToList();

            var card = El.Div("axis-card", compact ? "axis-card--compact" : null);
            card.EnableInClassList("axis-card--locked", !unlocked || premiumLocked);
            card.style.borderLeftColor = color;
            card.Append(El.Text(axis.Hanzi, "axis-card__hanzi", "hanzi")).Ink(color);
            var body = card.Append(El.Div("axis-card__body"));
            var head = body.Append(El.Row("axis-card__head"));
            head.Append(El.Text(axis.Title, "axis-card__title"));
            head.Append(El.Spacer());
            var challenges = services.Challenges;
            var badge = challenges.HasBadge(axis);
            if (premiumLocked)
                head.Append(new Icon(IconKind.Crown)).Ink(Theme.Gold);
            else if (!unlocked)
                head.Append(new Icon(IconKind.Lock)).Ink(Theme.Muted);
            else if (badge)
                head.Append(ChallengeUi.Seal(axis, true, "seal--tiny"));
            else if (value >= 0.999f)
                head.Append(new Icon(IconKind.Check)).Ink(Theme.Ok);
            if (!compact && !string.IsNullOrEmpty(axis.Subtitle))
                body.Append(El.Text(axis.Subtitle, "axis-card__subtitle"));
            if (!compact)
                body.Add(Track(services, axis, color));
            body.Append(El.Bar(value, color, "axis-card__bar")).Grow(value);
            var meta = $"{lessons.Count(l => progress.IsCompleted(l.Id))} de {lessons.Count} lecciones · {challenges.AxisStars(axis)}/{challenges.MaxAxisStars(axis)} estrellas" +
                       (badge ? " · sello obtenido" : string.Empty);
            if (!unlocked && !premiumLocked)
            {
                var missing = progress.MissingRequirements(axis).Select(a => a.Title).ToList();
                if (missing.Count > 0)
                    meta = "Requiere: " + string.Join(", ", missing);
            }
            if (premiumLocked)
                meta = "Contenido Premium";
            body.Append(El.Text(meta, "axis-card__meta"));
            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (premiumLocked)
                    shell.Push(new PlansPage());
                else
                    shell.Push(new AxisPage(axis));
            });
            return card;
        }

        /// <summary>El mapa del eje en miniatura: un punto por desafío (lecciones, desafíos de unidad y examen).</summary>
        private static VisualElement Track(Core.AppServices services, Axis axis, Color color)
        {
            var challenges = services.Challenges;
            var track = El.Row("ctrack");
            var next = challenges.NextChallenge();
            foreach (var stage in challenges.Stages(axis))
            {
                var status = challenges.Status(stage);
                var dot = track.Append(El.Div("ctrack__dot", "ctrack__dot--" + stage.Kind.ToString().ToLowerInvariant(),
                    "ctrack__dot--" + status.ToString().ToLowerInvariant()));
                if (status == ChallengeStatus.Passed)
                    dot.style.backgroundColor = stage.Kind == ChallengeKind.Axis ? Theme.Cinnabar : color;
                else if (status != ChallengeStatus.Locked)
                    dot.style.borderTopColor = dot.style.borderBottomColor = dot.style.borderLeftColor = dot.style.borderRightColor = color;
                if (next != null && next.Id == stage.Id)
                    Anim.Pulse(dot, "ctrack__dot--beat", 700);
            }
            return track;
        }
    }
}
