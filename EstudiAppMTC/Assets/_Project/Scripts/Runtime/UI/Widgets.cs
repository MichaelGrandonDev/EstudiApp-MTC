using System;
using System.Collections.Generic;
using System.Linq;
using EstudiApp.Domain;
using EstudiApp.UI.Diagrams;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI
{
    /// <summary>Logo: el Taiji.</summary>
    public class TaijiMark : VisualElement
    {
        public TaijiMark()
        {
            AddToClassList("taiji-mark");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += ctx =>
            {
                var r = Mathf.Min(contentRect.width, contentRect.height) / 2f - 1f;
                if (r > 1f)
                    RingDiagram.DrawTaiji(ctx.painter2D, contentRect.center, r);
            };
        }
    }

    /// <summary>Anillo de progreso con el porcentaje en el centro.</summary>
    public class ProgressRing : VisualElement
    {
        private float _value;
        private Color _color;
        private readonly Label _label;

        public ProgressRing(float value, Color color, string caption = null)
        {
            AddToClassList("progress-ring");
            _color = color;
            _label = this.Append(El.Text(string.Empty, "progress-ring__label"));
            if (caption != null)
                this.Append(El.Text(caption, "progress-ring__caption"));
            SetValue(value);
            generateVisualContent += Draw;
        }

        public void SetValue(float value)
        {
            _value = Mathf.Clamp01(value);
            _label.text = Mathf.RoundToInt(_value * 100f) + "%";
            MarkDirtyRepaint();
        }

        /// <summary>Llena el anillo desde cero hasta <paramref name="value"/>.</summary>
        public ProgressRing AnimateTo(float value, long ms = 900, long delayMs = 150)
        {
            var target = Mathf.Clamp01(value);
            SetValue(0f);
            long start = -1;
            var finished = false;
            schedule.Execute(t =>
            {
                if (start < 0)
                    start = t.now;
                var k = Mathf.Clamp01((t.now - start) / (float)ms);
                SetValue(target * (1f - Mathf.Pow(1f - k, 3f)));
                finished = k >= 1f;
            }).StartingIn(delayMs).Every(16).Until(() => finished);
            return this;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            var width = Mathf.Max(4f, rect.width * 0.09f);
            var r = Mathf.Min(rect.width, rect.height) / 2f - width / 2f - 1f;
            if (r < 2f)
                return;
            var p = ctx.painter2D;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.strokeColor = Theme.Line;
            p.BeginPath();
            p.Arc(rect.center, r, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Stroke();
            if (_value <= 0f)
                return;
            p.strokeColor = _color;
            p.BeginPath();
            p.Arc(rect.center, r, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * Mathf.Min(_value, 0.999f)));
            p.Stroke();
        }
    }

    /// <summary>
    /// Una pregunta con sus opciones (barajadas). El estudiante elige, comprueba y ve la explicación;
    /// <c>onAnswered</c> recibe si acertó.
    /// </summary>
    public class QuestionView : VisualElement
    {
        private readonly Question _question;
        private readonly Action<bool> _onAnswered;
        private readonly List<int> _order;
        private readonly HashSet<int> _chosen = new HashSet<int>();
        private readonly List<Button> _buttons = new List<Button>();
        private readonly Button _check;
        private readonly VisualElement _feedback;
        private bool _done;

        private bool Multi => _question.Kind == "multi";

        public bool Done => _done;

        public QuestionView(Question question, Action<bool> onAnswered, System.Random random = null)
        {
            _question = question;
            _onAnswered = onAnswered;
            AddToClassList("question");
            this.Append(El.Text(question.Prompt, "question__prompt"));
            if (Multi)
                this.Append(El.Text("Puede haber varias respuestas correctas.", "question__hint"));

            _order = Enumerable.Range(0, question.Options.Count).ToList();
            if (question.Kind != "truefalse")
            {
                random ??= new System.Random();
                _order = _order.OrderBy(_ => random.Next()).ToList();
            }
            var options = this.Append(El.Div("question__options", question.Kind == "truefalse" ? "question__options--row" : null));
            foreach (var index in _order)
            {
                var button = options.Append(El.Btn(string.Empty, () => Toggle(index), "option"));
                button.Append(El.Div("option__mark")).pickingMode = PickingMode.Ignore;
                button.Append(El.Text(question.Options[index], "option__text")).pickingMode = PickingMode.Ignore;
                button.userData = index;
                _buttons.Add(button);
            }
            _feedback = this.Append(El.Div("feedback"));
            _feedback.SetVisible(false);
            _check = this.Append(El.Btn("Comprobar", Check, "btn", "question__check"));
            _check.SetEnabled(false);
        }

        private void Toggle(int index)
        {
            if (_done)
                return;
            if (!Multi)
                _chosen.Clear();
            if (!_chosen.Remove(index))
                _chosen.Add(index);
            foreach (var button in _buttons)
                button.EnableInClassList("option--chosen", _chosen.Contains((int)button.userData));
            _check.SetEnabled(_chosen.Count > 0);
            if (!Multi)
                Check();
        }

        /// <summary>Responde con estas opciones (índices de la pregunta original).</summary>
        public void Submit(IEnumerable<int> chosen)
        {
            if (_done)
                return;
            _chosen.Clear();
            foreach (var index in chosen)
                _chosen.Add(index);
            Check();
        }

        /// <summary>Se acabó el tiempo: cuenta como fallo y muestra la respuesta.</summary>
        public void TimeUp()
        {
            if (_done)
                return;
            _done = true;
            Reveal(false, "Se acabó el tiempo");
        }

        private void Check()
        {
            if (_done || _chosen.Count == 0)
                return;
            _done = true;
            var correct = _question.IsCorrect(_chosen);
            Reveal(correct, correct ? "¡Correcto!" : "No es correcto");
        }

        private void Reveal(bool correct, string title)
        {
            foreach (var button in _buttons)
            {
                var index = (int)button.userData;
                var isAnswer = _question.Answer.Contains(index);
                button.EnableInClassList("option--chosen", false);
                button.EnableInClassList("option--right", isAnswer);
                button.EnableInClassList("option--wrong", !isAnswer && _chosen.Contains(index));
                button.EnableInClassList("option--locked", true);
                if (isAnswer && correct)
                    Anim.Pop(button);
                else if (!isAnswer && _chosen.Contains(index))
                    Anim.Shake(button);
            }
            _check.SetVisible(false);
            _feedback.SetVisible(true);
            _feedback.Enter();
            _feedback.EnableInClassList("feedback--ok", correct);
            _feedback.EnableInClassList("feedback--bad", !correct);
            var head = _feedback.Append(El.Row("feedback__head"));
            head.Append(new Icon(correct ? IconKind.Check : IconKind.Close));
            head.Append(El.Text(title, "feedback__title"));
            if (!string.IsNullOrEmpty(_question.Explain))
                _feedback.Append(El.Text(_question.Explain, "feedback__text"));
            _onAnswered?.Invoke(correct);
        }
    }
}
