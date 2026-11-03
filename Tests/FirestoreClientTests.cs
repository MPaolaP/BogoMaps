using System.Net;
using System.Text;
using System.Text.Json;
using BogoMaps.Models;
using BogoMaps.Services;

internal static class FirestoreClientTests
{
    public static async Task RunAsync()
    {
        const string prefix = "projects/demo-bogomaps/databases/(default)/documents";
        const string id = "0123456789abcdef0123456789abcdef";
        var calls = new List<(string Path, JsonElement Data)>();
        object F(string value) => new { stringValue = value };
        object Document(string docId) => new {
            name = prefix + "/bogoReports/" + docId,
            fields = new Dictionary<string, object> {
                ["ownerId"] = F("alice"), ["city"] = F("bogota"), ["category"] = F("Aceras"), ["description"] = F("Andén roto"), ["address"] = F("Calle 53"),
                ["latitude"] = new { doubleValue = 4.64 }, ["longitude"] = new { doubleValue = -74.06 }, ["severity"] = F("Moderado"),
                ["anonymous"] = new { booleanValue = false }, ["resolved"] = new { booleanValue = false }, ["lighting"] = F("Buena"), ["pavement"] = F("Deteriorado"),
                ["accessibility"] = F("Con barreras"), ["noise"] = F("Medio"), ["createdAt"] = new { timestampValue = "2026-11-02T12:00:00Z" },
                ["photo"] = F("/private/another-user/photo.jpg") // The decoder must never trust remote file paths.
            }
        };
        var status = HttpStatusCode.OK;
        string? failure = null;
        var count = 1;
        var denyCommitOnly = false;
        var missingMarkers = false;
        var handler = new Handler(async request =>
        {
            if (request.Headers.Authorization?.Scheme != "Bearer" || request.Headers.Authorization.Parameter != "user-id-token") throw new Exception("Missing user authentication");
            var body = request.Content == null ? "{}" : await request.Content.ReadAsStringAsync();
            using var parsed = JsonDocument.Parse(body);
            var data = parsed.RootElement.Clone();
            var path = request.RequestUri!.AbsolutePath;
            calls.Add((path, data));
            object response;
            var isError = failure != null && (!denyCommitOnly || path.EndsWith(":commit"));
            if (isError) response = new { error = new { status = failure, message = "Do not expose request credentials" } };
            else if (path.EndsWith(":runQuery"))
            {
                if (path.Contains("/bogoUsers/")) response = new[] { new { document = new { name = prefix + "/bogoUsers/alice/favorites/" + id,
                    fields = new { reportId = F(id), createdAt = new { timestampValue = "2026-11-02T12:00:00Z" } } } } };
                else response = Enumerable.Range(0, count).Select(i => new { document = Document(count == 1 ? id : i.ToString("x32")) }).ToArray();
            }
            else if (path.EndsWith(":batchGet")) response = data.GetProperty("documents").EnumerateArray().Select(x => x.GetString()!).Select(name =>
            {
                if (name.Contains("/bogoReports/")) return (object)new { found = Document(name.Split('/')[^1]) };
                if (!name.StartsWith(prefix + "/bogoUsers/alice/")) throw new Exception("Another user's private data requested");
                if (missingMarkers) return (object)new { missing = name };
                return (object)new { found = new { name, fields = new { reportId = F(name.Split('/')[^1]), createdAt = new { timestampValue = "2026-11-02T12:00:00Z" } } } };
            }).ToArray();
            else if (path.EndsWith(":commit")) response = new { commitTime = "2026-11-02T12:00:00Z" };
            else response = Document(id);
            return new HttpResponseMessage(isError ? status : HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json") };
        });
        var client = new FirestoreReportClient(new HttpClient(handler), "demo-bogomaps");
        var page = await client.FeedAsync("alice", "user-id-token");
        var row = page.Reports.Single();
        if (!row.IsShared || !row.Saved || !row.Confirmed || row.Photo != null || row.OwnerId != "alice") throw new Exception("Remote decoding or private state failed");
        if (calls[0].Data.GetProperty("structuredQuery").GetProperty("limit").GetInt32() != 50) throw new Exception("Unbounded feed");
        count = 50;
        var large = await client.FeedAsync("alice", "user-id-token");
        if (large.Next == null) throw new Exception("Missing pagination cursor");
        await client.FeedAsync("alice", "user-id-token", large.Next);
        var cursor = calls.Last(x => x.Path.EndsWith(":runQuery")).Data.GetProperty("structuredQuery").GetProperty("startAt");
        if (cursor.GetProperty("before").GetBoolean() || !cursor.GetProperty("values")[1].GetProperty("referenceValue").GetString()!.StartsWith(prefix + "/bogoReports/")) throw new Exception("Bad page cursor");
        await client.FeedAsync("alice", "user-id-token", mine: true);
        if (calls.Last(x => x.Path.EndsWith(":runQuery")).Data.GetProperty("structuredQuery").GetProperty("where").GetProperty("fieldFilter").GetProperty("field").GetProperty("fieldPath").GetString() != "ownerId") throw new Exception("Wrong own-report query");
        await client.FavoritesAsync("alice", "user-id-token");
        var report = new PedestrianReport { Id = id, Description = "Andén roto", Address = "Calle 53", Latitude = 4.64, Longitude = -74.06, Photo = "/private/photo.jpg", Saved = true, Confirmed = true };
        await client.PublishAsync(report, "alice", "user-id-token");
        var write = calls[^1].Data.GetProperty("writes")[0];
        var payload = write.GetProperty("update").GetProperty("fields");
        if (payload.TryGetProperty("photo", out _) || payload.TryGetProperty("saved", out _) || payload.TryGetProperty("confirmed", out _) || payload.TryGetProperty("IsDemo", out _)) throw new Exception("Private data published");
        if (payload.GetProperty("ownerId").GetProperty("stringValue").GetString() != "alice" || write.GetProperty("currentDocument").GetProperty("exists").GetBoolean()) throw new Exception("Unsafe ownership/create semantics");
        var before = calls.Count; report.IsDemo = true;
        try { await client.PublishAsync(report, "alice", "user-id-token"); throw new Exception("Demo published"); } catch (ArgumentException) { }
        if (calls.Count != before) throw new Exception("Demo reached server");
        await client.SetFavoriteAsync(id, "alice", "user-id-token", true);
        if (!calls[^1].Data.GetProperty("writes")[0].GetProperty("update").GetProperty("name").GetString()!.Contains("/bogoUsers/alice/favorites/")) throw new Exception("Favorite not scoped to user");
        await client.ConfirmAsync(id, "alice", "user-id-token");
        if (calls[^1].Data.GetProperty("writes")[0].GetProperty("currentDocument").GetProperty("exists").GetBoolean()) throw new Exception("Confirmation overwrite allowed");
        await client.ResolveAsync(id, "user-id-token", true);
        if (calls[^1].Data.GetProperty("writes")[0].GetProperty("updateMask").GetProperty("fieldPaths").GetArrayLength() != 1) throw new Exception("Resolver may change arbitrary report fields");
        status = HttpStatusCode.Forbidden; failure = "PERMISSION_DENIED"; denyCommitOnly = true;
        await client.ConfirmAsync(id, "alice", "user-id-token"); // Existing own marker makes a duplicate confirmation idempotent.
        missingMarkers = true;
        try { await client.ConfirmAsync(id, "alice", "user-id-token"); throw new Exception("Denied confirmation was reported as saved"); }
        catch (CommunityException ex) when (ex.Code == "PERMISSION_DENIED") { }
        denyCommitOnly = false;
        try { await client.FeedAsync("alice", "user-id-token"); throw new Exception("Denied operation accepted"); }
        catch (CommunityException ex) when (ex.Code == "PERMISSION_DENIED" && !ex.Message.Contains("credentials")) { }
        Console.WriteLine("PASS: Firestore REST: authenticated bounded queries, cursors, private state, favorites, confirmation preconditions, ownership, demo exclusion, private-field exclusion and denied access (mock HTTP).");
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handle(request);
    }
}
