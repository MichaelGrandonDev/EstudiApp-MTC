using System.Collections.Generic;
using System.Linq;

namespace EstudiApp.Domain
{
    /// <summary>
    /// Diagrama interactivo descrito como datos. Kind:
    /// <list type="bullet">
    /// <item>graph: nodos y flechas, con capas que se encienden y pasos guiados (Wu Xing, formación del Qi…).</item>
    /// <item>ring: anillo de segmentos con aguja horaria opcional (reloj de los meridianos).</item>
    /// <item>balance: balanza Yin-Yang con dos controles y el estado resultante.</item>
    /// <item>tongue: mapa de la lengua por zonas.</item>
    /// </list>
    /// </summary>
    public class DiagramSpec
    {
        public string Id;
        public string Title;
        public string Kind = "graph";
        public string Intro;
        /// <summary>Alto / ancho del lienzo.</summary>
        public float Aspect = 0.8f;
        public DiagramLayout Layout;
        public List<DiagramNode> Nodes = new List<DiagramNode>();
        public List<DiagramEdge> Edges = new List<DiagramEdge>();
        public List<DiagramLayer> Layers = new List<DiagramLayer>();
        public List<DiagramStep> Steps = new List<DiagramStep>();
        public List<RingSegment> Segments = new List<RingSegment>();
        public List<BalanceState> States = new List<BalanceState>();
        public List<DiagramZone> Zones = new List<DiagramZone>();
        /// <summary>Anillo: dibujo central ("taiji") en lugar del título.</summary>
        public string Center;
        /// <summary>Anillo: muestra la hora actual sobre el reloj de 24 h.</summary>
        public bool ShowClock;
        /// <summary>Anillo: hora en la que empieza el primer segmento (se dibuja arriba a la izquierda de las 12).</summary>
        public float StartHour;
        public List<Reference> References = new List<Reference>();

        public DiagramNode Node(string id) => Nodes.FirstOrDefault(n => n.Id == id);
        public DiagramLayer Layer(string id) => Layers.FirstOrDefault(l => l.Id == id);
    }

    /// <summary>Colocación automática: los nodos de Order en círculo (en sentido horario desde StartAngle; -90 = arriba).</summary>
    public class DiagramLayout
    {
        public string Type = "circle";
        public List<string> Order = new List<string>();
        public float CenterX = 0.5f;
        public float CenterY = 0.5f;
        /// <summary>Radio relativo al ancho del lienzo.</summary>
        public float Radius = 0.32f;
        public float StartAngle = -90f;
    }

    public class DiagramNode
    {
        public string Id;
        public string Label;
        public string Hanzi;
        public string Sub;
        /// <summary>Centro, relativo a ancho (X) y alto (Y) del lienzo.</summary>
        public float X = 0.5f;
        public float Y = 0.5f;
        /// <summary>circle o rect.</summary>
        public string Shape = "circle";
        /// <summary>Radio (círculo) o medio ancho (rectángulo), relativo al ancho del lienzo.</summary>
        public float Size = 0.075f;
        /// <summary>Medio alto del rectángulo relativo al ancho; 0 = automático.</summary>
        public float Height;
        public string Color = "#2E7D6B";
        public string Detail;
        public Reference Ref;
        /// <summary>Si tiene capa, sólo se ve con esa capa encendida.</summary>
        public string Layer;
    }

    public class DiagramEdge
    {
        public string From;
        public string To;
        public string Layer;
        public string Label;
        /// <summary>Curvatura: 0 recta, positivo hacia la izquierda del sentido de la flecha.</summary>
        public float Bend;
        public bool Dashed;
        /// <summary>Flechas en ambos extremos.</summary>
        public bool Both;
        public bool NoArrow;
        public string Color;
        public string Detail;

        public string Key => From + ">" + To + (Layer == null ? string.Empty : "@" + Layer);
    }

    public class DiagramLayer
    {
        public string Id;
        public string Name;
        public string Color = "#2E7D6B";
        public string Description;
        public bool On = true;
    }

    /// <summary>Paso de un recorrido guiado: resalta nodos y deja encendidas sólo ciertas capas.</summary>
    public class DiagramStep
    {
        public string Title;
        public string Text;
        public List<string> Focus = new List<string>();
        /// <summary>null = no cambia las capas.</summary>
        public List<string> Layers;
    }

    public class RingSegment
    {
        public string Id;
        public string Label;
        public string Hanzi;
        public string Sub;
        public string Color = "#2E7D6B";
        public string Detail;
        /// <summary>Hora de inicio (0-24) cuando el anillo es un reloj.</summary>
        public float Hour;
    }

    public class BalanceState
    {
        public string Id;
        public string Title;
        public string Text;
        public Reference Ref;
    }

    public class DiagramZone
    {
        public string Id;
        public string Label;
        public string Organs;
        public string Detail;
        public string Color = "#C8665C";
    }
}
