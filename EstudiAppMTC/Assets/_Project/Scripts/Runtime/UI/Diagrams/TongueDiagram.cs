using System.Collections.Generic;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Diagrams
{
    /// <summary>
    /// Mapa de la lengua por zonas (vista de frente, punta abajo). Ids de zona: raiz, centro, lado-izq, lado-der,
    /// pulmon (entre centro y punta) y punta.
    /// </summary>
    public class TongueDiagram : DiagramCanvas
    {
        private struct Ellipse
        {
            public Vector2 Center;
            public Vector2 Radius;

            public bool Contains(Vector2 point)
            {
                var d = point - Center;
                return d.x * d.x / (Radius.x * Radius.x) + d.y * d.y / (Radius.y * Radius.y) <= 1f;
            }
        }

        private static readonly Dictionary<string, (float x, float y, float rx, float ry)> Geometry =
            new Dictionary<string, (float, float, float, float)>
            {
                { "raiz", (0f, 0.16f, 0.62f, 0.12f) },
                { "centro", (0f, 0.45f, 0.42f, 0.15f) },
                { "lado-izq", (-0.74f, 0.44f, 0.17f, 0.23f) },
                { "lado-der", (0.74f, 0.44f, 0.17f, 0.23f) },
                { "pulmon", (0f, 0.69f, 0.34f, 0.075f) },
                { "punta", (0f, 0.85f, 0.3f, 0.065f) }
            };

        private readonly Dictionary<DiagramZone, Ellipse> _zones = new Dictionary<DiagramZone, Ellipse>();
        private readonly Dictionary<DiagramZone, Label> _labels = new Dictionary<DiagramZone, Label>();
        private DiagramZone _selected;
        private float _cx;
        private float _top;
        private float _bottom;
        private float _half;

        public TongueDiagram(DiagramSpec spec) : base(spec)
        {
            AddToClassList("diagram-canvas--tongue");
            foreach (var zone in spec.Zones)
            {
                if (!Geometry.ContainsKey(zone.Id))
                    continue;
                var label = Centered($"<b>{zone.Label}</b>" + (string.IsNullOrEmpty(zone.Organs) ? string.Empty : $"\n<size=82%>{zone.Organs}</size>"), "tongue-label");
                _labels[zone] = label;
                Add(label);
            }
            RegisterCallback<ClickEvent>(OnClick);
        }

        protected override void Layout()
        {
            _cx = W / 2f;
            _top = H * 0.06f;
            _bottom = H * 0.95f;
            _half = Mathf.Min(W * 0.3f, H * 0.32f);
            var length = _bottom - _top;
            _zones.Clear();
            foreach (var pair in _labels)
            {
                var g = Geometry[pair.Key.Id];
                var ellipse = new Ellipse
                {
                    Center = new Vector2(_cx + g.x * _half, _top + g.y * length),
                    Radius = new Vector2(g.rx * _half, g.ry * length)
                };
                _zones[pair.Key] = ellipse;
                Place(pair.Value, ellipse.Center);
                pair.Value.style.fontSize = Font(0.024f, 9f, 15f);
                pair.Value.style.maxWidth = Mathf.Max(ellipse.Radius.x * 2.2f, 70f);
            }
        }

        protected override void Paint(Painter2D p)
        {
            var length = _bottom - _top;
            var q = length * 0.07f;
            p.BeginPath();
            p.MoveTo(new Vector2(_cx - _half, _top + q));
            p.QuadraticCurveTo(new Vector2(_cx, _top - q), new Vector2(_cx + _half, _top + q));
            p.BezierCurveTo(new Vector2(_cx + _half * 1.08f, _top + length * 0.55f), new Vector2(_cx + _half * 0.78f, _bottom), new Vector2(_cx, _bottom));
            p.BezierCurveTo(new Vector2(_cx - _half * 0.78f, _bottom), new Vector2(_cx - _half * 1.08f, _top + length * 0.55f), new Vector2(_cx - _half, _top + q));
            p.ClosePath();
            p.fillColor = new Color32(0xEC, 0xAE, 0xA8, 0xFF);
            p.Fill();
            p.strokeColor = new Color32(0xC7, 0x7C, 0x79, 0xFF);
            p.lineWidth = 2f;
            p.Stroke();

            p.strokeColor = new Color32(0xD4, 0x8E, 0x8A, 0xFF);
            p.lineWidth = 1.5f;
            p.BeginPath();
            p.MoveTo(new Vector2(_cx, _top + length * 0.05f));
            p.LineTo(new Vector2(_cx, _top + length * 0.6f));
            p.Stroke();

            foreach (var pair in _zones)
            {
                var color = El.Hex(pair.Key.Color, Theme.Cinnabar);
                var selected = pair.Key == _selected;
                EllipsePath(p, pair.Value);
                p.fillColor = color.Alpha(selected ? 0.75f : 0.3f);
                p.Fill();
                p.strokeColor = color.Alpha(selected ? 1f : 0.6f);
                p.lineWidth = selected ? 2.5f : 1.2f;
                p.Stroke();
            }
        }

        private static void EllipsePath(Painter2D p, Ellipse e)
        {
            p.BeginPath();
            const int steps = 40;
            for (var i = 0; i <= steps; i++)
            {
                var a = i * Mathf.PI * 2f / steps;
                var point = e.Center + new Vector2(Mathf.Cos(a) * e.Radius.x, Mathf.Sin(a) * e.Radius.y);
                if (i == 0)
                    p.MoveTo(point);
                else
                    p.LineTo(point);
            }
            p.ClosePath();
        }

        private void OnClick(ClickEvent e)
        {
            var point = (Vector2)e.localPosition;
            _selected = null;
            foreach (var pair in _zones)
                if (pair.Value.Contains(point))
                {
                    _selected = pair.Key;
                    break;
                }
            Refresh();
            if (_selected == null)
            {
                Select(null);
                return;
            }
            Select(new DiagramSelection
            {
                Title = _selected.Label,
                Text = (string.IsNullOrEmpty(_selected.Organs) ? string.Empty : $"<b>{_selected.Organs}</b>\n") + _selected.Detail,
                Color = El.Hex(_selected.Color, Theme.Cinnabar)
            });
        }
    }
}
