using System;
using EstudiApp.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace EstudiApp.UI.Diagrams
{
    /// <summary>
    /// Diagrama completo: título, explicación, lienzo, capas que se encienden, recorrido guiado y ficha de detalle
    /// de lo que se toca (con su cita al libro).
    /// </summary>
    public class DiagramPanel : VisualElement
    {
        private readonly DiagramSpec _spec;
        private readonly Curriculum _curriculum;
        private readonly Action<Reference> _openRef;
        private readonly DiagramCanvas _canvas;
        private readonly VisualElement _layers;
        private readonly VisualElement _detail;
        private VisualElement _tour;
        private int _step = -1;

        public DiagramCanvas Canvas => _canvas;

        public DiagramPanel(DiagramSpec spec, Curriculum curriculum, Action<Reference> openRef, bool showHeader = true)
        {
            _spec = spec;
            _curriculum = curriculum;
            _openRef = openRef;
            AddToClassList("diagram");

            if (showHeader)
            {
                var header = this.Append(El.Div("diagram__header"));
                header.Append(new Icon(IconKind.Diagram)).AddToClassList("diagram__icon");
                header.Append(El.Text(spec.Title, "diagram__title"));
            }
            if (!string.IsNullOrEmpty(spec.Intro))
                this.Append(El.Text(spec.Intro, "diagram__intro"));

            _canvas = this.Append(DiagramCanvas.Create(spec));
            _canvas.Selected += ShowDetail;

            var controls = this.Append(El.Div("diagram__controls"));
            _canvas.BuildControls(controls);

            _layers = this.Append(El.Div("chips", "diagram__layers"));
            BuildLayers();

            if (spec.Steps.Count > 0)
            {
                _tour = this.Append(El.Div("tour"));
                BuildTour();
            }

            _detail = this.Append(El.Div("detail"));
            _detail.SetVisible(false);

            if (spec.References.Count > 0)
            {
                var refs = this.Append(El.Div("diagram__refs"));
                refs.Append(El.Text("Fuentes", "diagram__refs-title"));
                foreach (var reference in spec.References)
                    refs.Add(Refs.Button(reference, curriculum, openRef));
            }
            this.Append(El.Text(Hint(spec), "diagram__hint"));
        }

        private static string Hint(DiagramSpec spec)
        {
            switch (spec.Kind)
            {
                case "balance": return "Mueve los controles o elige un estado.";
                case "ring": return "Toca un sector para ver su explicación.";
                case "tongue": return "Toca una zona de la lengua.";
                default: return "Toca un elemento o una flecha para ver su explicación.";
            }
        }

        private void BuildLayers()
        {
            _layers.Clear();
            _layers.SetVisible(_spec.Layers.Count > 0);
            foreach (var layer in _spec.Layers)
            {
                var on = _canvas.LayerOn(layer.Id);
                var chip = El.Chip(layer.Name, () =>
                {
                    _canvas.SetLayer(layer.Id, !_canvas.LayerOn(layer.Id));
                    BuildLayers();
                    if (!string.IsNullOrEmpty(layer.Description) && _canvas.LayerOn(layer.Id))
                        ShowDetail(new DiagramSelection { Title = layer.Name, Text = layer.Description, Color = El.Hex(layer.Color) });
                }, on, El.Hex(layer.Color));
                _layers.Add(chip);
            }
        }

        private void BuildTour()
        {
            _tour.Clear();
            var head = _tour.Append(El.Row("tour__head"));
            head.Append(new Icon(IconKind.Play)).AddToClassList("tour__icon");
            if (_step < 0)
            {
                head.Append(El.Text($"Recorrido guiado · {_spec.Steps.Count} pasos", "tour__title"));
                head.Append(El.Spacer());
                head.Append(El.Btn("Empezar", () => GoTo(0), "btn", "btn--small"));
                return;
            }
            var step = _spec.Steps[_step];
            head.Append(El.Text($"Paso {_step + 1} de {_spec.Steps.Count}", "tour__count"));
            head.Append(El.Spacer());
            head.Append(El.Btn("Salir", () => GoTo(-1), "btn", "btn--ghost", "btn--small"));
            _tour.Append(El.Text(step.Title, "tour__step-title"));
            _tour.Append(El.Text(step.Text, "tour__text"));
            var nav = _tour.Append(El.Row("tour__nav"));
            var prev = nav.Append(El.IconBtn(IconKind.Back, "Anterior", () => GoTo(_step - 1), "btn--ghost"));
            prev.SetEnabled(_step > 0);
            nav.Append(El.Spacer());
            var last = _step == _spec.Steps.Count - 1;
            nav.Append(El.Btn(last ? "Terminar" : "Siguiente", () => GoTo(last ? -1 : _step + 1), "btn", "btn--small"));
        }

        private void GoTo(int index)
        {
            _step = index < 0 || index >= _spec.Steps.Count ? -1 : index;
            _canvas.ApplyStep(_step < 0 ? null : _spec.Steps[_step]);
            BuildLayers();
            BuildTour();
        }

        private void ShowDetail(DiagramSelection selection)
        {
            _detail.Clear();
            _detail.SetVisible(selection != null);
            if (selection == null)
                return;
            _detail.style.borderLeftColor = selection.Color;
            var head = _detail.Append(El.Row("detail__head"));
            if (!string.IsNullOrEmpty(selection.Hanzi))
                head.Append(El.Text(selection.Hanzi, "detail__hanzi")).Ink(selection.Color);
            head.Append(El.Text(selection.Title, "detail__title"));
            head.Append(El.Spacer());
            head.Append(El.IconBtn(IconKind.Close, null, () => _detail.SetVisible(false), "btn--ghost", "icon-btn--small"));
            if (!string.IsNullOrEmpty(selection.Text))
                _detail.Append(El.Text(selection.Text, "detail__text"));
            if (selection.Ref != null && !string.IsNullOrEmpty(selection.Ref.Book))
                _detail.Add(Refs.Button(selection.Ref, _curriculum, _openRef));
        }
    }
}
