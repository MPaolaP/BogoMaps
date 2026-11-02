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
/// Origen de los puntos urbanos. Por ahora lee datos de ejemplo desde Resources/Raw/puntos.json;
/// cuando exista backend solo cambia esta clase.
/// </summary>
public class PuntosService
{
    private List<PuntoUrbano>? _puntos;

    public async Task<IReadOnlyList<PuntoUrbano>> ObtenerAsync()
    {
        if (_puntos is not null)
            return _puntos;

        await using var stream = await FileSystem.OpenAppPackageFileAsync("puntos.json");
        _puntos = await JsonSerializer.DeserializeAsync(stream, PuntosJsonContext.Default.ListPuntoUrbano) ?? [];
        return _puntos;
    }
}
