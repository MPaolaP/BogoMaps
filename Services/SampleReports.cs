using BogoMaps.Models;
namespace BogoMaps.Services;

// Synthetic observations at real coordinates, never presented as verified conditions.
public static class SampleReports
{
    public static IReadOnlySet<string> Ids { get; } = Enumerable.Range(1, 6).Select(i => $"sample-v1-{i}").ToHashSet();
    public static List<PedestrianReport> Create()
    {
        var now = DateTimeOffset.UtcNow;
        return [
            new() { Id = "sample-v1-1", IsDemo = true, Category = "Aceras", Description = "[Prueba] Baldosa suelta en el andén", Address = "Calle 53 con Carrera 13, Bogotá", Latitude = 4.6406, Longitude = -74.0653, Severity = "Moderado", Pavement = "Deteriorado", Accessibility = "Con barreras", Saved = true, CreatedAt = now.AddHours(-1) },
            new() { Id = "sample-v1-2", IsDemo = true, Category = "Iluminación", Description = "[Prueba] Luminaria apagada", Address = "Calle 45 con Carrera 19, Bogotá", Latitude = 4.6337, Longitude = -74.0744, Severity = "Crítico", Lighting = "Sin alumbrado", Noise = "Bajo", CreatedAt = now.AddHours(-2) },
            new() { Id = "sample-v1-3", IsDemo = true, Category = "Accesibilidad", Description = "[Prueba] Rampa despejada", Address = "Carrera 11 con Calle 72, Bogotá", Latitude = 4.6571, Longitude = -74.0599, Severity = "Bajo", Accessibility = "Apto", Pavement = "Bueno", Saved = true, Resolved = true, Confirmed = true, ConfirmedAt = now.AddMinutes(-30), CreatedAt = now.AddHours(-3) },
            new() { Id = "sample-v1-4", IsDemo = true, Category = "Cruces", Description = "[Prueba] Cruce con señalización deteriorada", Address = "Carrera 7 con Calle 67, Bogotá", Latitude = 4.6500, Longitude = -74.0570, Severity = "Crítico", Accessibility = "Con barreras", Noise = "Alto", Anonymous = true, CreatedAt = now.AddHours(-4) },
            new() { Id = "sample-v1-5", IsDemo = true, Category = "Obras", Description = "[Prueba] Cerramiento sobre el paso peatonal", Address = "Calle 63 con Carrera 13, Bogotá", Latitude = 4.6486, Longitude = -74.0630, Severity = "Moderado", Pavement = "Obstruido", Accessibility = "Inaccesible", CreatedAt = now.AddHours(-5) },
            new() { Id = "sample-v1-6", IsDemo = true, Category = "Punto seguro", Description = "[Prueba] Paso amplio e iluminado", Address = "Carrera 7 con Calle 72, Bogotá", Latitude = 4.6550, Longitude = -74.0548, Severity = "Bajo", Lighting = "Buena", Pavement = "Bueno", Accessibility = "Apto", Noise = "Medio", Saved = true, Resolved = true, Confirmed = true, ConfirmedAt = now.AddMinutes(-15), CreatedAt = now.AddHours(-6) }
        ];
    }
}
