using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI
{
    public enum IconKind
    {
        Home,
        Path,
        Diagram,
        Review,
        Library,
        Search,
        Settings,
        Back,
        Forward,
        Close,
        Lock,
        Check,
        Star,
        Play,
        Plus,
        Minus,
        Bookmark,
        Note,
        Book,
        Trash,
        Flame,
        Clock,
        Crown,
        Link,
        Info,
        Upload,
        StarFilled,
        Trophy,
        Bolt,
        Target,
        Shield,
        Medal
    }

    /// <summary>
    /// Icono de trazos en una caja de 24×24 dibujado con Painter2D. Toma el color de la propiedad <c>color</c>
    /// de la hoja de estilos, así sigue los estados hover / activo de su botón.
    /// </summary>
    public class Icon : VisualElement
    {
        private static readonly CustomStyleProperty<float> StrokeProperty = new CustomStyleProperty<float>("--icon-stroke");

        private IconKind _kind;
        private float _stroke = 1.8f;
        private Painter2D _p;
        private float _s;
        private Vector2 _o;

        public Icon(IconKind kind)
        {
            _kind = kind;
            AddToClassList("icon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if (e.customStyle.TryGetValue(StrokeProperty, out var stroke))
                    _stroke = stroke;
                MarkDirtyRepaint();
            });
        }

        public IconKind Kind
        {
            get => _kind;
            set
            {
                _kind = value;
                MarkDirtyRepaint();
            }
        }

        private Vector2 P(float x, float y) => _o + new Vector2(x, y) * _s;

        private void Line(params float[] xy)
        {
            _p.BeginPath();
            _p.MoveTo(P(xy[0], xy[1]));
            for (var i = 2; i + 1 < xy.Length; i += 2)
                _p.LineTo(P(xy[i], xy[i + 1]));
            _p.Stroke();
        }

        private void Closed(params float[] xy)
        {
            _p.BeginPath();
            _p.MoveTo(P(xy[0], xy[1]));
            for (var i = 2; i + 1 < xy.Length; i += 2)
                _p.LineTo(P(xy[i], xy[i + 1]));
            _p.ClosePath();
            _p.Stroke();
        }

        private void Circle(float x, float y, float r, bool fill = false)
        {
            _p.BeginPath();
            _p.Arc(P(x, y), r * _s, Angle.Degrees(0f), Angle.Degrees(360f));
            _p.ClosePath();
            if (fill)
                _p.Fill();
            else
                _p.Stroke();
        }

        private void ArcStroke(float x, float y, float r, float from, float to)
        {
            _p.BeginPath();
            _p.Arc(P(x, y), r * _s, Angle.Degrees(from), Angle.Degrees(to));
            _p.Stroke();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            var size = Mathf.Min(rect.width, rect.height);
            if (size < 1f)
                return;
            _s = size / 24f;
            _o = new Vector2(rect.x + (rect.width - size) / 2f, rect.y + (rect.height - size) / 2f);
            var color = resolvedStyle.color;
            _p = ctx.painter2D;
            _p.strokeColor = color;
            _p.fillColor = color;
            _p.lineWidth = _stroke * _s;
            _p.lineCap = LineCap.Round;
            _p.lineJoin = LineJoin.Round;

            switch (_kind)
            {
                case IconKind.Home:
                    Line(3, 11, 12, 3.5f, 21, 11);
                    Line(5.5f, 9.5f, 5.5f, 20, 18.5f, 20, 18.5f, 9.5f);
                    Line(10, 20, 10, 14, 14, 14, 14, 20);
                    break;
                case IconKind.Path:
                    Circle(6, 6, 2.5f);
                    Circle(18, 18, 2.5f);
                    _p.BeginPath();
                    _p.MoveTo(P(8.5f, 6));
                    _p.BezierCurveTo(P(20, 6), P(4, 18), P(15.5f, 18));
                    _p.Stroke();
                    break;
                case IconKind.Diagram:
                    Circle(12, 12, 9);
                    _p.BeginPath();
                    _p.Arc(P(12, 7.5f), 4.5f * _s, Angle.Degrees(-90f), Angle.Degrees(90f));
                    _p.Arc(P(12, 16.5f), 4.5f * _s, Angle.Degrees(-90f), Angle.Degrees(90f), ArcDirection.CounterClockwise);
                    _p.Stroke();
                    Circle(12, 7.5f, 1.1f, true);
                    Circle(12, 16.5f, 1.1f, true);
                    break;
                case IconKind.Review:
                    ArcStroke(12, 12, 8, -160f, 110f);
                    Line(3.5f, 5.5f, 4.6f, 9.4f, 8.5f, 8.4f);
                    Line(12, 8, 12, 12, 15, 14);
                    break;
                case IconKind.Library:
                    Closed(4, 4, 8, 4, 8, 20, 4, 20);
                    Closed(10, 4, 14, 4, 14, 20, 10, 20);
                    Closed(15.5f, 5, 19.2f, 4, 21.5f, 19, 17.8f, 20);
                    break;
                case IconKind.Search:
                    Circle(10.5f, 10.5f, 6.5f);
                    Line(15.5f, 15.5f, 20.5f, 20.5f);
                    break;
                case IconKind.Settings:
                    Circle(12, 12, 3);
                    for (var i = 0; i < 8; i++)
                    {
                        var a = i * Mathf.PI / 4f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Line(12 + d.x * 6.5f, 12 + d.y * 6.5f, 12 + d.x * 9, 12 + d.y * 9);
                    }
                    Circle(12, 12, 6.5f);
                    break;
                case IconKind.Back:
                    Line(15, 4.5f, 7.5f, 12, 15, 19.5f);
                    break;
                case IconKind.Forward:
                    Line(9, 4.5f, 16.5f, 12, 9, 19.5f);
                    break;
                case IconKind.Close:
                    Line(6, 6, 18, 18);
                    Line(18, 6, 6, 18);
                    break;
                case IconKind.Lock:
                    Closed(5, 11, 19, 11, 19, 20.5f, 5, 20.5f);
                    _p.BeginPath();
                    _p.MoveTo(P(8, 11));
                    _p.LineTo(P(8, 8));
                    _p.Arc(P(12, 8), 4 * _s, Angle.Degrees(180f), Angle.Degrees(360f));
                    _p.LineTo(P(16, 11));
                    _p.Stroke();
                    break;
                case IconKind.Check:
                    Line(4.5f, 12.5f, 9.5f, 17.5f, 19.5f, 6.5f);
                    break;
                case IconKind.Star:
                case IconKind.StarFilled:
                    _p.BeginPath();
                    for (var i = 0; i < 10; i++)
                    {
                        var a = -Mathf.PI / 2f + i * Mathf.PI / 5f;
                        var r = i % 2 == 0 ? 9f : 4f;
                        var point = P(12 + Mathf.Cos(a) * r, 12.8f + Mathf.Sin(a) * r);
                        if (i == 0)
                            _p.MoveTo(point);
                        else
                            _p.LineTo(point);
                    }
                    _p.ClosePath();
                    if (_kind == IconKind.StarFilled)
                        _p.Fill();
                    _p.Stroke();
                    break;
                case IconKind.Trophy:
                    _p.BeginPath();
                    _p.MoveTo(P(7, 4));
                    _p.LineTo(P(17, 4));
                    _p.LineTo(P(17, 9));
                    _p.BezierCurveTo(P(17, 12.5f), P(14.5f, 14.5f), P(12, 14.5f));
                    _p.BezierCurveTo(P(9.5f, 14.5f), P(7, 12.5f), P(7, 9));
                    _p.ClosePath();
                    _p.Stroke();
                    ArcStroke(7, 7.5f, 3, 90f, 270f);
                    ArcStroke(17, 7.5f, 3, -90f, 90f);
                    Line(12, 14.5f, 12, 20);
                    Line(8, 20.5f, 16, 20.5f);
                    break;
                case IconKind.Bolt:
                    _p.BeginPath();
                    _p.MoveTo(P(13.5f, 2.5f));
                    _p.LineTo(P(5, 13.5f));
                    _p.LineTo(P(11.5f, 13.5f));
                    _p.LineTo(P(10.5f, 21.5f));
                    _p.LineTo(P(19, 10));
                    _p.LineTo(P(12.5f, 10));
                    _p.ClosePath();
                    _p.Fill();
                    _p.Stroke();
                    break;
                case IconKind.Target:
                    Circle(12, 12, 9);
                    Circle(12, 12, 5.5f);
                    Circle(12, 12, 2, true);
                    break;
                case IconKind.Shield:
                    _p.BeginPath();
                    _p.MoveTo(P(12, 3));
                    _p.LineTo(P(20, 6));
                    _p.LineTo(P(20, 11.5f));
                    _p.BezierCurveTo(P(20, 16.5f), P(16.5f, 19.5f), P(12, 21.5f));
                    _p.BezierCurveTo(P(7.5f, 19.5f), P(4, 16.5f), P(4, 11.5f));
                    _p.LineTo(P(4, 6));
                    _p.ClosePath();
                    _p.Stroke();
                    Line(8.5f, 12, 11, 14.5f, 15.5f, 9.5f);
                    break;
                case IconKind.Medal:
                    Line(8.5f, 9.5f, 6, 3, 9.5f, 3, 12, 8.5f);
                    Line(15.5f, 9.5f, 18, 3, 14.5f, 3, 12, 8.5f);
                    Circle(12, 14.5f, 6);
                    Circle(12, 14.5f, 2.5f, true);
                    break;
                case IconKind.Play:
                    _p.BeginPath();
                    _p.MoveTo(P(8, 5));
                    _p.LineTo(P(19, 12));
                    _p.LineTo(P(8, 19));
                    _p.ClosePath();
                    _p.Fill();
                    break;
                case IconKind.Plus:
                    Line(12, 5, 12, 19);
                    Line(5, 12, 19, 12);
                    break;
                case IconKind.Minus:
                    Line(5, 12, 19, 12);
                    break;
                case IconKind.Bookmark:
                    Closed(6.5f, 3.5f, 17.5f, 3.5f, 17.5f, 20.5f, 12, 16, 6.5f, 20.5f);
                    break;
                case IconKind.Note:
                    Closed(5, 3.5f, 15, 3.5f, 19, 7.5f, 19, 20.5f, 5, 20.5f);
                    Line(8.5f, 11, 15.5f, 11);
                    Line(8.5f, 15, 15.5f, 15);
                    break;
                case IconKind.Book:
                    _p.BeginPath();
                    _p.MoveTo(P(12, 6.5f));
                    _p.BezierCurveTo(P(9, 4.5f), P(5.5f, 4.5f), P(3, 5.5f));
                    _p.LineTo(P(3, 19));
                    _p.BezierCurveTo(P(5.5f, 18), P(9, 18), P(12, 20));
                    _p.BezierCurveTo(P(15, 18), P(18.5f, 18), P(21, 19));
                    _p.LineTo(P(21, 5.5f));
                    _p.BezierCurveTo(P(18.5f, 4.5f), P(15, 4.5f), P(12, 6.5f));
                    _p.LineTo(P(12, 20));
                    _p.Stroke();
                    break;
                case IconKind.Trash:
                    Line(4, 6.5f, 20, 6.5f);
                    Line(9, 6.5f, 9.5f, 3.5f, 14.5f, 3.5f, 15, 6.5f);
                    Line(6, 6.5f, 7, 20.5f, 17, 20.5f, 18, 6.5f);
                    break;
                case IconKind.Flame:
                    _p.BeginPath();
                    _p.MoveTo(P(12, 21));
                    _p.BezierCurveTo(P(6, 21), P(4.5f, 15.5f), P(7.5f, 11.5f));
                    _p.BezierCurveTo(P(8.5f, 13.5f), P(10, 13.5f), P(10, 13.5f));
                    _p.BezierCurveTo(P(9, 9), P(11.5f, 5), P(14, 3));
                    _p.BezierCurveTo(P(14, 7), P(19.5f, 10), P(19, 15));
                    _p.BezierCurveTo(P(18.7f, 18.5f), P(16, 21), P(12, 21));
                    _p.Stroke();
                    break;
                case IconKind.Clock:
                    Circle(12, 12, 9);
                    Line(12, 6.5f, 12, 12, 16, 14);
                    break;
                case IconKind.Crown:
                    Closed(3.5f, 18, 3, 7.5f, 8.5f, 12, 12, 5, 15.5f, 12, 21, 7.5f, 20.5f, 18);
                    Line(4, 20.5f, 20, 20.5f);
                    break;
                case IconKind.Link:
                    ArcStroke(8, 12, 4.5f, 90f, 270f);
                    ArcStroke(16, 12, 4.5f, -90f, 90f);
                    Line(8, 7.5f, 11, 7.5f);
                    Line(8, 16.5f, 11, 16.5f);
                    Line(13, 7.5f, 16, 7.5f);
                    Line(13, 16.5f, 16, 16.5f);
                    Line(8.5f, 12, 15.5f, 12);
                    break;
                case IconKind.Info:
                    Circle(12, 12, 9);
                    Line(12, 11, 12, 16.5f);
                    Circle(12, 7.5f, 0.9f, true);
                    break;
                case IconKind.Upload:
                    Line(12, 15, 12, 4);
                    Line(7.5f, 8.5f, 12, 4, 16.5f, 8.5f);
                    Line(4.5f, 14.5f, 4.5f, 19.5f, 19.5f, 19.5f, 19.5f, 14.5f);
                    break;
            }
        }
    }
}
