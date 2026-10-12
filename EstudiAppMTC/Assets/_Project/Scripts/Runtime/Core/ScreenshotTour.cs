using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EstudiApp.Domain;
using EstudiApp.UI;
using EstudiApp.UI.Pages;
using UnityEngine;
using ScrollView = UnityEngine.UIElements.ScrollView;
using UIDocument = UnityEngine.UIElements.UIDocument;

namespace EstudiApp.Core
{
    /// <summary>
    /// Recorrido de verificación: con <c>-estudiapp-shots &lt;carpeta&gt;</c> recorre las pantallas principales, guarda
    /// una captura de cada una y cierra la app. Con <c>-estudiapp-pdf &lt;ruta&gt;</c> además importa ese PDF, lo abre
    /// en el lector desde una cita y lo quita de la biblioteca al terminar. El progreso se simula en memoria: el del
    /// estudiante no se modifica.
    /// </summary>
    public static class ScreenshotTour
    {
        public static string Folder => Argument("-estudiapp-shots");

        public static IEnumerator Run(AppShell shell, AppServices services)
        {
            var folder = Folder;
            Directory.CreateDirectory(folder);
            var curriculum = services.Curriculum;
            var index = 0;

            IEnumerator Shot(string name)
            {
                for (var i = 0; i < 20; i++)
                    yield return null;
                yield return new WaitForEndOfFrame();
                Debug.Log($"[EstudiApp] Captura {index + 1:00}-{name}: {shell.Current?.GetType().Name} «{shell.Current?.Title}»");
                var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(folder, $"{++index:00}-{name}.png"), texture.EncodeToPNG());
                UnityEngine.Object.Destroy(texture);
                yield return null;
            }

            // El recorrido simula progreso: nada se guarda y se parte de cero (las notas y la biblioteca no se tocan).
            services.SavingSuspended = true;
            services.Progress.Reset();
            services.Progress.SetFreeMode(false);

            yield return Shot("bienvenida");
            shell.CloseOverlay();
            yield return Shot("inicio");
            shell.SwitchTab(Tab.Path);
            yield return Shot("ruta");
            var axis = curriculum.Axes.First();
            shell.Push(new AxisPage(axis));
            yield return Shot("eje");
            shell.OpenLesson(axis.Lessons.First());
            yield return Shot("leccion");

            foreach (var step in ChallengeShots(shell, services, axis, Shot))
                yield return step;

            shell.SwitchTab(Tab.Diagrams);
            yield return Shot("diagramas");
            foreach (var kind in new[] { "graph", "ring", "balance", "tongue" })
            {
                var spec = curriculum.Diagrams.FirstOrDefault(d => d.Kind == kind);
                if (spec == null)
                    continue;
                shell.Push(new DiagramPage(spec));
                yield return Shot("diagrama-" + spec.Id);
            }

            shell.SwitchTab(Tab.Review);
            yield return Shot("repaso");
            shell.SwitchTab(Tab.Library);
            yield return Shot("biblioteca");
            shell.Push(new SearchPage());
            yield return Shot("buscar");
            shell.Push(new SettingsPage());
            yield return Shot("ajustes");

            var pdf = Argument("-estudiapp-pdf");
            if (!string.IsNullOrEmpty(pdf))
            {
                var task = services.Library.Import(pdf, Path.GetFileName(pdf), false);
                while (!task.IsCompleted)
                    yield return null;
                var result = task.Result;
                if (result.Error != null)
                {
                    Debug.LogError("[EstudiApp] Importación de prueba: " + result.Error);
                }
                else
                {
                    var book = result.Book;
                    services.Library.Add(book);
                    Debug.Log($"[EstudiApp] Importado «{book.Title}» ({book.Pages} págs.) → {book.BibliographyId ?? "sin enlazar"}");
                    var reference = curriculum.AllLessons.SelectMany(l => l.References)
                        .FirstOrDefault(r => r.Book == book.BibliographyId && r.Pdf > 0);
                    shell.SwitchTab(Tab.Library);
                    yield return Shot("biblioteca-con-libro");
                    shell.Push(new ReaderPage(book, reference != null ? LibraryService.PageFor(reference, book) : 1, reference));
                    yield return new WaitForSeconds(2f);
                    yield return Shot("lector");
                    shell.Back();
                    services.Library.Remove(book);
                }
            }

            Screen.SetResolution(400, 820, FullScreenMode.Windowed);
            yield return new WaitForSeconds(1f);
            shell.SwitchTab(Tab.Home);
            yield return new WaitForSeconds(1f);
            yield return Shot("movil-inicio");
            shell.OpenLesson(axis.Lessons.First());
            yield return Shot("movil-leccion");
            shell.Open(Tab.Path, new AxisPage(curriculum.Axes[1]));
            yield return new WaitForSeconds(1f);
            yield return Shot("movil-mapa");
            shell.Push(new ChallengeTestPage(services.Challenges.Practice(curriculum.Axes[1]), true, 11));
            yield return new WaitForSeconds(1f);
            yield return Shot("movil-minitest");
            shell.SwitchTab(Tab.Diagrams);
            var ring = curriculum.Diagrams.FirstOrDefault(d => d.Kind == "ring");
            if (ring != null)
            {
                shell.Push(new DiagramPage(ring));
                yield return Shot("movil-diagrama");
            }

            Screen.SetResolution(1280, 800, FullScreenMode.Windowed);
            yield return new WaitForSeconds(1f);
            Debug.Log("[EstudiApp] Capturas guardadas en " + folder);
            Application.Quit();
        }

        /// <summary>
        /// Desafíos: progreso simulado en el primer eje, mapa, mini test con racha y emparejar, resultado, y el examen
        /// del eje con su sello. Deja abierto el segundo eje con algo estudiado (para las capturas de móvil).
        /// </summary>
        private static IEnumerable ChallengeShots(AppShell shell, AppServices services, Axis axis, Func<string, IEnumerator> shot)
        {
            var progress = services.Progress;
            var challenges = services.Challenges;
            var curriculum = services.Curriculum;
            var lessons = axis.Lessons.ToList();

            void Quiz(Lesson lesson, int wrong)
            {
                progress.MarkOpened(lesson.Id);
                progress.RecordQuiz(lesson.Id, lesson.Quiz.Select((q, i) => (q, i)).ToDictionary(x => x.q.Id, x => x.i >= wrong), lesson.Quiz.Count - wrong);
            }

            void Pass(Challenge challenge, int wrong)
            {
                var items = challenges.BuildTest(challenge, new System.Random(5));
                challenges.Record(challenge, items.Select((item, i) => new TestAnswer { Item = item, Correct = i >= wrong }).ToList(), items.Count - wrong);
            }

            // Primera unidad y la del Taiji superadas (con notas distintas), la siguiente a medias.
            var taiji = axis.Units.FirstOrDefault(u => ChallengeService.HasBoss(u));
            var plan = new[] { 0, 0, 1, 1 };
            for (var i = 0; i < Math.Min(plan.Length, lessons.Count); i++)
                Quiz(lessons[i], plan[i]);
            if (taiji != null)
                Pass(challenges.ForUnit(axis, taiji), 1);
            if (lessons.Count > 5)
            {
                Quiz(lessons[4], 0);
                progress.MarkOpened(lessons[5].Id);
                progress.MarkRead(lessons[5].Id);
            }

            shell.SwitchTab(Tab.Home);
            yield return new WaitForSeconds(1.2f);
            yield return shot("desafios-inicio");
            shell.SwitchTab(Tab.Path);
            shell.SwitchTab(Tab.Path, true);
            yield return new WaitForSeconds(1f);
            yield return shot("desafios-ruta");
            var root = shell.GetComponent<UIDocument>().rootVisualElement;
            shell.Push(new AxisPage(axis));
            for (var i = 0; i < 6; i++)
            {
                var sv = UnityEngine.UIElements.UQueryExtensions.Q<ScrollView>(root, className: "page-scroll");
                Debug.Log($"[EstudiApp] Mapa t{i}: scroll={sv?.scrollOffset} foco={root.focusController?.focusedElement}");
                yield return new WaitForSeconds(0.3f);
            }
            yield return shot("desafios-mapa");
            var scroll = UnityEngine.UIElements.UQueryExtensions.Q<ScrollView>(root, className: "page-scroll");
            if (scroll != null)
            {
                scroll.scrollOffset = new Vector2(0, 560);
                yield return new WaitForSeconds(0.6f);
                yield return shot("desafios-mapa-tramos");
            }

            var practice = challenges.Practice(axis);
            shell.OpenChallenge(practice);
            yield return new WaitForSeconds(1f);
            yield return shot("minitest-portada");

            var run = new ChallengeTestPage(practice, true, 3);
            shell.Replace(run);
            yield return new WaitForSeconds(0.8f);
            yield return shot("minitest-pregunta");

            // Tres aciertos seguidos (racha), luego el ejercicio de emparejar, y un fallo para sugerir qué repasar.
            var answered = 0;
            var shotMatch = false;
            var missed = false;
            while (run.CurrentItem != null && shell.Current == run)
            {
                var item = run.CurrentItem;
                if (item.IsMatch)
                {
                    if (!shotMatch)
                    {
                        shotMatch = true;
                        yield return new WaitForSeconds(0.6f);
                        yield return shot("minitest-emparejar");
                    }
                    run.CurrentMatch.Solve(true);
                }
                else
                {
                    var right = answered < 3 || missed;
                    missed |= !right;
                    var question = item.Question;
                    run.CurrentQuestion.Submit(right ? question.Answer : new List<int> { Enumerable.Range(0, question.Options.Count).First(o => !question.Answer.Contains(o)) });
                }
                answered++;
                if (answered == 3)
                {
                    yield return new WaitForSeconds(0.35f);
                    yield return shot("minitest-racha");
                }
                yield return null;
                run.Advance();
                yield return new WaitForSeconds(0.3f);
            }
            yield return new WaitForSeconds(2.6f);
            yield return shot("minitest-resultado");

            // Todo el eje dominado y el examen aprobado: resultado con el sello y mapa completo.
            foreach (var lesson in lessons.Where(l => !progress.IsCompleted(l.Id)))
                Quiz(lesson, lesson.Quiz.Count >= 5 ? 1 : 0);
            foreach (var unit in axis.Units.Where(ChallengeService.HasBoss))
                if (!challenges.IsPassed(challenges.ForUnit(axis, unit)))
                    Pass(challenges.ForUnit(axis, unit), 0);
            var exam = new ChallengeTestPage(challenges.ForAxis(axis), false, 8);
            shell.Open(Tab.Path, new AxisPage(axis));
            shell.Push(exam);
            yield return new WaitForSeconds(0.5f);
            var index = 0;
            while (exam.CurrentItem != null && shell.Current == exam)
            {
                var question = exam.CurrentItem.Question;
                exam.CurrentQuestion.Submit(index++ != 4 ? question.Answer : new List<int> { Enumerable.Range(0, question.Options.Count).First(o => !question.Answer.Contains(o)) });
                yield return null;
                exam.Advance();
                yield return null;
            }
            yield return new WaitForSeconds(3.2f);
            yield return shot("examen-sello");
            shell.SwitchTab(Tab.Home);
            yield return new WaitForSeconds(1.5f);
            yield return shot("inicio-con-sello");

            // Para el móvil: el segundo eje ya abierto, con un par de lecciones estudiadas.
            var second = curriculum.Axes.Count > 1 ? curriculum.Axes[1] : null;
            if (second != null)
                foreach (var (lesson, i) in second.Lessons.Take(2).Select((l, i) => (l, i)))
                    Quiz(lesson, i);
        }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
