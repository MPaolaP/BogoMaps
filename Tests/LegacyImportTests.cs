using System.Text.Json;
using BogoMaps.Services;
using BogoMaps.Models;

internal static class LegacyImportTests
{
    public static async Task RunAsync(string root)
    {
        var photos = Path.Combine(root, "fotos"); Directory.CreateDirectory(photos);
        var photoPath = Path.Combine(photos, "evidencia.jpg");
        await File.WriteAllTextAsync(photoPath, "original-photo-must-survive");
        const string original = """
        [{"id":1000,"titulo":"Falta de iluminación","ubicacion":"Calle 53","categoria":"Iluminacion","gravedad":"Critico","latitud":4.64,"longitud":-74.06,"descripcion":"Poste apagado","fotoArchivo":"evidencia.jpg","fechaReporte":"2026-11-02T12:00:00Z","calificacion":4.9,"confirmaciones":99,"detalles":[{"titulo":"Reportado por","valor":"Anónimo"}]}]
        """;
        var preview = LegacyPuntoAdapter.Read(original, photos);
        var report = preview.Reports.Single();
        if (preview.Warnings.Count != 0 || report.Category != "Iluminación" || report.Severity != "Crítico" || report.Description != "Poste apagado" ||
            report.Photo != photoPath || !report.Anonymous || report.IsShared || report.IsDemo || report.Confirmed || report.CreatedAt != DateTimeOffset.Parse("2026-11-02T12:00:00Z")) throw new Exception("Legacy import changed report meaning");
        if (report.Id != LegacyPuntoAdapter.Read(original, photos).Reports.Single().Id) throw new Exception("Legacy import IDs are not stable");
        var store = new LocalRepository { AccountId = "legacy-import" };
        if (await store.ImportLocalAsync(preview.Reports) != 1 || await store.ImportLocalAsync(preview.Reports) != 0) throw new Exception("Legacy import duplicates reports");
        report.Saved = true;
        await store.SaveAsync(report);
        await store.ImportLocalAsync(preview.Reports);
        if (!(await store.ReadAsync()).Single().Saved) throw new Exception("Legacy import overwrote current edits");
        store.AccountId = "another-legacy-user";
        if ((await store.ReadAsync()).Count != 0) throw new Exception("Legacy import crossed account scope");
        var unsafePath = LegacyPuntoAdapter.Read(original.Replace("evidencia.jpg", "../private.jpg"), photos);
        if (unsafePath.Reports.Count != 0 || unsafePath.Warnings.Count != 1) throw new Exception("Unsafe legacy photo name accepted");
        var outside = LegacyPuntoAdapter.Read(original.Replace("4.64", "40.64"), photos);
        if (outside.Reports.Count != 0 || outside.Warnings.Count != 1) throw new Exception("Out-of-area legacy location accepted without review");
        var corruptionReported = false;
        try { LegacyPuntoAdapter.Read("broken-json", photos); } catch (JsonException) { corruptionReported = true; }
        if (!corruptionReported) throw new Exception("Legacy corruption was hidden");
        var examplesJson = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "puntos.json"));
        var examples = LegacyPuntoAdapter.Read(examplesJson, photos, samples: true);
        var expected = JsonDocument.Parse(examplesJson).RootElement.GetArrayLength();
        if (examples.Warnings.Count != 0 || examples.Reports.Count != expected || examples.Reports.Any(r => !r.IsDemo || r.Confirmed || r.IsShared || !r.Description.StartsWith("[Prueba]"))) throw new Exception("Original examples treated as real community observations");
        store.AccountId = "legacy-import";
        if (await store.ImportLocalAsync(examples.Reports) != expected || await store.ImportLocalAsync(examples.Reports) != 0) throw new Exception("Original examples duplicate on reload");
        if (await store.RemoveSamplesAsync() != expected || (await store.ReadAsync()).Count != 1) throw new Exception("Removing imported examples removed a real report");
        if (await File.ReadAllTextAsync(photoPath) != "original-photo-must-survive") throw new Exception("Legacy photo was overwritten or removed");
        Console.WriteLine("PASS: merge compatibility: old schema, enums, stable IDs, photo preservation, import deduplication, retained edits, account isolation, invalid-file handling and original demo dataset.");
    }
}
