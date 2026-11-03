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

    public string StoragePath => PathName;

    public async Task<int> ImportLocalAsync(IEnumerable<PedestrianReport> incoming)
    {
        var items = incoming.ToList();
        foreach (var item in items)
        {
            Validate(item);
            if (item.IsShared) throw new ArgumentException("La importación de archivos anteriores es solo local.");
        }
        var path = PathName;
        await gate.WaitAsync();
        try
        {
            var reports = await ReadCoreAsync(path);
            var ids = reports.Select(r => r.Id).ToHashSet();
            var added = 0;
            foreach (var item in items) if (ids.Add(item.Id)) { reports.Add(item); added++; }
            if (added > 0) await WriteCoreAsync(path, reports);
            return added;
        }
        finally { gate.Release(); }
    }

    public async Task<List<PedestrianReport>> ReadCommunityCacheAsync()
    {
        if (AccountId == null) return [];
        var path = PathName + ".community.json";
        await gate.WaitAsync();
        try { return await ReadCoreAsync(path); }
        finally { gate.Release(); }
    }
    public async Task CacheCommunityAsync(IEnumerable<PedestrianReport> incoming)
    {
        if (AccountId == null) throw new InvalidOperationException("Inicia sesión para guardar la copia comunitaria.");
        var path = PathName + ".community.json";
        await gate.WaitAsync();
        try
        {
            var reports = await ReadCoreAsync(path);
            foreach (var report in incoming)
            {
                if (!report.IsShared || report.IsDemo || string.IsNullOrWhiteSpace(report.OwnerId) || !Guid.TryParseExact(report.Id, "N", out _)) throw new ArgumentException("Solo se admiten reportes comunitarios reales.");
                var index = reports.FindIndex(r => r.Id == report.Id);
                if (index < 0) reports.Add(report); else reports[index] = report;
            }
            await WriteCoreAsync(path, reports);
        }
        finally { gate.Release(); }
    }

    public async Task<int> LoadSamplesAsync()
    {
        var path = PathName;
        await gate.WaitAsync();
        try
        {
            var reports = await ReadCoreAsync(path);
            var existing = reports.Select(r => r.Id).ToHashSet();
            var samples = SampleReports.Create().Where(r => !existing.Contains(r.Id)).ToList();
            if (samples.Count == 0) return 0;
            reports.AddRange(samples);
            await WriteCoreAsync(path, reports);
            return samples.Count;
        }
        finally { gate.Release(); }
    }

    public async Task<int> RemoveSamplesAsync()
    {
        var path = PathName;
        await gate.WaitAsync();
        try
        {
            var reports = await ReadCoreAsync(path);
            var removed = reports.RemoveAll(r => r.IsDemo);
            if (removed > 0) await WriteCoreAsync(path, reports);
            return removed;
        }
        finally { gate.Release(); }
    }

    private static async Task WriteCoreAsync(string path, List<PedestrianReport> reports)
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(reports));
        File.Move(temporary, path, true);
    }

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

    public static void Validate(PedestrianReport report)
    {
        if (string.IsNullOrWhiteSpace(report.Description) || report.Description.Length > 250)
            throw new ArgumentException("Escribe una descripción de entre 1 y 250 caracteres.");
        if (string.IsNullOrWhiteSpace(report.Address) || report.Address.Length > 300)
            throw new ArgumentException("Escribe una dirección o referencia de entre 1 y 300 caracteres.");
        if (!new[] { "Aceras", "Iluminación", "Accesibilidad", "Cruces", "Obras", "Punto seguro" }.Contains(report.Category) ||
            !new[] { "Bajo", "Moderado", "Crítico" }.Contains(report.Severity))
            throw new ArgumentException("Selecciona una categoría y gravedad válidas.");
        if (!new[] { "Sin evaluar", "Buena", "Deficiente", "Sin alumbrado" }.Contains(report.Lighting) ||
            !new[] { "Sin evaluar", "Bueno", "Deteriorado", "Obstruido" }.Contains(report.Pavement) ||
            !new[] { "Sin evaluar", "Apto", "Con barreras", "Inaccesible" }.Contains(report.Accessibility) ||
            !new[] { "Sin evaluar", "Bajo", "Medio", "Alto" }.Contains(report.Noise))
            throw new ArgumentException("Selecciona valoraciones válidas para el estado del punto.");
        if (!double.IsFinite(report.Latitude) || !double.IsFinite(report.Longitude) ||
            report.Latitude is < 3.7 or > 4.9 || report.Longitude is < -74.6 or > -73.9)
            throw new ArgumentException("Selecciona una ubicación dentro de Bogotá.");
    }
    public async Task SaveAsync(PedestrianReport report)
    {
        Validate(report);
        var path = PathName;
        await gate.WaitAsync();
        try
        {
            var reports = await ReadCoreAsync(path);
            var index = reports.FindIndex(r => r.Id == report.Id);
            if (index < 0) reports.Add(report); else reports[index] = report;
            await WriteCoreAsync(path, reports);
        }
        finally { gate.Release(); }
    }
}
