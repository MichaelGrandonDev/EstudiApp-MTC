using System.Collections.Generic;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Diagrams
{
    /// <summary>Balanza Yin-Yang: dos columnas que el estudiante sube o baja; el estado resultante explica el síndrome.</summary>
    public class BalanceDiagram : DiagramCanvas
    {
        private static readonly Color YinColor = new Color32(0x3F, 0x5A, 0x7A, 0xFF);
        private static readonly Color YangColor = new Color32(0xD4, 0x6A, 0x3A, 0xFF);

        /// <summary>Valores de ejemplo para cada estado (1 = normal).</summary>
        private static readonly Dictionary<string, Vector2> Presets = new Dictionary<string, Vector2>
        {
            { "equilibrio", new Vector2(1f, 1f) },
            { "exceso_yang", new Vector2(1f, 1.55f) },
            { "exceso_yin", new Vector2(1.55f, 1f) },
            { "vacio_yin", new Vector2(0.55f, 1f) },
            { "vacio_yang", new Vector2(1f, 0.55f) },
            { "exceso_yang_dana_yin", new Vector2(0.7f, 1.55f) },
            { "exceso_yin_dana_yang", new Vector2(1.55f, 0.7f) },
            { "vacio_ambos", new Vector2(0.55f, 0.55f) },
            { "plenitud_ambos", new Vector2(1.45f, 1.45f) }
        };

        private float _yin = 1f;
        private float _yang = 1f;
        private string _state;
        private readonly Label _yinLabel;
        private readonly Label _yangLabel;
        private readonly Label _normalLabel;
        private Slider _yinSlider;
        private Slider _yangSlider;
        private VisualElement _chips;

        public BalanceDiagram(DiagramSpec spec) : base(spec)
        {
            AddToClassList("diagram-canvas--balance");
            _yinLabel = Centered("Yin 阴", "balance-label");
            _yangLabel = Centered("Yang 阳", "balance-label");
            _normalLabel = Centered("nivel normal", "balance-normal");
            Add(_yinLabel);
            Add(_yangLabel);
            Add(_normalLabel);
        }

        public override void BuildControls(VisualElement slot)
        {
            var sliders = slot.Append(El.Div("balance-sliders"));
            _yinSlider = sliders.Append(new Slider("Yin", 0.3f, 1.8f) { value = _yin });
            _yinSlider.AddToClassList("balance-slider");
            _yinSlider.AddToClassList("balance-slider--yin");
            _yangSlider = sliders.Append(new Slider("Yang", 0.3f, 1.8f) { value = _yang });
            _yangSlider.AddToClassList("balance-slider");
            _yangSlider.AddToClassList("balance-slider--yang");
            _yinSlider.RegisterValueChangedCallback(e => SetValues(e.newValue, _yang, false));
            _yangSlider.RegisterValueChangedCallback(e => SetValues(_yin, e.newValue, false));

            _chips = slot.Append(El.Div("chips"));
            BuildChips();
            schedule.Execute(() => SetValues(1f, 1f, true, true));
        }

        private void BuildChips()
        {
            _chips.Clear();
            foreach (var state in Spec.States)
            {
                if (!Presets.TryGetValue(state.Id, out var values))
                    continue;
                var title = state.Title;
                var arrow = title.IndexOf('→');
                if (arrow > 0)
                    title = title.Substring(0, arrow).Trim();
                _chips.Add(El.Chip(title, () => SetValues(values.x, values.y, true), state.Id == _state));
            }
        }

        private void SetValues(float yin, float yang, bool moveSliders, bool force = false)
        {
            _yin = yin;
            _yang = yang;
            if (moveSliders)
            {
                _yinSlider?.SetValueWithoutNotify(yin);
                _yangSlider?.SetValueWithoutNotify(yang);
            }
            MarkDirtyRepaint();
            Layout();
            var id = YinYangBalance.Classify(yin, yang);
            if (id == _state && !force)
                return;
            _state = id;
            if (_chips != null)
                BuildChips();
            var state = Spec.States.Find(s => s.Id == id);
            if (state != null)
                Select(new DiagramSelection { Title = state.Title, Text = state.Text, Ref = state.Ref, Color = Tint(yin, yang) });
        }

        private static Color Tint(float yin, float yang) =>
            Mathf.Abs(yin - yang) < 0.2f ? Theme.Jade : yang > yin ? YangColor : YinColor;

        private float BaseY => H * 0.8f;
        private float MaxHeight => H * 0.62f;
        private float Column(float value) => value / 2f * MaxHeight;

        protected override void Layout()
        {
            if (W < 1f)
                return;
            var size = Font(0.03f, 11f, 18f);
            Place(_yinLabel, new Vector2(W * 0.33f, BaseY + size * 1.3f));
            Place(_yangLabel, new Vector2(W * 0.67f, BaseY + size * 1.3f));
            _yinLabel.style.fontSize = _yangLabel.style.fontSize = size;
            _yinLabel.style.color = YinColor;
            _yangLabel.style.color = YangColor;
            Place(_normalLabel, new Vector2(W * 0.88f, BaseY - Column(1f) - size * 0.8f));
            _normalLabel.style.fontSize = size * 0.7f;
        }

        protected override void Paint(Painter2D p)
        {
            var columnWidth = W * 0.16f;
            var normalY = BaseY - Column(1f);

            p.strokeColor = Theme.Line;
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(new Vector2(W * 0.12f, BaseY));
            p.LineTo(new Vector2(W * 0.88f, BaseY));
            p.Stroke();

            DrawColumn(p, W * 0.33f, columnWidth, Column(_yin), YinColor);
            DrawColumn(p, W * 0.67f, columnWidth, Column(_yang), YangColor);

            p.strokeColor = Theme.Muted;
            p.lineWidth = 1.5f;
            Dashed(p, new[] { new Vector2(W * 0.14f, normalY), new Vector2(W * 0.86f, normalY) }, 7f, 5f);

            // Fiel de la balanza: une la parte alta de las dos columnas.
            var a = new Vector2(W * 0.33f, BaseY - Column(_yin));
            var b = new Vector2(W * 0.67f, BaseY - Column(_yang));
            p.strokeColor = Theme.Ink.Alpha(0.6f);
            p.lineWidth = 2.5f;
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
            Circle(p, (a + b) / 2f, 5f, Theme.Ink);
            Circle(p, a, 4f, YinColor);
            Circle(p, b, 4f, YangColor);
        }

        private void DrawColumn(Painter2D p, float x, float width, float height, Color color)
        {
            var radius = Mathf.Min(10f, width * 0.2f);
            var top = BaseY - height;
            p.BeginPath();
            p.MoveTo(new Vector2(x - width / 2f, BaseY));
            p.LineTo(new Vector2(x - width / 2f, top + radius));
            p.ArcTo(new Vector2(x - width / 2f, top), new Vector2(x, top), radius);
            p.ArcTo(new Vector2(x + width / 2f, top), new Vector2(x + width / 2f, top + radius), radius);
            p.LineTo(new Vector2(x + width / 2f, BaseY));
            p.ClosePath();
            p.fillColor = color.Mix(Color.white, 0.15f);
            p.Fill();
        }
    }
}
