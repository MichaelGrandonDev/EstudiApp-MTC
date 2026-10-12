using System;
using System.Collections.Generic;
using System.Linq;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Diagrams
{
    /// <summary>Nodos y flechas (curvas, discontinuas, por capas): Wu Xing, Zang Fu, formación del Qi…</summary>
    public class GraphDiagram : DiagramCanvas
    {
        private sealed class NodeView
        {
            public DiagramNode Node;
            public VisualElement Element;
            public Label Hanzi;
            public Label Title;
            public Label Sub;
            public Vector2 Center;
            public Vector2 Half;
            public bool Round;
            public Color Color;
        }

        private readonly Dictionary<string, NodeView> _nodes = new Dictionary<string, NodeView>();
        private readonly Dictionary<DiagramEdge, Label> _edgeLabels = new Dictionary<DiagramEdge, Label>();
        private string _selectedNode;
        private DiagramEdge _selectedEdge;

        public GraphDiagram(DiagramSpec spec) : base(spec)
        {
            AddToClassList("diagram-canvas--graph");
            foreach (var node in spec.Nodes)
            {
                var view = new NodeView
                {
                    Node = node,
                    Round = node.Shape != "rect",
                    Color = El.Hex(node.Color),
                    Element = El.Div("dnode", node.Shape == "rect" ? "dnode--rect" : "dnode--round")
                };
                if (!string.IsNullOrEmpty(node.Hanzi))
                    view.Hanzi = view.Element.Append(El.Text(node.Hanzi, "dnode__hanzi"));
                view.Title = view.Element.Append(El.Text(node.Label, "dnode__label"));
                if (!string.IsNullOrEmpty(node.Sub))
                    view.Sub = view.Element.Append(El.Text(node.Sub, "dnode__sub"));
                foreach (var label in view.Element.Children())
                    label.pickingMode = PickingMode.Ignore;
                view.Element.RegisterCallback<ClickEvent>(e =>
                {
                    e.StopPropagation();
                    SelectNode(node.Id);
                });
                Add(view.Element);
                _nodes[node.Id] = view;
            }
            foreach (var edge in spec.Edges.Where(e => !string.IsNullOrEmpty(e.Label)))
            {
                var label = Centered(edge.Label, "dedge-label");
                _edgeLabels[edge] = label;
                Add(label);
            }
            RegisterCallback<ClickEvent>(OnClick);
        }

        private Color EdgeColor(DiagramEdge edge)
        {
            if (!string.IsNullOrEmpty(edge.Color))
                return El.Hex(edge.Color);
            var layer = Spec.Layer(edge.Layer);
            return layer != null ? El.Hex(layer.Color) : Theme.Muted;
        }

        private bool NodeVisible(NodeView view) => LayerOn(view.Node.Layer);

        private bool EdgeVisible(DiagramEdge edge) =>
            LayerOn(edge.Layer) && _nodes.TryGetValue(edge.From, out var a) && _nodes.TryGetValue(edge.To, out var b) &&
            NodeVisible(a) && NodeVisible(b);

        private bool Dim(string nodeId) => FocusIds.Count > 0 && !FocusIds.Contains(nodeId);

        private static int LongestWord(string text) =>
            Math.Max(4, (text ?? string.Empty).Split(' ', '-', '/').Max(w => w.Length));

        protected override void Layout()
        {
            var layout = Spec.Layout;
            var order = layout != null && layout.Type == "circle" ? layout.Order : new List<string>();
            foreach (var view in _nodes.Values)
            {
                var node = view.Node;
                var index = order.IndexOf(node.Id);
                if (index >= 0)
                {
                    var angle = (layout.StartAngle + index * 360f / order.Count) * Mathf.Deg2Rad;
                    view.Center = new Vector2(layout.CenterX * W + layout.Radius * W * Mathf.Cos(angle),
                        layout.CenterY * H + layout.Radius * W * Mathf.Sin(angle));
                }
                else
                    view.Center = new Vector2(node.X * W, node.Y * H);

                var lines = 1 + (view.Hanzi != null ? 1 : 0) + (view.Sub != null ? 1 : 0);
                float fontSize;
                if (view.Round)
                {
                    var r = node.Size * W;
                    view.Half = new Vector2(r, r);
                    fontSize = Mathf.Clamp(r * (lines > 2 ? 0.27f : 0.32f), 8f, 18f);
                }
                else
                {
                    fontSize = Font(0.024f, 8f, 16f);
                    var halfHeight = node.Height > 0f ? node.Height * W : (lines * fontSize * 1.25f + 10f) / 2f;
                    view.Half = new Vector2(node.Size * W, halfHeight);
                    fontSize = Mathf.Min(fontSize, (halfHeight * 2f - 6f) / (lines * 1.3f));
                }
                var inner = view.Half.x * (view.Round ? 1.75f : 1.9f);
                fontSize = Mathf.Min(fontSize, inner / (0.6f * LongestWord(node.Label)));
                fontSize = Mathf.Max(5f, fontSize);
                view.Title.style.fontSize = fontSize;
                if (view.Hanzi != null)
                    view.Hanzi.style.fontSize = fontSize * 1.2f;
                if (view.Sub != null)
                    view.Sub.style.fontSize = fontSize * 0.8f;

                var e = view.Element;
                e.style.left = view.Center.x - view.Half.x;
                e.style.top = view.Center.y - view.Half.y;
                e.style.width = view.Half.x * 2f;
                e.style.height = view.Half.y * 2f;
                if (view.Round)
                    e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = view.Half.x;
                e.SetVisible(NodeVisible(view));
                var selected = node.Id == _selectedNode;
                var dim = Dim(node.Id);
                e.style.backgroundColor = selected ? view.Color : Theme.Wash(view.Color, 0.84f);
                e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = Theme.Readable(view.Color, 0.18f);
                e.style.color = selected ? Color.white : Theme.Ink;
                e.style.opacity = dim ? 0.28f : 1f;
                e.EnableInClassList("dnode--selected", selected);
            }

            foreach (var pair in _edgeLabels)
            {
                var visible = EdgeVisible(pair.Key) && Curve(pair.Key, out var a, out var c, out var b);
                pair.Value.SetVisible(visible);
                if (!visible)
                    continue;
                Curve(pair.Key, out a, out c, out b);
                Place(pair.Value, Bezier(a, c, b, 0.5f));
                pair.Value.style.fontSize = Font(0.019f, 8f, 13f);
                pair.Value.style.color = Theme.Readable(EdgeColor(pair.Key).Mix(Theme.Ink, 0.35f));
                pair.Value.style.opacity = Dim(pair.Key.From) && Dim(pair.Key.To) ? 0.25f : 1f;
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t) =>
            (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;

        private Vector2 Boundary(NodeView view, Vector2 toward, float gap)
        {
            var d = toward - view.Center;
            if (d.sqrMagnitude < 1e-4f)
                return view.Center;
            var n = d.normalized;
            if (view.Round)
                return view.Center + n * (view.Half.x + gap);
            var tx = Mathf.Abs(n.x) < 1e-4f ? float.MaxValue : view.Half.x / Mathf.Abs(n.x);
            var ty = Mathf.Abs(n.y) < 1e-4f ? float.MaxValue : view.Half.y / Mathf.Abs(n.y);
            return view.Center + n * (Mathf.Min(tx, ty) + gap);
        }

        /// <summary>Curva cuadrática del borde de un nodo al del otro.</summary>
        private bool Curve(DiagramEdge edge, out Vector2 a, out Vector2 c, out Vector2 b)
        {
            a = c = b = Vector2.zero;
            if (!_nodes.TryGetValue(edge.From, out var from) || !_nodes.TryGetValue(edge.To, out var to))
                return false;
            var d = to.Center - from.Center;
            var length = d.magnitude;
            if (length < 1f)
                return false;
            var left = new Vector2(d.y, -d.x) / length;
            c = (from.Center + to.Center) / 2f + left * edge.Bend * length;
            var gap = Mathf.Max(2f, W * 0.004f);
            a = Boundary(from, c, gap);
            b = Boundary(to, c, gap);
            if (Mathf.Abs(edge.Bend) < 1e-3f)
                c = (a + b) / 2f;
            return true;
        }

        protected override void Paint(Painter2D p)
        {
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            var baseWidth = Mathf.Max(1.4f, W * 0.0035f);
            var arrowSize = Mathf.Max(7f, W * 0.016f);
            foreach (var edge in Spec.Edges)
            {
                if (!EdgeVisible(edge) || !Curve(edge, out var a, out var c, out var b))
                    continue;
                var color = Theme.Readable(EdgeColor(edge), 0.18f);
                var selected = edge == _selectedEdge;
                if (Dim(edge.From) && Dim(edge.To) || FocusIds.Count > 0 && (Dim(edge.From) || Dim(edge.To)))
                    color = color.Alpha(0.22f);
                var width = selected ? baseWidth * 2.2f : baseWidth;
                var arrowEnd = !edge.NoArrow;
                var arrowStart = edge.Both && !edge.NoArrow;

                // Recorta la curva para que la punta de flecha no quede tapada por el trazo.
                var start = arrowStart ? a + (c - a).normalized * arrowSize * 0.7f : a;
                var end = arrowEnd ? b + (c - b).normalized * arrowSize * 0.7f : b;
                var points = new List<Vector2>(25);
                for (var i = 0; i <= 24; i++)
                    points.Add(Bezier(start, c, end, i / 24f));

                p.strokeColor = color;
                p.lineWidth = width;
                if (edge.Dashed)
                    Dashed(p, points, width * 3.5f, width * 2.5f);
                else
                {
                    p.BeginPath();
                    p.MoveTo(points[0]);
                    for (var i = 1; i < points.Count; i++)
                        p.LineTo(points[i]);
                    p.Stroke();
                }
                if (arrowEnd)
                    Arrow(p, b, b - c, arrowSize * (selected ? 1.25f : 1f), color);
                if (arrowStart)
                    Arrow(p, a, a - c, arrowSize * (selected ? 1.25f : 1f), color);
            }
        }

        private void OnClick(ClickEvent e)
        {
            var point = (Vector2)e.localPosition;
            DiagramEdge best = null;
            var bestDistance = Mathf.Max(10f, W * 0.018f);
            foreach (var edge in Spec.Edges)
            {
                if (!EdgeVisible(edge) || !Curve(edge, out var a, out var c, out var b))
                    continue;
                for (var i = 0; i <= 20; i++)
                {
                    var distance = Vector2.Distance(point, Bezier(a, c, b, i / 20f));
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = edge;
                    }
                }
            }
            if (best != null && (!string.IsNullOrEmpty(best.Detail) || !string.IsNullOrEmpty(best.Label)))
                SelectEdge(best);
            else if (_selectedNode != null || _selectedEdge != null)
            {
                _selectedNode = null;
                _selectedEdge = null;
                Refresh();
                Select(null);
            }
        }

        public void SelectNode(string id)
        {
            if (!_nodes.TryGetValue(id, out var view))
                return;
            _selectedNode = id;
            _selectedEdge = null;
            Refresh();
            var node = view.Node;
            Select(new DiagramSelection
            {
                Title = node.Label,
                Hanzi = node.Hanzi,
                Text = !string.IsNullOrEmpty(node.Detail) ? node.Detail : node.Sub,
                Ref = node.Ref,
                Color = view.Color
            });
        }

        private void SelectEdge(DiagramEdge edge)
        {
            _selectedEdge = edge;
            _selectedNode = null;
            Refresh();
            var from = _nodes[edge.From].Node;
            var to = _nodes[edge.To].Node;
            var arrow = edge.Both ? " ↔ " : edge.NoArrow ? " — " : " → ";
            Select(new DiagramSelection
            {
                Title = from.Label + arrow + to.Label,
                Text = !string.IsNullOrEmpty(edge.Detail) ? edge.Detail : edge.Label,
                Color = EdgeColor(edge)
            });
        }
    }
}
