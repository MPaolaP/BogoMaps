using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BogoMaps.Models;

namespace BogoMaps.Services;

public sealed record ReportCursor(DateTimeOffset CreatedAt, string Id);
public sealed record ReportPage(List<PedestrianReport> Reports, ReportCursor? Next);
public sealed class CommunityException(string message, string code = "") : Exception(message) { public string Code { get; } = code; }

// Firebase user ID tokens authenticate each request; there is no admin credential in the application.
public sealed class FirestoreReportClient
{
    private readonly HttpClient http;
    private readonly string documents;
    private readonly string root;
    public FirestoreReportClient(HttpClient http, string projectId, string databaseId = "(default)")
    {
        this.http = http;
        documents = $"projects/{SafeId(projectId)}/databases/{(databaseId == "(default)" ? databaseId : SafeId(databaseId))}/documents";
        root = "https://firestore.googleapis.com/v1/" + documents;
    }
    private string ReportPath(string id) => $"{documents}/bogoReports/{SafeId(id)}";
    private string MarkerPath(string uid, string kind, string id) => $"{documents}/bogoUsers/{SafeId(uid)}/{kind}/{SafeId(id)}";
    private static string SafeId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') ? value : throw new ArgumentException("Identificador no válido.");
    private static object Text(string value) => new { stringValue = value };
    private static object Number(double value) => new { doubleValue = value };
    private static object Flag(bool value) => new { booleanValue = value };
    private static object Time(DateTimeOffset value) => new { timestampValue = value.ToUniversalTime().ToString("O") };
    private static object ServerTime(string field) => new { fieldPath = field, setToServerValue = "REQUEST_TIME" };

    public async Task<ReportPage> FeedAsync(string uid, string token, ReportCursor? after = null, bool mine = false)
    {
        var query = new Dictionary<string, object> {
            ["from"] = new[] { new { collectionId = "bogoReports" } },
            ["where"] = new { fieldFilter = new { field = new { fieldPath = mine ? "ownerId" : "city" }, op = "EQUAL", value = Text(mine ? uid : "bogota") } },
            ["orderBy"] = new[] { new { field = new { fieldPath = "createdAt" }, direction = "DESCENDING" }, new { field = new { fieldPath = "__name__" }, direction = "DESCENDING" } },
            ["limit"] = 50
        };
        if (after != null) query["startAt"] = new { values = new object[] { Time(after.CreatedAt), new { referenceValue = ReportPath(after.Id) } }, before = false };
        var response = await SendAsync(HttpMethod.Post, root + ":runQuery", token, new { structuredQuery = query });
        var rows = response.EnumerateArray().Where(x => x.TryGetProperty("document", out _)).Select(x => Decode(x.GetProperty("document"))).ToList();
        await PersonalizeAsync(rows, uid, token);
        var last = rows.LastOrDefault();
        return new(rows, rows.Count == 50 && last != null ? new(last.CreatedAt, last.Id) : null);
    }
    public async Task<PedestrianReport> GetAsync(string id, string uid, string token)
    {
        var report = Decode(await SendAsync(HttpMethod.Get, "https://firestore.googleapis.com/v1/" + ReportPath(id), token));
        await PersonalizeAsync([report], uid, token);
        return report;
    }
    public async Task<ReportPage> FavoritesAsync(string uid, string token, ReportCursor? after = null)
    {
        var query = new Dictionary<string, object> {
            ["from"] = new[] { new { collectionId = "favorites" } },
            ["orderBy"] = new[] { new { field = new { fieldPath = "createdAt" }, direction = "DESCENDING" }, new { field = new { fieldPath = "__name__" }, direction = "DESCENDING" } }, ["limit"] = 50
        };
        if (after != null) query["startAt"] = new { values = new object[] { Time(after.CreatedAt), new { referenceValue = MarkerPath(uid, "favorites", after.Id) } }, before = false };
        var data = await SendAsync(HttpMethod.Post, root + "/bogoUsers/" + SafeId(uid) + ":runQuery", token, new { structuredQuery = query });
        var markers = data.EnumerateArray().Where(x => x.TryGetProperty("document", out _)).Select(x => x.GetProperty("document")).ToList();
        var ids = markers.Select(d => d.GetProperty("fields").GetProperty("reportId").GetProperty("stringValue").GetString()!).ToList();
        var found = ids.Count == 0 ? [] : await BatchAsync(ids.Select(ReportPath).ToArray(), token);
        var reports = found.Values.Select(Decode).OrderBy(r => ids.IndexOf(r.Id)).ToList();
        await PersonalizeAsync(reports, uid, token);
        var last = markers.LastOrDefault();
        return new(reports, markers.Count == 50 ? new(DateTimeOffset.Parse(last.GetProperty("fields").GetProperty("createdAt").GetProperty("timestampValue").GetString()!), ids[^1]) : null);
    }
    public async Task PublishAsync(PedestrianReport report, string uid, string token)
    {
        LocalRepository.Validate(report);
        if (report.IsDemo || report.Id.StartsWith("sample-", StringComparison.Ordinal) || !Guid.TryParseExact(report.Id, "N", out _))
            throw new ArgumentException("Los datos de prueba no se publican en la comunidad.");
        var fields = new Dictionary<string, object> {
            ["ownerId"] = Text(uid), ["city"] = Text("bogota"), ["category"] = Text(report.Category),
            ["description"] = Text(report.Description), ["address"] = Text(report.Address), ["latitude"] = Number(report.Latitude),
            ["longitude"] = Number(report.Longitude), ["severity"] = Text(report.Severity), ["anonymous"] = Flag(report.Anonymous),
            ["resolved"] = Flag(false), ["lighting"] = Text(report.Lighting), ["pavement"] = Text(report.Pavement),
            ["accessibility"] = Text(report.Accessibility), ["noise"] = Text(report.Noise)
        };
        // Explicit whitelist: never send local photo paths, favorites, tokens or demo flags.
        await CommitAsync(token, new { update = new { name = ReportPath(report.Id), fields },
            updateTransforms = new[] { ServerTime("createdAt"), ServerTime("updatedAt") }, currentDocument = new { exists = false } });
    }
    public async Task SetFavoriteAsync(string id, string uid, string token, bool saved)
    {
        var name = MarkerPath(uid, "favorites", id);
        if (!saved) { await CommitAsync(token, new { delete = name }); return; }
        await CommitAsync(token, new { update = new { name, fields = new { reportId = Text(id) } }, updateTransforms = new[] { ServerTime("createdAt") } });
    }
    public async Task ConfirmAsync(string id, string uid, string token)
    {
        try
        {
            await CommitAsync(token, new { update = new { name = MarkerPath(uid, "confirmations", id), fields = new { reportId = Text(id) } },
                updateTransforms = new[] { ServerTime("createdAt") }, currentDocument = new { exists = false } });
        }
        catch (CommunityException ex) when (ex.Code is "ALREADY_EXISTS" or "PERMISSION_DENIED")
        {
            var name = MarkerPath(uid, "confirmations", id);
            if (!(await BatchAsync([name], token)).ContainsKey(name)) throw;
        }
    }
    public Task ResolveAsync(string id, string token, bool resolved) => CommitAsync(token, new {
        update = new { name = ReportPath(id), fields = new { resolved = Flag(resolved) } }, updateMask = new { fieldPaths = new[] { "resolved" } },
        updateTransforms = new[] { ServerTime("updatedAt") }, currentDocument = new { exists = true }
    });
    private async Task PersonalizeAsync(List<PedestrianReport> reports, string uid, string token)
    {
        if (reports.Count == 0) return;
        var names = reports.SelectMany(r => new[] { MarkerPath(uid, "favorites", r.Id), MarkerPath(uid, "confirmations", r.Id) }).ToArray();
        var found = await BatchAsync(names, token);
        foreach (var report in reports)
        {
            report.Saved = found.ContainsKey(MarkerPath(uid, "favorites", report.Id));
            report.Confirmed = found.TryGetValue(MarkerPath(uid, "confirmations", report.Id), out var confirmation);
            if (report.Confirmed) report.ConfirmedAt = DateTimeOffset.Parse(confirmation.GetProperty("fields").GetProperty("createdAt").GetProperty("timestampValue").GetString()!);
        }
    }
    private async Task<Dictionary<string, JsonElement>> BatchAsync(string[] names, string token)
    {
        var data = await SendAsync(HttpMethod.Post, root + ":batchGet", token, new { documents = names });
        return data.EnumerateArray().Where(x => x.TryGetProperty("found", out _)).Select(x => x.GetProperty("found")).ToDictionary(x => x.GetProperty("name").GetString()!, x => x.Clone());
    }
    private async Task CommitAsync(string token, object write) => await SendAsync(HttpMethod.Post, root + ":commit", token, new { writes = new[] { write } });
    private async Task<JsonElement> SendAsync(HttpMethod method, string uri, string token, object? payload = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (payload != null) request.Content = JsonContent.Create(payload);
        using var response = await http.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var code = response.StatusCode switch { HttpStatusCode.Forbidden => "PERMISSION_DENIED", HttpStatusCode.NotFound => "NOT_FOUND", HttpStatusCode.Unauthorized => "UNAUTHENTICATED", _ => "UNKNOWN" };
            try { using var error = JsonDocument.Parse(content); var value = error.RootElement.GetProperty("error").GetProperty("status").GetString(); if (value != null && value.Length <= 64 && value.All(c => char.IsAsciiLetterUpper(c) || c == '_')) code = value; } catch (JsonException) { } catch (KeyNotFoundException) { }
            throw new CommunityException(code switch {
                "PERMISSION_DENIED" => "Firestore rechazó el acceso. Publica las reglas de BogoMaps en la base correcta e inicia sesión.",
                "NOT_FOUND" => "El reporte o la base de Firestore no existe. Comprueba que la base se llame (default), o configura su identificador.",
                "FAILED_PRECONDITION" => "Firestore necesita los índices de BogoMaps o no se cumple una condición de la escritura. Revisa firebase/firestore.indexes.json.",
                "UNAUTHENTICATED" => "La sesión no es válida para Firestore. Vuelve a iniciar sesión.",
                _ => $"No se pudo completar la operación comunitaria ({code}). No se ha confirmado el cambio."
            }, code);
        }
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }
    private static PedestrianReport Decode(JsonElement document)
    {
        var fields = document.GetProperty("fields");
        string S(string key) => fields.TryGetProperty(key, out var v) && v.TryGetProperty("stringValue", out var text) ? text.GetString() ?? "" : "";
        bool B(string key) => fields.TryGetProperty(key, out var v) && v.TryGetProperty("booleanValue", out var flag) && flag.GetBoolean();
        double N(string key) { var v = fields.GetProperty(key); return v.TryGetProperty("doubleValue", out var n) ? n.GetDouble() : double.Parse(v.GetProperty("integerValue").GetString()!, System.Globalization.CultureInfo.InvariantCulture); }
        var report = new PedestrianReport { Id = document.GetProperty("name").GetString()!.Split('/')[^1], IsShared = true, OwnerId = S("ownerId"),
            Category = S("category"), Description = S("description"), Address = S("address"), Latitude = N("latitude"), Longitude = N("longitude"),
            Severity = S("severity"), Anonymous = B("anonymous"), Resolved = B("resolved"), Lighting = S("lighting"), Pavement = S("pavement"), Accessibility = S("accessibility"), Noise = S("noise"),
            CreatedAt = DateTimeOffset.Parse(fields.GetProperty("createdAt").GetProperty("timestampValue").GetString()!) };
        LocalRepository.Validate(report);
        if (!Guid.TryParseExact(report.Id, "N", out _) || string.IsNullOrWhiteSpace(report.OwnerId)) throw new InvalidDataException("Reporte remoto no válido.");
        return report;
    }
}
