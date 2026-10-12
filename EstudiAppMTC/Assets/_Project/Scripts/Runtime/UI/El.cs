using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI
{
    /// <summary>Atajos para construir la interfaz desde código.</summary>
    public static class El
    {
        public static VisualElement Div(params string[] classes) => With(new VisualElement(), classes);

        public static Label Text(string text, params string[] classes) => With(new Label(text), classes);

        public static Button Btn(string text, Action onClick, params string[] classes) =>
            With(new Button(onClick) { text = text }, classes);

        public static Button IconBtn(IconKind icon, string label, Action onClick, params string[] classes)
        {
            var button = new Button(onClick);
            button.AddToClassList("icon-btn");
            button.Add(new Icon(icon));
            if (!string.IsNullOrEmpty(label))
            {
                button.AddToClassList("icon-btn--label");
                button.Append(Text(label, "icon-btn__label")).pickingMode = PickingMode.Ignore;
            }
            return With(button, classes);
        }

        public static ScrollView VScroll(params string[] classes) =>
            With(new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden }, classes);

        public static VisualElement Row(params string[] classes) => With(Div(classes), "row");

        public static VisualElement Spacer() => Div("spacer");

        public static T With<T>(T element, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes)
                if (!string.IsNullOrEmpty(c))
                    element.AddToClassList(c);
            return element;
        }

        public static T Append<T>(this VisualElement parent, T child) where T : VisualElement
        {
            parent.Add(child);
            return child;
        }

        public static void SetVisible(this VisualElement element, bool visible) =>
            element.EnableInClassList("hidden", !visible);

        public static T Tint<T>(this T element, Color color) where T : VisualElement
        {
            element.style.backgroundColor = color;
            return element;
        }

        /// <summary>Color de texto o icono sobre las superficies del tema (en el oscuro se aclara si hace falta).</summary>
        public static T Ink<T>(this T element, Color color) where T : VisualElement
        {
            element.style.color = Theme.Readable(color);
            return element;
        }

        /// <summary>Barra de progreso horizontal (0–1).</summary>
        public static VisualElement Bar(float value, Color color, params string[] classes)
        {
            var bar = Div(classes);
            bar.AddToClassList("bar");
            var fill = bar.Append(Div("bar__fill"));
            fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
            fill.style.backgroundColor = color;
            return bar;
        }

        /// <summary>Chip pulsable; con <paramref name="color"/> se usa como punto de color.</summary>
        public static Button Chip(string text, Action onClick, bool active, Color? color = null)
        {
            var chip = Btn(string.Empty, onClick, "chip");
            chip.EnableInClassList("chip--on", active);
            if (color.HasValue)
                chip.Append(Div("chip__dot")).Tint(color.Value);
            chip.Append(Text(text, "chip__label")).pickingMode = PickingMode.Ignore;
            return chip;
        }

        public static Color Hex(string hex, Color? fallback = null) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var color) ? color : fallback ?? Theme.Jade;

        public static Color Alpha(this Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        public static Color Mix(this Color color, Color other, float t) => Color.Lerp(color, other, t);
    }
}
