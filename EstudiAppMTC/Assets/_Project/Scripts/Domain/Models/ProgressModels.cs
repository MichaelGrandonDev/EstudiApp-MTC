using System;
using System.Collections.Generic;

namespace EstudiApp.Domain
{
    /// <summary>Todo lo que el estudiante hizo; se guarda como JSON en el dispositivo.</summary>
    public class ProgressState
    {
        /// <summary>2: desafíos, experiencia y racha de aciertos (los archivos de la versión 1 se migran al cargarlos).</summary>
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        public Dictionary<string, LessonProgress> Lessons = new Dictionary<string, LessonProgress>();
        public Dictionary<string, ReviewCard> Cards = new Dictionary<string, ReviewCard>();
        public Dictionary<string, string> Notes = new Dictionary<string, string>();
        /// <summary>Días con estudio, "yyyy-MM-dd".</summary>
        public List<string> StudyDays = new List<string>();
        public string LastLessonId;
        /// <summary>Modo exploración: todo desbloqueado sin seguir la ruta.</summary>
        public bool FreeMode;
        /// <summary>Desafíos de unidad, exámenes de eje y mini tests, por id de desafío.</summary>
        public Dictionary<string, ChallengeProgress> Challenges = new Dictionary<string, ChallengeProgress>();
        /// <summary>Puntos de experiencia acumulados.</summary>
        public int Xp;
        /// <summary>Mejor racha de aciertos seguidos en una evaluación.</summary>
        public int BestCombo;
    }

    public class ChallengeProgress
    {
        public float BestScore;
        public int Attempts;
        public bool Passed;
        public DateTime LastPlayed;
    }

    public class LessonProgress
    {
        public bool Read;
        public bool Completed;
        public float BestScore;
        public int Attempts;
        public DateTime LastStudied;
    }

    /// <summary>Tarjeta de repaso espaciado (sistema Leitner) a partir de una pregunta de evaluación.</summary>
    public class ReviewCard
    {
        public string QuestionId;
        public string LessonId;
        /// <summary>1 a 5: cuanto más alta, más espaciado el repaso.</summary>
        public int Box = 1;
        public DateTime Due;
        public int Reviews;
        public int Lapses;
    }

    public enum LessonStatus
    {
        Locked,
        PremiumLocked,
        Available,
        Read,
        Completed
    }

    public class QuizResult
    {
        public int Correct;
        public int Total;
        public float Score => Total == 0 ? 1f : (float)Correct / Total;
        public bool Passed;
        public bool FirstPass;
        public List<string> UnlockedLessons = new List<string>();
        public List<string> UnlockedAxes = new List<string>();
        /// <summary>Estrellas (0–3) del desafío tras este intento, y las que tenía antes.</summary>
        public int Stars;
        public int PreviousStars;
        public int XpGained;
        public int LevelBefore = 1;
        public int LevelAfter = 1;
        public int BestCombo;
        /// <summary>Se aprobó por primera vez el examen de un eje: se gana su sello.</summary>
        public bool BadgeEarned;
        /// <summary>Lecciones con fallos, de más a menos fallos (para sugerir qué repasar).</summary>
        public List<string> WeakLessons = new List<string>();
    }

    /// <summary>Nivel del estudiante según su experiencia.</summary>
    public struct LevelInfo
    {
        public int Level;
        public string Title;
        public int Xp;
        /// <summary>Experiencia con la que empieza este nivel y con la que empieza el siguiente.</summary>
        public int Floor;
        public int Next;

        public float Fraction => Next <= Floor ? 1f : Math.Min(1f, Math.Max(0f, (Xp - Floor) / (float)(Next - Floor)));
    }
}
