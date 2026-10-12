using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI
{
    /// <summary>
    /// Animaciones de la interfaz: casi todas cambian una clase y dejan que las transiciones de Theme.uss
    /// (bloque «Animaciones») hagan el resto; los contadores y sacudidas se programan con <c>schedule</c>.
    /// </summary>
    public static class Anim
    {
        /// <summary>Aparece con un fundido y subiendo un poco, tras <paramref name="delayMs"/>.</summary>
        public static T Enter<T>(this T element, long delayMs = 0) where T : VisualElement
        {
            element.AddToClassList("anim");
            element.AddToClassList("anim--in");
            element.schedule.Execute(() => element.RemoveFromClassList("anim--in")).StartingIn(30 + delayMs);
            return element;
        }

        /// <summary>Crece desde cero con un pequeño rebote (estrellas, sellos).</summary>
        public static T PopIn<T>(this T element, long delayMs = 0) where T : VisualElement
        {
            element.AddToClassList("anim");
            element.AddToClassList("anim--zero");
            element.schedule.Execute(() => element.RemoveFromClassList("anim--zero")).StartingIn(30 + delayMs);
            return element;
        }

        /// <summary>Un latido: crece y vuelve.</summary>
        public static void Pop(VisualElement element)
        {
            element.AddToClassList("anim");
            element.AddToClassList("anim--pop");
            element.schedule.Execute(() => element.RemoveFromClassList("anim--pop")).StartingIn(170);
        }

        /// <summary>Sacudida horizontal (respuesta incorrecta).</summary>
        public static void Shake(VisualElement element)
        {
            float[] offsets = { -9f, 8f, -6f, 5f, -3f, 2f, 0f };
            var step = 0;
            element.schedule.Execute(() =>
            {
                element.style.translate = new Translate(offsets[Mathf.Min(step, offsets.Length - 1)], 0);
                step++;
            }).Every(45).Until(() => step >= offsets.Length);
        }

        /// <summary>Cuenta de <paramref name="from"/> a <paramref name="to"/> en <paramref name="ms"/> milisegundos.</summary>
        public static void CountUp(Label label, int from, int to, long ms, Func<int, string> format, long delayMs = 0)
        {
            label.text = format(from);
            long start = -1;
            var finished = false;
            label.schedule.Execute(t =>
            {
                if (start < 0)
                    start = t.now;
                var k = ms <= 0 ? 1f : Mathf.Clamp01((t.now - start) / (float)ms);
                var eased = 1f - Mathf.Pow(1f - k, 3f);
                label.text = format(Mathf.RoundToInt(Mathf.Lerp(from, to, eased)));
                finished = k >= 1f;
            }).StartingIn(delayMs).Every(30).Until(() => finished);
        }

        /// <summary>Barra que se llena desde cero hasta <paramref name="value"/> (0–1).</summary>
        public static VisualElement Grow(this VisualElement bar, float value, long delayMs = 120)
        {
            var fill = bar.Q(className: "bar__fill");
            if (fill == null)
                return bar;
            fill.style.width = Length.Percent(0f);
            fill.schedule.Execute(() => fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f)).StartingIn(delayMs);
            return bar;
        }

        /// <summary>Texto que sube y se desvanece sobre <paramref name="parent"/> (por ejemplo «+5 XP»).</summary>
        public static void FloatUp(VisualElement parent, string text, params string[] classes)
        {
            var label = parent.Append(El.Text(text, "float-text"));
            foreach (var c in classes)
                label.AddToClassList(c);
            label.pickingMode = PickingMode.Ignore;
            label.schedule.Execute(() => label.AddToClassList("float-text--up")).StartingIn(30);
            label.schedule.Execute(() => label.RemoveFromHierarchy()).StartingIn(1300);
        }

        /// <summary>Alterna una clase cada <paramref name="periodMs"/> (latidos continuos, halos).</summary>
        public static void Pulse(VisualElement element, string className, long periodMs = 900)
        {
            element.schedule.Execute(() => element.ToggleInClassList(className)).StartingIn(200).Every(periodMs);
        }
    }
}
