using System.Text.Json;
using System.Text.Json.Serialization;
using BogoMaps.Models;

namespace BogoMaps.Services;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(List<PuntoUrbano>))]
internal partial class PuntosJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Origen de los puntos urbanos. Combina los datos de ejemplo (Resources/Raw/puntos.json) con los
/// reportes creados en el dispositivo (reportes.json y fotos/ en la carpeta de datos de la app);
/// cuando exista backend solo cambia esta clase.
/// </summary>
public class PuntosService
{
    // Los ids de los reportes del usuario empiezan aquí para no chocar con los de ejemplo.
    private const int IdInicialReportes = 1000;

    // Las páginas se crean desde Shell sin inyección de dependencias, así que comparten esta instancia.
    public static PuntosService Compartido { get; } = new();

    private static string ArchivoReportes => Path.Combine(FileSystem.AppDataDirectory, "reportes.json");
    private static string CarpetaFotos => Path.Combine(FileSystem.AppDataDirectory, "fotos");

    private List<PuntoUrbano>? _puntos;
    private List<PuntoUrbano> _reportes = [];

    public static string RutaFoto(string archivo) => Path.Combine(CarpetaFotos, archivo);

    public async Task<IReadOnlyList<PuntoUrbano>> ObtenerAsync()
    {
        if (_puntos is not null)
            return _puntos;

        await using var stream = await FileSystem.OpenAppPackageFileAsync("puntos.json");
        var puntos = await JsonSerializer.DeserializeAsync(stream, PuntosJsonContext.Default.ListPuntoUrbano) ?? [];

        _reportes = await LeerReportesAsync();
        puntos.AddRange(_reportes);

        _puntos = puntos;
        return _puntos;
    }

    /// <summary>Copia la foto elegida a la carpeta de la app y devuelve el nombre del archivo guardado.</summary>
    public async Task<string> GuardarFotoAsync(FileResult foto)
    {
        Directory.CreateDirectory(CarpetaFotos);

        var extension = Path.GetExtension(foto.FileName);
        var archivo = $"{Guid.NewGuid():N}{(string.IsNullOrEmpty(extension) ? ".jpg" : extension)}";

        await using var origen = await foto.OpenReadAsync();
        await using var destino = File.Create(RutaFoto(archivo));
        await origen.CopyToAsync(destino);

        return archivo;
    }

    public static void EliminarFoto(string archivo) => File.Delete(RutaFoto(archivo));

    /// <summary>Asigna el id al reporte, lo guarda en disco y lo suma a los puntos del mapa.</summary>
    public async Task AgregarReporteAsync(PuntoUrbano reporte)
    {
        var puntos = await ObtenerAsync();
        reporte.Id = Math.Max(IdInicialReportes, puntos.Select(p => p.Id).DefaultIfEmpty(0).Max() + 1);

        // Se escribe primero: si falla, el reporte no queda solo en memoria.
        List<PuntoUrbano> reportes = [.. _reportes, reporte];
        await using (var stream = File.Create(ArchivoReportes))
            await JsonSerializer.SerializeAsync(stream, reportes, PuntosJsonContext.Default.ListPuntoUrbano);

        _reportes = reportes;
        _puntos!.Add(reporte);
    }

    private static async Task<List<PuntoUrbano>> LeerReportesAsync()
    {
        if (!File.Exists(ArchivoReportes))
            return [];

        try
        {
            await using var stream = File.OpenRead(ArchivoReportes);
            return await JsonSerializer.DeserializeAsync(stream, PuntosJsonContext.Default.ListPuntoUrbano) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
