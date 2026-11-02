using System.Text.Json;
using BogoMaps.Models;

namespace BogoMaps.Services;

// Writes are serialized and replaced atomically. UI receives snapshots only after a successful save.
public sealed class LocalRepository
{
    public static LocalRepository Instance { get; } = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    public string? AccountId { get; set; }
    private string PathName => Path.Combine(FileSystem.AppDataDirectory, AccountId == null ? "reports-v1.json" :
        "reports-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(AccountId))) + ".json");

    public async Task<List<PedestrianReport>> ReadAsync()
    {
        var path = PathName;
        await gate.WaitAsync();
        try { return await ReadCoreAsync(path); }
        finally { gate.Release(); }
    }

    private async Task<List<PedestrianReport>> ReadCoreAsync(string path)
    {
        if (!File.Exists(path)) return [];
        // Do not silently replace unreadable user data with an empty store.
        return JsonSerializer.Deserialize<List<PedestrianReport>>(await File.ReadAllTextAsync(path))
            ?? throw new InvalidDataException("No se pudieron leer los reportes locales.");
    }

    public async Task SaveAsync(PedestrianReport report)
    {
        if (string.IsNullOrWhiteSpace(report.Description) || report.Description.Length > 250)
            throw new ArgumentException("Escribe una descripción de entre 1 y 250 caracteres.");
        if (string.IsNullOrWhiteSpace(report.Address))
            throw new ArgumentException("Escribe una dirección o referencia del lugar.");
        if (!new[] { "Aceras", "Iluminación", "Accesibilidad", "Cruces", "Obras", "Punto seguro" }.Contains(report.Category) ||
            !new[] { "Bajo", "Moderado", "Crítico" }.Contains(report.Severity))
            throw new ArgumentException("Selecciona una categoría y gravedad válidas.");
        if (!double.IsFinite(report.Latitude) || !double.IsFinite(report.Longitude) ||
            report.Latitude is < 3.7 or > 4.9 || report.Longitude is < -74.6 or > -73.9)
            throw new ArgumentException("Selecciona una ubicación dentro de Bogotá.");
        var path = PathName;
        await gate.WaitAsync();
        try
        {
            var reports = await ReadCoreAsync(path);
            var index = reports.FindIndex(r => r.Id == report.Id);
            if (index < 0) reports.Add(report); else reports[index] = report;
            var temporary = path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(reports));
            File.Move(temporary, path, true);
        }
        finally { gate.Release(); }
    }
}
