namespace Examen_Parcial_Incidencias.Models
{
    public class Incidencia
    {
        public int Id { get; set; }
        public string Estacion { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Prioridad { get; set; } = "Media"; // Alta, Media, Baja
        public string Estado { get; set; } = "Abierta"; // Abierta, Cerrada
    }
}