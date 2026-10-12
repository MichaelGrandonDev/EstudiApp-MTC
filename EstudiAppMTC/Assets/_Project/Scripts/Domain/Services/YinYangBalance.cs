namespace EstudiApp.Domain
{
    /// <summary>
    /// Lectura de la balanza Yin-Yang (Li Ping, cap. 1): con 1 = nivel normal, cada lado puede estar en exceso
    /// (plenitud) o en vacío (insuficiencia). El resultado es el id de un <see cref="BalanceState"/>.
    /// </summary>
    public static class YinYangBalance
    {
        public const float Normal = 1f;
        public const float Tolerance = 0.2f;

        public static string Classify(float yin, float yang)
        {
            var yinHigh = yin > Normal + Tolerance;
            var yinLow = yin < Normal - Tolerance;
            var yangHigh = yang > Normal + Tolerance;
            var yangLow = yang < Normal - Tolerance;

            if (yinLow && yangLow)
                return "vacio_ambos";
            if (yangHigh && yinLow)
                return "exceso_yang_dana_yin";
            if (yinHigh && yangLow)
                return "exceso_yin_dana_yang";
            if (yangHigh && yinHigh)
                return "plenitud_ambos";
            if (yangHigh)
                return "exceso_yang";
            if (yinHigh)
                return "exceso_yin";
            if (yinLow)
                return "vacio_yin";
            if (yangLow)
                return "vacio_yang";
            return "equilibrio";
        }
    }
}
