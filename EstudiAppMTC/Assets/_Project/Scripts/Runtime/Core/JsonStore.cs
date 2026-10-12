using System;
using System.IO;
using EstudiApp.Domain;
using UnityEngine;

namespace EstudiApp.Core
{
    /// <summary>Guarda un objeto como JSON en la carpeta de datos de la app (escritura atómica).</summary>
    public class JsonStore<T> where T : new()
    {
        private readonly string _path;

        public JsonStore(string fileName)
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
        }

        public T Load()
        {
            try
            {
                return File.Exists(_path) ? ContentParser.Read<T>(File.ReadAllText(_path)) : new T();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EstudiApp] No se pudo leer {_path}: {e.Message}");
                return new T();
            }
        }

        public void Save(T value)
        {
            try
            {
                var temp = _path + ".tmp";
                File.WriteAllText(temp, ContentParser.Write(value));
                if (File.Exists(_path))
                    File.Delete(_path);
                File.Move(temp, _path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EstudiApp] No se pudo guardar {_path}: {e.Message}");
            }
        }
    }
}
