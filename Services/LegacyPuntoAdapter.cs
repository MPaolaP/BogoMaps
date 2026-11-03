using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BogoMaps.Models;

namespace BogoMaps.Services;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(List<PuntoUrbano>))]
internal partial class LegacyPuntoJsonContext : JsonSerializerContext { }

public sealed record LegacyImportPreview(List<PedestrianReport> Reports, List<string> Warnings);

public static class LegacyPuntoAdapter
{
    public static LegacyImportPreview Read(string json, string photoDirectory, bool samples = false)
    {
        var source = JsonSerializer.Deserialize(json, LegacyPuntoJsonContext.Default.ListPuntoUrbano)
            ?? throw new InvalidDataException("El archivo anterior no contiene una lista de reportes.");
        var reports = new List<PedestrianReport>();
        var warnings = new List<string>();
        foreach (var point in source)
        {
            try { reports.Add(Convert(point, photoDirectory, samples)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
            { warnings.Add($"Reporte anterior {point.Id}: {ex.Message}"); }
        }
        return new(reports, warnings);
    }
    private static PedestrianReport Convert(PuntoUrbano point, string photoDirectory, bool sample)
    {
        var title = point.Titulo.Trim();
        var category = point.Categoria switch
        {
            CategoriaPunto.Iluminacion => "Iluminación", CategoriaPunto.Accesibilidad => "Accesibilidad",
            CategoriaPunto.Aceras when title.Contains("obra", StringComparison.OrdinalIgnoreCase) => "Obras",
            CategoriaPunto.Aceras => "Aceras",
            CategoriaPunto.Seguridad when title.Contains("cruce", StringComparison.OrdinalIgnoreCase) => "Cruces",
            CategoriaPunto.Seguridad when title.StartsWith("Punto seguro", StringComparison.OrdinalIgnoreCase) => "Punto seguro",
            CategoriaPunto.Seguridad when sample => point.Gravedad == GravedadPunto.Bajo ? "Punto seguro" : "Cruces",
            _ => throw new InvalidDataException("Categoría de seguridad ambigua; revisa el original antes de importarlo.")
        };
        string? photo = null;
        if (!string.IsNullOrEmpty(point.FotoArchivo))
        {
            if (Path.IsPathRooted(point.FotoArchivo) || point.FotoArchivo.IndexOfAny(['/', '\\']) >= 0 || point.FotoArchivo is "." or "..")
                throw new InvalidDataException("El nombre de foto no es seguro. El archivo original no se modifica.");
            var candidate = Path.Combine(photoDirectory, point.FotoArchivo);
            if (File.Exists(candidate)) photo = candidate;
        }
        var text = string.IsNullOrWhiteSpace(point.Descripcion) ? title : point.Descripcion.Trim();
        if (string.IsNullOrWhiteSpace(text)) text = "Reporte importado";
        if (sample) text = "[Prueba] " + title;
        if (text.Length > 250) throw new InvalidDataException("La descripción supera 250 caracteres; revisa el original.");
        if (!sample && point.FechaReporte == null) throw new InvalidDataException("La fecha original no está disponible; revisa el registro antes de importarlo.");
        var date = point.FechaReporte is { } value ? new DateTimeOffset(value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime()) : DateTimeOffset.UnixEpoch;
        var identity = JsonSerializer.Serialize(new { source = "BogoMaps/reportes-v1", point.Id, point.Titulo, point.Descripcion, point.Latitud, point.Longitud, point.FotoArchivo, date });
        var id = sample ? $"stitch-sample-{point.Id}" : System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32].ToLowerInvariant();
        var report = new PedestrianReport { Id = id, IsDemo = sample, Description = text, Category = category,
            Address = string.IsNullOrWhiteSpace(point.Ubicacion) ? string.Create(CultureInfo.InvariantCulture, $"{point.Latitud:F5}, {point.Longitud:F5}") : point.Ubicacion,
            Anonymous = point.Detalles?.Any(d => d != null && string.Equals(d.Titulo, "Reportado por", StringComparison.OrdinalIgnoreCase) && string.Equals(d.Valor, "Anónimo", StringComparison.OrdinalIgnoreCase)) == true,
            Latitude = point.Latitud, Longitude = point.Longitud, Photo = photo, CreatedAt = sample ? DateTimeOffset.UtcNow.AddMinutes(-point.Id) : date,
            Severity = point.Gravedad switch { GravedadPunto.Bajo => "Bajo", GravedadPunto.Moderado => "Moderado", GravedadPunto.Critico => "Crítico", _ => throw new InvalidDataException("Gravedad anterior no reconocida.") } };
        // Legacy ratings/confirmation totals were mock data; they do not become real votes or verified assessments.
        LocalRepository.Validate(report);
        return report;
    }
}
