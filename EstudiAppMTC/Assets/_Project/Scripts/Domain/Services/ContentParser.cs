using System;
using Newtonsoft.Json;

namespace EstudiApp.Domain
{
    /// <summary>Lee curriculum.json (generado por Tools/content/build_content.py).</summary>
    public static class ContentParser
    {
        public const string FileName = "curriculum.json";

        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        public static Curriculum Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("El contenido está vacío.");
            return JsonConvert.DeserializeObject<Curriculum>(json, Settings) ?? new Curriculum();
        }

        public static T Read<T>(string json) where T : new()
        {
            if (string.IsNullOrWhiteSpace(json))
                return new T();
            try
            {
                var value = JsonConvert.DeserializeObject<T>(json, Settings);
                return value == null ? new T() : value;
            }
            catch (JsonException)
            {
                return new T();
            }
        }

        public static string Write<T>(T value) => JsonConvert.SerializeObject(value, Formatting.Indented, Settings);
    }
}
