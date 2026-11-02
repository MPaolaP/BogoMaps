using BogoMaps.Models;
using BogoMaps.Services;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "bogomaps-tests-" + Guid.NewGuid());
Directory.CreateDirectory(root);
FileSystem.AppDataDirectory = root;
var store = new LocalRepository();
var count = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; }
PedestrianReport Valid() => new() { Description = "Andén deteriorado", Address = "Calle 53", Latitude = 4.65, Longitude = -74.07 };
try
{
    Check((await store.ReadAsync()).Count == 0, "Fresh store must be empty");
    var first = Valid(); first.Anonymous = true; first.Lighting = "Buena"; first.Pavement = "Deteriorado"; first.Accessibility = "Con barreras"; first.Noise = "Alto";
    await store.SaveAsync(first);
    var restarted = new LocalRepository();
    var restored = (await restarted.ReadAsync()).Single();
    Check(restored.Id == first.Id && restored.Anonymous && restored.Latitude == first.Latitude, "Report must survive restart");
    Check(restored.Lighting == "Buena" && restored.Pavement == "Deteriorado" && restored.Accessibility == "Con barreras" && restored.Noise == "Alto", "Point assessment must survive restart");
    var legacy = JsonSerializer.Deserialize<PedestrianReport>("{\"Description\":\"Reporte anterior\"}")!;
    Check(legacy.Lighting == "Sin evaluar" && legacy.Accessibility == "Sin evaluar", "Legacy reports must show unknown assessments, not fabricated metrics");
    restored.Saved = restored.Confirmed = restored.Resolved = true;
    restored.ConfirmedAt = DateTimeOffset.UtcNow;
    await restarted.SaveAsync(restored);
    var edited = (await store.ReadAsync()).Single();
    Check(edited.Saved && edited.Confirmed && edited.Resolved && edited.ConfirmedAt.HasValue, "State must persist without duplicating reports");
    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.SaveAsync(Valid())));
    Check((await store.ReadAsync()).Count == 9, "Concurrent writes must not lose reports");
    var missingDescription = Valid(); missingDescription.Description = " ";
    var outsideBogota = Valid(); outsideBogota.Latitude = 40;
    var tooLong = Valid(); tooLong.Description = new string('x', 251);
    var missingAddress = Valid(); missingAddress.Address = "";
    var notFinite = Valid(); notFinite.Latitude = double.NaN;
    var invalidCategory = Valid(); invalidCategory.Category = "Unknown";
    foreach (var invalid in new[] { missingDescription, outsideBogota, tooLong, missingAddress, notFinite, invalidCategory })
    {
        var rejected = false;
        try { await store.SaveAsync(invalid); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Invalid report must be rejected");
    }
    Check((await store.ReadAsync()).Count == 9, "Validation failure must preserve store");
    store.AccountId = "user-a";
    Check((await store.ReadAsync()).Count == 0, "Account must not inherit guest reports");
    await store.SaveAsync(Valid());
    store.AccountId = "user-b";
    Check((await store.ReadAsync()).Count == 0, "Accounts must be isolated");
    store.AccountId = "user-a";
    Check((await store.ReadAsync()).Count == 1, "Account data must survive account switches");
    store.AccountId = null;
    Check((await store.ReadAsync()).Count == 9, "Guest data must remain intact");
    var seedResults = await Task.WhenAll(store.LoadSamplesAsync(), store.LoadSamplesAsync());
    Check(seedResults.Sum() == 6 && (await store.ReadAsync()).Count == 15, "Concurrent seeding must add six examples only once");
    var samples = (await store.ReadAsync()).Where(r => r.IsDemo).ToList();
    Check(samples.Count == 6 && samples.Select(r => r.Category).Distinct().Count() == 6, "Samples must cover all categories");
    Check(samples.Count(r => r.Saved) == 3 && samples.Count(r => r.Resolved) == 2 && samples.Count(r => r.Confirmed) == 2, "Samples must exercise saved, resolved and confirmed views");
    var changedSample = samples[0]; changedSample.Description = "Ejemplo editado";
    await store.SaveAsync(changedSample);
    Check(await store.LoadSamplesAsync() == 0 && (await store.ReadAsync()).Single(r => r.Id == changedSample.Id).Description == "Ejemplo editado", "Reloading samples must preserve edits");
    store.AccountId = "user-a";
    Check((await store.ReadAsync()).Count == 1, "Guest samples must not appear in another account");
    Check(await store.LoadSamplesAsync() == 6 && await store.RemoveSamplesAsync() == 6 && (await store.ReadAsync()).Count == 1, "Removing account samples must preserve its own reports");
    store.AccountId = null;
    Check(await store.RemoveSamplesAsync() == 6 && (await store.ReadAsync()).Count == 9, "Sample removal must preserve all real guest reports");
    Check(await store.RemoveSamplesAsync() == 0, "Repeated sample removal must be harmless");
    store.AccountId = "collision-test";
    var collision = Valid(); collision.Id = "sample-v1-1";
    await store.SaveAsync(collision);
    Check(await store.LoadSamplesAsync() == 5, "Seeding must not replace a real report with a conflicting ID");
    Check(await store.RemoveSamplesAsync() == 5 && (await store.ReadAsync()).Single().Description == collision.Description, "Removal must not delete a real report with a sample-like ID");
    store.AccountId = "sample-validation";
    foreach (var sample in SampleReports.Create()) await store.SaveAsync(sample);
    Check((await store.ReadAsync()).Count == 6, "Every sample must pass normal report validation");
    store.AccountId = null;
    await AuthClientTests.RunAsync();
    var path = Path.Combine(root, "reports-v1.json");
    await File.WriteAllTextAsync(path, "corrupted data");
    var corruptionReported = false;
    try { await store.SaveAsync(Valid()); } catch (JsonException) { corruptionReported = true; }
    Check(corruptionReported && await File.ReadAllTextAsync(path) == "corrupted data", "Corrupt data must never be overwritten silently");
    foreach (var operation in new Func<Task<int>>[] { store.LoadSamplesAsync, store.RemoveSamplesAsync })
    {
        var sampleCorruptionReported = false;
        try { await operation(); } catch (JsonException) { sampleCorruptionReported = true; }
        Check(sampleCorruptionReported && await File.ReadAllTextAsync(path) == "corrupted data", "Sample operations must not overwrite corrupt data");
    }
    Console.WriteLine($"PASS: {count} assertions; persistence, restart, updates, concurrent writes, validation and corruption protection.");
}
finally { Directory.Delete(root, true); }

// Minimal platform adapter for exercising the actual repository implementation on the desktop CLR.
public static class FileSystem { public static string AppDataDirectory { get; set; } = ""; }
