using UnityEngine;

namespace EstudiApp.UI
{
    public enum ThemeMode
    {
        Light,
        Dark,
        System
    }

    /// <summary>
    /// Paleta (la misma que Theme.uss) para lo que se dibuja desde código. Con <see cref="Dark"/> devuelve los
    /// valores del tema oscuro (la clase <c>.theme-dark</c> de la raíz hace lo mismo con las variables de la hoja).
    /// </summary>
    public static class Theme
    {
        public static ThemeMode Mode { get; set; } = ThemeMode.Light;
        public static bool Dark { get; set; }

        public static Color Paper => Dark ? new Color32(0x1B, 0x19, 0x17, 0xFF) : new Color32(0xF6, 0xF1, 0xE7, 0xFF);
        public static Color Card => Dark ? new Color32(0x25, 0x22, 0x1F, 0xFF) : new Color32(0xFF, 0xFD, 0xF8, 0xFF);
        public static Color Ink => Dark ? new Color32(0xEC, 0xE5, 0xD8, 0xFF) : new Color32(0x2B, 0x26, 0x22, 0xFF);
        public static Color Muted => Dark ? new Color32(0xA6, 0x9C, 0x8F, 0xFF) : new Color32(0x7A, 0x70, 0x66, 0xFF);
        public static Color Line => Dark ? new Color32(0x3F, 0x39, 0x33, 0xFF) : new Color32(0xE2, 0xD8, 0xC8, 0xFF);
        public static Color Jade => Dark ? new Color32(0x2C, 0x82, 0x6E, 0xFF) : new Color32(0x2E, 0x7D, 0x6B, 0xFF);
        public static Color Cinnabar => new Color32(0xB5, 0x52, 0x3B, 0xFF);
        public static Color Gold => new Color32(0xC9, 0x9A, 0x2E, 0xFF);
        public static Color Night => Dark ? new Color32(0x32, 0x39, 0x44, 0xFF) : new Color32(0x24, 0x2A, 0x33, 0xFF);
        public static Color Ok => Dark ? new Color32(0x4E, 0xA0, 0x6A, 0xFF) : new Color32(0x3E, 0x8E, 0x5A, 0xFF);
        public static Color Bad => Dark ? new Color32(0xBA, 0x48, 0x56, 0xFF) : new Color32(0xB2, 0x3A, 0x48, 0xFF);
        public static Color Yin => Dark ? new Color32(0x7E, 0x9E, 0xC4, 0xFF) : new Color32(0x3F, 0x5A, 0x7A, 0xFF);
        public static Color Yang => Dark ? new Color32(0xE2, 0x80, 0x54, 0xFF) : new Color32(0xD4, 0x6A, 0x3A, 0xFF);

        /// <summary>Lo claro del Taiji (en los dos temas).</summary>
        public static Color YangLight => new Color(0.98f, 0.96f, 0.9f);

        public static string MutedHex => "#" + ColorUtility.ToHtmlStringRGB(Muted);

        /// <summary>
        /// Un color de contenido (eje, capa, zona) sobre las superficies del tema: en el oscuro se aclara hasta tener
        /// buen contraste (luminancia ≥ <paramref name="minLuminance"/>; 0.3 para texto, ~0.18 para trazos).
        /// </summary>
        public static Color Readable(Color color, float minLuminance = 0.3f)
        {
            if (!Dark)
                return color;
            Color.RGBToHSV(color, out var h, out var s, out var v);
            var result = color;
            for (var i = 0; i < 40 && Luminance(result) < minLuminance; i++)
            {
                if (v < 1f)
                    v = Mathf.Min(1f, v + 0.04f);
                else
                    s = Mathf.Max(0f, s - 0.05f);
                result = Color.HSVToRGB(h, s, v).Alpha(color.a);
            }
            return result;
        }

        /// <summary>Fondo suave de un color (nodos, sectores): mezclado con blanco en el claro y con la tarjeta en el oscuro.</summary>
        public static Color Wash(Color color, float amount) =>
            Dark ? color.Mix(Card, amount * 0.9f) : color.Mix(Color.white, amount);

        public static float Luminance(Color color)
        {
            var l = color.linear;
            return 0.2126f * l.r + 0.7152f * l.g + 0.0722f * l.b;
        }

        public static ThemeMode ParseMode(string id)
        {
            switch (id?.Trim().ToLowerInvariant())
            {
                case "dark":
                case "oscuro":
                    return ThemeMode.Dark;
                case "system":
                case "sistema":
                    return ThemeMode.System;
                default:
                    return ThemeMode.Light;
            }
        }

        public static string ModeId(ThemeMode mode) =>
            mode == ThemeMode.Dark ? "dark" : mode == ThemeMode.System ? "system" : "light";
    }
}
