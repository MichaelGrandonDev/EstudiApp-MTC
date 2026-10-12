using System;
using System.Collections.Generic;
using System.Linq;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI
{
    /// <summary>Piezas de la ruta de desafíos: estrellas, sellos, nivel, mapa y el ejercicio de emparejar.</summary>
    public static class ChallengeUi
    {
        /// <summary>Fila de estrellas; con <paramref name="animate"/> aparecen una a una.</summary>
        public static VisualElement Stars(int stars, int max = 3, string size = null, bool animate = false, long delayMs = 0)
        {
            var row = El.Row("stars", size);
            for (var i = 0; i < max; i++)
            {
                var on = i < stars;
                var star = row.Append(new Icon(on ? IconKind.StarFilled : IconKind.Star));
                star.AddToClassList(on ? "star--on" : "star--off");
                if (animate && on)
                    star.PopIn(delayMs + i * 280);
            }
            return row;
        }

        /// <summary>Sello del eje (como un sello chino de bermellón con su hanzi); apagado si todavía no se ganó.</summary>
        public static VisualElement Seal(Axis axis, bool earned, string size = null)
        {
            var seal = El.Div("seal", size, earned ? "seal--on" : "seal--off");
            seal.Append(El.Text(axis.Hanzi, "seal__hanzi", "hanzi"));
            seal.pickingMode = PickingMode.Ignore;
            return seal;
        }

        /// <summary>Nivel, experiencia hacia el siguiente nivel, estrellas y sellos.</summary>
        public static VisualElement LevelCard(AppShell shell, bool animate = true)
        {
            var services = shell.Services;
            var level = services.Progress.Level;
            var challenges = services.Challenges;
            var card = El.Div("level-card");
            var badge = card.Append(El.Div("level-card__badge"));
            badge.Append(El.Text(level.Level.ToString(), "level-card__number"));
            badge.Append(El.Text("nivel", "level-card__caption"));
            var body = card.Append(El.Div("level-card__body"));
            var head = body.Append(El.Row("level-card__head"));
            head.Append(El.Text(level.Title, "level-card__title"));
            head.Append(El.Spacer());
            var xp = head.Append(El.Row("xp-chip"));
            xp.Append(new Icon(IconKind.Bolt));
            var xpLabel = xp.Append(El.Text($"{level.Xp} XP", "xp-chip__text"));
            var bar = body.Append(El.Bar(level.Fraction, Theme.Gold, "level-card__bar"));
            if (animate)
            {
                bar.Grow(level.Fraction, 250);
                Anim.CountUp(xpLabel, 0, level.Xp, 900, v => $"{v} XP", 150);
            }
            body.Append(El.Text($"Faltan {Math.Max(0, level.Next - level.Xp)} XP para el nivel {level.Level + 1} · {Rewards.Title(level.Level + 1)}", "level-card__meta"));

            var side = card.Append(El.Div("level-card__side"));
            var stars = side.Append(El.Row("level-card__stat"));
            stars.Append(new Icon(IconKind.StarFilled)).AddToClassList("star--on");
            stars.Append(El.Text($"{challenges.TotalStars()} / {challenges.MaxStars()}", "level-card__stat-text"));
            var seals = side.Append(El.Row("level-card__seals"));
            foreach (var axis in services.Curriculum.Axes)
                seals.Add(Seal(axis, challenges.HasBadge(axis), "seal--tiny"));
            return card;
        }

        public static IconKind KindIcon(ChallengeKind kind) =>
            kind == ChallengeKind.Unit ? IconKind.Shield : kind == ChallengeKind.Axis ? IconKind.Trophy : kind == ChallengeKind.Practice ? IconKind.Target : IconKind.Book;

        public static string KindName(ChallengeKind kind) =>
            kind == ChallengeKind.Unit ? "Desafío de unidad" : kind == ChallengeKind.Axis ? "Examen del eje" : kind == ChallengeKind.Practice ? "Mini test" : "Lección";
    }

    /// <summary>Contenedor del mapa de desafíos: dibuja el camino que une los nodos (coloreado hasta donde se llegó).</summary>
    public class ChallengeMap : VisualElement
    {
        private readonly List<(VisualElement node, bool reached)> _nodes = new List<(VisualElement, bool)>();
        private readonly Color _color;

        public ChallengeMap(Color color)
        {
            _color = color;
            AddToClassList("cmap");
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            // Los nodos entran con una transición (translate no dispara GeometryChangedEvent): seguirlos un momento.
            schedule.Execute(MarkDirtyRepaint).Every(40).ForDuration(2000);
        }

        public void Track(VisualElement node, bool reached)
        {
            _nodes.Add((node, reached));
            node.RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        private void Draw(MeshGenerationContext ctx)
        {
            if (_nodes.Count < 2)
                return;
            var p = ctx.painter2D;
            p.lineWidth = 6f;
            p.lineCap = LineCap.Round;
            var points = _nodes.Select(n => n.node.panel == null ? Vector2.zero : n.node.ChangeCoordinatesTo(this, n.node.contentRect.center)).ToList();
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                if (a == Vector2.zero || b == Vector2.zero)
                    continue;
                var midY = (a.y + b.y) / 2f;
                p.strokeColor = _nodes[i].reached ? Theme.Readable(_color, 0.18f).Alpha(0.85f) : Theme.Line;
                p.BeginPath();
                p.MoveTo(a);
                p.BezierCurveTo(new Vector2(a.x, midY), new Vector2(b.x, midY), b);
                p.Stroke();
            }
        }
    }

    /// <summary>
    /// Emparejar términos (hanzi y pinyin) con su significado. Se toca un término y luego su significado;
    /// <c>onDone</c> recibe true si se completó sin fallos.
    /// </summary>
    public class MatchView : VisualElement
    {
        private readonly List<Term> _pairs;
        private readonly Action<bool> _onDone;
        private readonly List<Button> _left = new List<Button>();
        private readonly List<Button> _right = new List<Button>();
        private readonly HashSet<int> _matched = new HashSet<int>();
        private readonly VisualElement _feedback;
        private Button _pickLeft;
        private Button _pickRight;
        private int _mistakes;
        private bool _done;

        public bool Done => _done;

        public MatchView(List<Term> pairs, Action<bool> onDone, System.Random random = null)
        {
            _pairs = pairs;
            _onDone = onDone;
            random ??= new System.Random();
            AddToClassList("question");
            AddToClassList("match");
            this.Append(El.Text("Empareja cada término con su significado", "question__prompt"));
            this.Append(El.Text("Toca un término y después su significado. Sin fallos cuenta como acierto.", "question__hint"));
            var columns = this.Append(El.Row("match__cols"));
            var left = columns.Append(El.Div("match__col"));
            var right = columns.Append(El.Div("match__col"));
            foreach (var index in Enumerable.Range(0, pairs.Count).OrderBy(_ => random.Next()).ToList())
            {
                var term = pairs[index];
                var button = left.Append(El.Btn(string.Empty, null, "option", "match__item"));
                button.clicked += () => Pick(button, true);
                if (!string.IsNullOrEmpty(term.Hanzi))
                    button.Append(El.Text(term.Hanzi, "match__hanzi", "hanzi")).pickingMode = PickingMode.Ignore;
                button.Append(El.Text(term.Pinyin, "match__pinyin")).pickingMode = PickingMode.Ignore;
                button.userData = index;
                _left.Add(button);
            }
            foreach (var index in Enumerable.Range(0, pairs.Count).OrderBy(_ => random.Next()).ToList())
            {
                var button = right.Append(El.Btn(string.Empty, null, "option", "match__item"));
                button.clicked += () => Pick(button, false);
                button.Append(El.Text(pairs[index].Es, "option__text")).pickingMode = PickingMode.Ignore;
                button.userData = index;
                _right.Add(button);
            }
            _feedback = this.Append(El.Div("feedback"));
            _feedback.SetVisible(false);
        }

        private void Pick(Button button, bool left)
        {
            if (_done || _matched.Contains((int)button.userData))
                return;
            if (left)
            {
                _pickLeft?.RemoveFromClassList("option--chosen");
                _pickLeft = button;
            }
            else
            {
                _pickRight?.RemoveFromClassList("option--chosen");
                _pickRight = button;
            }
            button.AddToClassList("option--chosen");
            if (_pickLeft == null || _pickRight == null)
                return;

            var a = _pickLeft;
            var b = _pickRight;
            _pickLeft = _pickRight = null;
            a.RemoveFromClassList("option--chosen");
            b.RemoveFromClassList("option--chosen");
            if ((int)a.userData == (int)b.userData)
            {
                _matched.Add((int)a.userData);
                foreach (var hit in new[] { a, b })
                {
                    hit.AddToClassList("option--right");
                    hit.AddToClassList("option--locked");
                    Anim.Pop(hit);
                }
                if (_matched.Count == _pairs.Count)
                    Finish(_mistakes == 0, _mistakes == 0 ? "¡Emparejado sin fallos!" : $"Emparejado con {_mistakes} fallo{(_mistakes == 1 ? "" : "s")}");
            }
            else
            {
                _mistakes++;
                foreach (var miss in new[] { a, b })
                {
                    miss.AddToClassList("option--wrong");
                    Anim.Shake(miss);
                    miss.schedule.Execute(() => miss.RemoveFromClassList("option--wrong")).StartingIn(600);
                }
            }
        }

        /// <summary>Se acabó el tiempo: cuenta como fallo y muestra las parejas.</summary>
        public void TimeUp()
        {
            if (!_done)
                Finish(false, "Se acabó el tiempo");
        }

        /// <summary>Completa el ejercicio (sin fallos con <paramref name="perfect"/>; si no, con un error).</summary>
        public void Solve(bool perfect)
        {
            if (_done)
                return;
            if (!perfect)
                _mistakes++;
            foreach (var button in _left.Concat(_right))
                button.AddToClassList("option--right");
            Finish(perfect, perfect ? "¡Emparejado sin fallos!" : $"Emparejado con {_mistakes} fallo{(_mistakes == 1 ? "" : "s")}");
        }

        private void Finish(bool ok, string title)
        {
            _done = true;
            foreach (var button in _left.Concat(_right))
                button.AddToClassList("option--locked");
            _feedback.SetVisible(true);
            _feedback.Enter();
            _feedback.EnableInClassList("feedback--ok", ok);
            _feedback.EnableInClassList("feedback--bad", !ok);
            var head = _feedback.Append(El.Row("feedback__head"));
            head.Append(new Icon(ok ? IconKind.Check : IconKind.Close));
            head.Append(El.Text(title, "feedback__title"));
            foreach (var term in _pairs)
                _feedback.Append(El.Text($"<b>{term.Hanzi} {term.Pinyin}</b> · {term.Es}", "feedback__text"));
            _onDone?.Invoke(ok);
        }
    }
}
