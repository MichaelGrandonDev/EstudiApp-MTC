using System;
using System.IO;
using EstudiApp.Domain;
using Xunit;

namespace EstudiApp.Tests
{
    public class MatcherAndBalanceTests
    {
        private static readonly Lazy<Curriculum> Loaded = new Lazy<Curriculum>(() =>
            ContentParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "curriculum.json"))));

        [Theory]
        [InlineData("Los Fundamentos de la Medicina China.pdf", 554, "maciocia")]
        [InlineData("El Gran Libro de la Medicina China.pdf", 385, "liping")]
        [InlineData("SU WEN.pdf", 429, "suwen")]
        [InlineData("Atlas de Acupuntura.pdf", 718, "focks")]
        [InlineData("La prac mtc macciosa.pdf", 1517, "practica")]
        [InlineData("DIAG.LENGUA.pdf", 238, "lengua")]
        public void RecognisesTheBooksOfTheBibliography(string fileName, int pages, string expected)
        {
            Assert.Equal(expected, BookMatcher.Match(Loaded.Value, fileName, pages)?.Id);
        }

        [Fact]
        public void UnknownPdfsStayUnlinked()
        {
            Assert.Null(BookMatcher.Match(Loaded.Value, "Recetas de cocina.pdf", 120));
        }

        [Fact]
        public void NormalizeRemovesAccentsAndSymbols()
        {
            Assert.Equal("diagnostico por el pulso pdf", BookMatcher.Normalize("Diagnóstico  por_el-PULSO.pdf"));
            Assert.Equal("zhongyi", BookMatcher.Normalize("Zhōngyī"));
            Assert.Equal(string.Empty, BookMatcher.Normalize(null));
        }

        [Theory]
        [InlineData(1f, 1f, "equilibrio")]
        [InlineData(1f, 1.5f, "exceso_yang")]
        [InlineData(1.5f, 1f, "exceso_yin")]
        [InlineData(0.6f, 1f, "vacio_yin")]
        [InlineData(1f, 0.6f, "vacio_yang")]
        [InlineData(0.6f, 0.6f, "vacio_ambos")]
        [InlineData(0.6f, 1.5f, "exceso_yang_dana_yin")]
        [InlineData(1.5f, 0.6f, "exceso_yin_dana_yang")]
        [InlineData(1.5f, 1.5f, "plenitud_ambos")]
        [InlineData(1.15f, 0.85f, "equilibrio")]
        public void ClassifiesTheYinYangBalance(float yin, float yang, string expected)
        {
            Assert.Equal(expected, YinYangBalance.Classify(yin, yang));
        }
    }
}
