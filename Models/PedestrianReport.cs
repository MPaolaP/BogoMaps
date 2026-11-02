namespace BogoMaps.Models;

public sealed class PedestrianReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Category { get; set; } = "Aceras";
    public string Description { get; set; } = "";
    public string Address { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Severity { get; set; } = "Moderado";
    public string Lighting { get; set; } = "Sin evaluar";
    public string Pavement { get; set; } = "Sin evaluar";
    public string Accessibility { get; set; } = "Sin evaluar";
    public string Noise { get; set; } = "Sin evaluar";
    public string? Photo { get; set; }
    public bool IsDemo { get; set; }
    public bool Anonymous { get; set; }
    public bool Saved { get; set; }
    public bool Confirmed { get; set; }
    public bool Resolved { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string Summary => $"{Category} · {Severity} · {(Resolved ? "Resuelto" : "Activo")}";
}
