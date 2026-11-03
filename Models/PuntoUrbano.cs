namespace BogoMaps.Models;

public enum CategoriaPunto
{
    Iluminacion,
    Accesibilidad,
    Aceras,
    Seguridad
}

public enum GravedadPunto
{
    Bajo,
    Moderado,
    Critico
}

public class DetallePunto
{
    public string Titulo { get; set; } = "";
    public string Valor { get; set; } = "";
}

public class PuntoUrbano
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string Ubicacion { get; set; } = "";
    public CategoriaPunto Categoria { get; set; }
    public GravedadPunto Gravedad { get; set; }
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public double Calificacion { get; set; }
    public int Confirmaciones { get; set; }
    public List<DetallePunto> Detalles { get; set; } = [];

    // Solo los reportes creados por usuarios traen estos campos.
    public string Descripcion { get; set; } = "";
    public string? FotoArchivo { get; set; }
    public DateTime? FechaReporte { get; set; }
}
