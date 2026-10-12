using EstudiApp.Core;
using UnityEngine.UIElements;

namespace EstudiApp.UI
{
    /// <summary>Una pantalla dentro de la pila de navegación de una pestaña.</summary>
    public abstract class Page
    {
        protected AppShell Shell { get; private set; }
        protected AppServices S => Shell.Services;

        public VisualElement Root { get; } = El.Div("page");

        public virtual string Title => string.Empty;

        /// <summary>Con true, el contenido no va dentro de un ScrollView (la página gestiona su propio desplazamiento).</summary>
        public virtual bool OwnsScroll => false;

        internal void Attach(AppShell shell) => Shell = shell;

        /// <summary>Construye o actualiza la pantalla cada vez que se muestra.</summary>
        public abstract void Build();

        public virtual void OnHide()
        {
        }

        /// <summary>Se llama al salir de la pila (liberar texturas, documentos…).</summary>
        public virtual void OnClose()
        {
        }
    }
}
