using System;
using System.Collections;
using System.IO;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.Networking;

namespace EstudiApp.Core
{
    /// <summary>Lee StreamingAssets/content/curriculum.json en todas las plataformas.</summary>
    public static class ContentLoader
    {
        public const string Folder = "content";

        public static IEnumerator Load(Action<Curriculum, string> onLoaded)
        {
            var path = Path.Combine(Application.streamingAssetsPath, Folder, ContentParser.FileName);
            string text = null;
            string error = null;

            // En Android y WebGL StreamingAssets está dentro del paquete y sólo es accesible por URL.
            if (path.Contains("://"))
            {
                using (var request = UnityWebRequest.Get(path))
                {
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                        text = request.downloadHandler.text;
                    else
                        error = request.error;
                }
            }
            else if (File.Exists(path))
                text = File.ReadAllText(path);
            else
                error = "No se encontró " + path;

            Curriculum curriculum = null;
            if (text != null)
            {
                try
                {
                    curriculum = ContentParser.Parse(text);
                }
                catch (Exception e)
                {
                    error = e.Message;
                }
            }
            onLoaded(curriculum, error);
        }
    }
}
