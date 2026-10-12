using System;
using System.Collections.Generic;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Diagrams
{
    /// <summary>Lo que se muestra en la ficha de detalle al tocar una parte del diagrama.</summary>
    public class DiagramSelection
    {
        public string Title;
        public string Hanzi;
        public string Text;
        public Reference Ref;
        public Color Color = Theme.Jade;
    }

    /// <summary>
    /// Lienzo de un diagrama: el alto sigue al ancho según <see cref="DiagramSpec.Aspect"/>. Las formas se pintan
    /// con Painter2D y los textos son etiquetas superpuestas (así usan las fuentes de la interfaz, incluido el chino).
    /// </summary>
    public abstract class DiagramCanvas : VisualElement
    {
        public DiagramSpec Spec { get; }
        public event Action<DiagramSelection> Selected;

        protected float W;
        protected float H;
        protected readonly HashSet<string> FocusIds = new HashSet<string>();

        private readonly HashSet<string> _layersOn = new HashSet<string>();

        protected DiagramCanvas(DiagramSpec spec)
        {
            Spec = spec;
            AddToClassList("diagram-canvas");
            foreach (var layer in spec.Layers)
                if (layer.On)
                    _layersOn.Add(layer.Id);
            generateVisualContent += ctx =>
            {
                if (W > 1f && H > 1f)
                    Paint(ctx.painter2D);
            };
            RegisterCallback<GeometryChangedEvent>(_ => Resize());
        }

        public static DiagramCanvas Create(DiagramSpec spec)
        {
            switch (spec.Kind)
            {
                case "ring": return new RingDiagram(spec);
                case "balance": return new BalanceDiagram(spec);
                case "tongue": return new TongueDiagram(spec);
                default: return new GraphDiagram(spec);
            }
        }

        private void Resize()
        {
            var w = resolvedStyle.width;
            if (float.IsNaN(w) || w < 1f)
                return;
            var h = Mathf.Round(w * Mathf.Clamp(Spec.Aspect, 0.3f, 1.6f));
            if (Mathf.Abs(resolvedStyle.height - h) > 1f)
                style.height = h;
            if (Mathf.Abs(w - W) < 0.5f && Mathf.Abs(h - H) < 0.5f)
                return;
            W = w;
            H = h;
            Layout();
            MarkDirtyRepaint();
        }

        /// <summary>Recoloca las etiquetas para el tamaño actual (W × H).</summary>
        protected abstract void Layout();

        protected abstract void Paint(Painter2D painter);

        /// <summary>Controles propios bajo el lienzo (por ejemplo, los deslizadores de la balanza).</summary>
        public virtual void BuildControls(VisualElement slot)
        {
        }

        public bool LayerOn(string id) => string.IsNullOrEmpty(id) || _layersOn.Contains(id);

        public void SetLayer(string id, bool on)
        {
            if (on)
                _layersOn.Add(id);
            else
                _layersOn.Remove(id);
            Refresh();
        }

        public void ResetLayers()
        {
            _layersOn.Clear();
            foreach (var layer in Spec.Layers)
                if (layer.On)
                    _layersOn.Add(layer.Id);
            Refresh();
        }

        /// <summary>Aplica un paso del recorrido guiado (null = sale del recorrido).</summary>
        public void ApplyStep(DiagramStep step)
        {
            FocusIds.Clear();
            if (step == null)
            {
                ResetLayers();
                return;
            }
            foreach (var id in step.Focus)
                FocusIds.Add(id);
            if (step.Layers != null)
            {
                _layersOn.Clear();
                foreach (var id in step.Layers)
                    _layersOn.Add(id);
            }
            Refresh();
        }

        public void Refresh()
        {
            if (W > 1f)
                Layout();
            MarkDirtyRepaint();
        }

        /// <summary>Miniatura de la galería: sin interacción.</summary>
        public void MakeStatic()
        {
            pickingMode = PickingMode.Ignore;
            this.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
        }

        protected void Select(DiagramSelection selection) => Selected?.Invoke(selection);

        protected float Font(float relative, float min = 9f, float max = 18f) => Mathf.Clamp(W * relative, min, max);

        /// <summary>Etiqueta centrada en un punto.</summary>
        protected static void Place(VisualElement label, Vector2 center)
        {
            label.style.left = center.x;
            label.style.top = center.y;
        }

        protected static Label Centered(string text, params string[] classes)
        {
            var label = El.Text(text, classes);
            label.AddToClassList("dlabel");
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            return label;
        }

        // ---- Ayudas de dibujo ---------------------------------------------------------------------------------

        protected static void Circle(Painter2D p, Vector2 c, float r, Color? fill, Color? stroke = null, float width = 1f)
        {
            p.BeginPath();
            p.Arc(c, r, Angle.Degrees(0f), Angle.Degrees(360f));
            p.ClosePath();
            if (fill.HasValue)
            {
                p.fillColor = fill.Value;
                p.Fill();
            }
            if (stroke.HasValue)
            {
                p.strokeColor = stroke.Value;
                p.lineWidth = width;
                p.Stroke();
            }
        }

        protected static void Dashed(Painter2D p, IList<Vector2> points, float dash, float gap)
        {
            var draw = true;
            var left = dash;
            p.BeginPath();
            p.MoveTo(points[0]);
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var segment = Vector2.Distance(a, b);
                var done = 0f;
                while (segment - done > left)
                {
                    done += left;
                    var point = Vector2.Lerp(a, b, done / segment);
                    if (draw)
                        p.LineTo(point);
                    else
                        p.MoveTo(point);
                    draw = !draw;
                    left = draw ? dash : gap;
                }
                left -= segment - done;
                if (draw)
                    p.LineTo(b);
                else
                    p.MoveTo(b);
            }
            p.Stroke();
        }

        protected static void Arrow(Painter2D p, Vector2 tip, Vector2 direction, float size, Color color)
        {
            var d = direction.normalized;
            var n = new Vector2(-d.y, d.x);
            p.BeginPath();
            p.MoveTo(tip);
            p.LineTo(tip - d * size + n * size * 0.55f);
            p.LineTo(tip - d * size * 0.7f);
            p.LineTo(tip - d * size - n * size * 0.55f);
            p.ClosePath();
            p.fillColor = color;
            p.Fill();
        }
    }
}
