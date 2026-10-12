using System;

namespace EstudiApp.Domain
{
    /// <summary>Estrellas, experiencia y niveles: las recompensas de la ruta de desafíos.</summary>
    public static class Rewards
    {
        /// <summary>Por cada respuesta correcta en cualquier evaluación.</summary>
        public const int XpPerCorrect = 5;
        /// <summary>Extra por respuesta correcta si se juega con reloj.</summary>
        public const int XpTimedBonus = 2;
        /// <summary>Por cada estrella nueva que se consigue en un desafío.</summary>
        public const int XpPerStar = 15;
        /// <summary>Al dominar una lección por primera vez.</summary>
        public const int XpLessonPass = 30;
        /// <summary>Al superar por primera vez el desafío de una unidad.</summary>
        public const int XpUnitPass = 80;
        /// <summary>Al aprobar por primera vez el examen de un eje (y ganar su sello).</summary>
        public const int XpAxisPass = 200;
        /// <summary>Evaluación sin fallos.</summary>
        public const int XpPerfect = 20;
        /// <summary>Por cada acierto seguido a partir de <see cref="ComboStart"/>.</summary>
        public const int XpComboStep = 3;
        public const int ComboStart = 3;
        /// <summary>Por cada tarjeta acertada en el repaso espaciado.</summary>
        public const int XpReview = 2;

        public const float TwoStars = 0.85f;

        private static readonly string[] Titles =
            { "Semilla", "Brote", "Aprendiz", "Estudiante", "Practicante", "Terapeuta", "Erudito", "Maestro" };

        /// <summary>1 estrella al aprobar (70 %), 2 desde el 85 % y 3 con todo correcto.</summary>
        public static int Stars(float score)
        {
            if (score >= 1f - 1e-4f)
                return 3;
            if (score >= TwoStars - 1e-4f)
                return 2;
            return score >= ProgressService.PassScore - 1e-4f ? 1 : 0;
        }

        public static int ComboBonus(int combo) => combo >= ComboStart ? (combo - ComboStart + 1) * XpComboStep : 0;

        /// <summary>Experiencia necesaria para llegar a un nivel: 0, 100, 300, 600, 1000…</summary>
        public static int XpForLevel(int level) => 50 * level * (level - 1);

        public static int LevelOf(int xp)
        {
            var level = 1;
            while (xp >= XpForLevel(level + 1))
                level++;
            return level;
        }

        public static string Title(int level)
        {
            var index = Math.Max(1, level) - 1;
            return index < Titles.Length ? Titles[index] : $"{Titles[Titles.Length - 1]} {index - Titles.Length + 2}";
        }

        public static LevelInfo Level(int xp)
        {
            var level = LevelOf(Math.Max(0, xp));
            return new LevelInfo
            {
                Level = level,
                Title = Title(level),
                Xp = xp,
                Floor = XpForLevel(level),
                Next = XpForLevel(level + 1)
            };
        }
    }
}
