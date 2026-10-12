using System;
using System.Collections.Generic;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Diagrams
{
    /// <summary>
    /// Anillo de segmentos. Como reloj de 24 h: mediodía arriba, medianoche abajo y las horas avanzan en el
    /// sentido de las agujas; la aguja marca la hora actual. Con Center = "taiji" dibuja el símbolo en el centro.
    /// </summary>
    public class RingDiagram : DiagramCanvas
    {
        private readonly List<Label> _labels = new List<Label>();
        private readonly List<Label> _hourLabels = new List<Label>();
        private readonly Label _centerLabel;
        private readonly float[] _start;
        private readonly float[] _span;
        private int _selected = -1;
        private Vector2 _center;
        private float _outer;
        private float _inner;

        private bool Clock => Spec.ShowClock;
        private bool Taiji => Spec.Center == "taiji";

        public RingDiagram(DiagramSpec spec) : base(spec)
        {
            AddToClassList("diagram-canvas--ring");
            var segments = spec.Segments;
            var n = Math.Max(1, segments.Count);
            _start = new float[n];
            _span = new float[n];
            var hasHours = false;
            foreach (var s in segments)
                hasHours |= s.Hour > 0f;
            for (var i = 0; i < segments.Count; i++)
            {
                var hour = hasHours ? segments[i].Hour : spec.StartHour + i * 24f / n;
                var next = hasHours ? segments[(i + 1) % n].Hour : hour + 24f / n;
                var hours = ((next - hour) % 24f + 24f) % 24f;
                if (hours < 1e-3f)
                    hours = 24f / n;
                if (Clock || hasHours)
                {
                    _start[i] = HourAngle(hour);
                    _span[i] = hours * 15f;
                }
                else
                {
                    _start[i] = -90f + i * 360f / n;
                    _span[i] = 360f / n;
                }
            }

            foreach (var segment in segments)
            {
                var text = string.IsNullOrEmpty(segment.Hanzi) ? segment.Label : $"<size=125%>{segment.Hanzi}</size>\n{segment.Label}";
                if (!string.IsNullOrEmpty(segment.Sub) && n <= 6)
                    text += $"\n<size=80%><color=#00000099>{segment.Sub}</color></size>";
                var label = Centered(text, "ring-label");
                _labels.Add(label);
                Add(label);
            }
            if (Clock)
                for (var h = 0; h < 24; h += 2)
                {
                    var label = Centered(h.ToString(), "ring-hour");
                    _hourLabels.Add(label);
                    Add(label);
                }
            _centerLabel = Centered(string.Empty, "ring-center");
            Add(_centerLabel);

            RegisterCallback<ClickEvent>(OnClick);
            if (Clock)
                schedule.Execute(() =>
                {
                    UpdateCenter();
                    MarkDirtyRepaint();
                }).Every(30000);
        }

        /// <summary>Ángulo en pantalla (grados, 0 = derecha, crece en sentido horario) de una hora del día.</summary>
        public static float HourAngle(float hour) => -90f + (hour - 12f) * 15f;

        private static float NowHour()
        {
            var now = DateTime.Now;
            return now.Hour + now.Minute / 60f;
        }

        private int SegmentAt(float angle)
        {
            for (var i = 0; i < _start.Length; i++)
            {
                var delta = ((angle - _start[i]) % 360f + 360f) % 360f;
                if (delta < _span[i])
                    return i;
            }
            return -1;
        }

        protected override void Layout()
        {
            _center = new Vector2(W / 2f, H / 2f);
            _outer = Mathf.Min(W, H) * (Clock ? 0.41f : 0.46f);
            _inner = _outer * (Taiji ? 0.6f : 0.5f);
            var n = Math.Max(1, _labels.Count);
            var fontSize = Mathf.Clamp(_outer * (n > 8 ? 0.062f : 0.075f), 8f, 16f);
            for (var i = 0; i < _labels.Count; i++)
            {
                var mid = (_start[i] + _span[i] / 2f) * Mathf.Deg2Rad;
                var radius = (_outer + _inner) / 2f;
                Place(_labels[i], _center + new Vector2(Mathf.Cos(mid), Mathf.Sin(mid)) * radius);
                _labels[i].style.fontSize = fontSize;
                _labels[i].style.color = i == _selected ? Color.white : Theme.Ink;
            }
            for (var i = 0; i < _hourLabels.Count; i++)
            {
                var angle = HourAngle(i * 2) * Mathf.Deg2Rad;
                Place(_hourLabels[i], _center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (_outer + Mathf.Max(12f, _outer * 0.1f)));
                _hourLabels[i].style.fontSize = Mathf.Clamp(_outer * 0.055f, 8f, 13f);
            }
            Place(_centerLabel, _center);
            _centerLabel.style.fontSize = Mathf.Clamp(_inner * 0.16f, 9f, 18f);
            _centerLabel.style.maxWidth = _inner * 1.6f;
            UpdateCenter();
        }

        private void UpdateCenter()
        {
            if (Taiji)
            {
                _centerLabel.text = string.Empty;
                return;
            }
            if (_selected >= 0)
            {
                var s = Spec.Segments[_selected];
                _centerLabel.text = string.IsNullOrEmpty(s.Hanzi) ? s.Label : $"<size=180%>{s.Hanzi}</size>\n{s.Label}";
            }
            else if (Clock)
            {
                var now = SegmentAt(HourAngle(NowHour()));
                var time = DateTime.Now.ToString("HH:mm");
                _centerLabel.text = now >= 0 ? $"<size=80%>Ahora · {time}</size>\n<b>{Spec.Segments[now].Label}</b>" : time;
            }
            else
                _centerLabel.text = string.Empty;
        }

        protected override void Paint(Painter2D p)
        {
            var current = Clock ? SegmentAt(HourAngle(NowHour())) : -1;
            for (var i = 0; i < _start.Length && i < Spec.Segments.Count; i++)
            {
                var color = El.Hex(Spec.Segments[i].Color);
                var outer = _outer + (i == _selected ? _outer * 0.04f : 0f);
                var fill = i == _selected ? color : color.Mix(Color.white, i == current ? 0.45f : 0.7f);
                Sector(p, _center, _inner, outer, _start[i], _start[i] + _span[i], fill);
                p.strokeColor = Theme.Card;
                p.lineWidth = Mathf.Max(1.5f, _outer * 0.012f);
                p.Stroke();
                if (i == current)
                {
                    Sector(p, _center, _inner, outer, _start[i], _start[i] + _span[i], null);
                    p.strokeColor = color;
                    p.lineWidth = Mathf.Max(2f, _outer * 0.018f);
                    p.Stroke();
                }
            }

            if (Clock)
            {
                p.strokeColor = Theme.Muted;
                p.lineWidth = 1f;
                for (var h = 0; h < 24; h++)
                {
                    var a = HourAngle(h) * Mathf.Deg2Rad;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    p.BeginPath();
                    p.MoveTo(_center + d * (_outer + 2f));
                    p.LineTo(_center + d * (_outer + (h % 2 == 0 ? 6f : 3.5f)));
                    p.Stroke();
                }
            }

            if (Taiji)
                DrawTaiji(p, _center, _inner * 0.86f);

            if (Clock)
            {
                var a = HourAngle(NowHour()) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.strokeColor = Theme.Cinnabar;
                p.lineWidth = Mathf.Max(2f, _outer * 0.014f);
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.MoveTo(_center + d * (Taiji ? _inner * 0.9f : _inner * 0.25f));
                p.LineTo(_center + d * (_outer + 8f));
                p.Stroke();
                Circle(p, _center + d * (_outer + 8f), Mathf.Max(3f, _outer * 0.02f), Theme.Cinnabar);
            }
        }

        private static void Sector(Painter2D p, Vector2 c, float inner, float outer, float from, float to, Color? fill)
        {
            p.BeginPath();
            p.Arc(c, outer, Angle.Degrees(from), Angle.Degrees(to));
            p.Arc(c, inner, Angle.Degrees(to), Angle.Degrees(from), ArcDirection.CounterClockwise);
            p.ClosePath();
            if (fill.HasValue)
            {
                p.fillColor = fill.Value;
                p.Fill();
            }
        }

        /// <summary>Taiji con el Yang (claro) máximo arriba (mediodía) y el Yin (oscuro) máximo abajo (medianoche).</summary>
        public static void DrawTaiji(Painter2D p, Vector2 c, float r)
        {
            var yin = Theme.Night;
            var yang = new Color(0.98f, 0.96f, 0.9f);
            Circle(p, c, r, yin);
            p.BeginPath();
            p.Arc(c, r, Angle.Degrees(90f), Angle.Degrees(270f));
            p.ClosePath();
            p.fillColor = yang;
            p.Fill();
            Circle(p, c + new Vector2(0f, -r / 2f), r / 2f, yang);
            Circle(p, c + new Vector2(0f, r / 2f), r / 2f, yin);
            Circle(p, c + new Vector2(0f, -r / 2f), r * 0.13f, yin);
            Circle(p, c + new Vector2(0f, r / 2f), r * 0.13f, yang);
            Circle(p, c, r, null, yin, Mathf.Max(1.5f, r * 0.03f));
        }

        private void OnClick(ClickEvent e)
        {
            var d = (Vector2)e.localPosition - _center;
            var distance = d.magnitude;
            var index = distance >= _inner && distance <= _outer * 1.06f
                ? SegmentAt(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg)
                : -1;
            _selected = index;
            Refresh();
            if (index < 0)
            {
                Select(null);
                return;
            }
            var segment = Spec.Segments[index];
            Select(new DiagramSelection
            {
                Title = string.IsNullOrEmpty(segment.Sub) ? segment.Label : $"{segment.Label} · {segment.Sub}",
                Hanzi = segment.Hanzi,
                Text = segment.Detail,
                Color = El.Hex(segment.Color)
            });
        }
    }
}
