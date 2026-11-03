using System.Text.Json;
using BogoMaps.Models;
namespace BogoMaps.Services;

public sealed class ReportData
{
    public static ReportData Instance { get; } = new();
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private FirestoreReportClient? cloud;
    private readonly SemaphoreSlim mutationGate = new(1, 1);
    public string Notice { get; private set; } = "";
    private async Task<FirestoreReportClient> CloudAsync()
    {
        if (cloud != null) return cloud;
        using var input = await FileSystem.OpenAppPackageFileAsync("firebase-client.json");
        using var data = await JsonDocument.ParseAsync(input);
        return cloud = new FirestoreReportClient(http, data.RootElement.GetProperty("ProjectId").GetString()!,
            data.RootElement.TryGetProperty("FirestoreDatabaseId", out var db) ? db.GetString()! : "(default)");
    }
    private static void SameAccount(string uid)
    {
        if (AuthenticationService.Instance.Current?.UserId != uid) throw new OperationCanceledException("La cuenta cambió durante la operación.");
    }
    private static void SameScope(string? uid)
    {
        if (AuthenticationService.Instance.Current?.UserId != uid) throw new OperationCanceledException("La cuenta cambió durante la operación.");
    }
    public async Task<List<PedestrianReport>> ReadAsync()
    {
        var uid = AuthenticationService.Instance.Current?.UserId;
        var local = await LocalRepository.Instance.ReadAsync();
        SameScope(uid);
        if (AuthenticationService.Instance.Current == null) return local;
        var shared = await LocalRepository.Instance.ReadCommunityCacheAsync();
        SameScope(uid);
        return local.Concat(shared).GroupBy(r => r.Id).Select(g => g.Last()).ToList();
    }
    private async Task CacheAsync(string uid, List<PedestrianReport> reports)
    {
        SameAccount(uid);
        var cached = await LocalRepository.Instance.ReadCommunityCacheAsync();
        SameAccount(uid);
        foreach (var report in reports)
            if (report.OwnerId == uid) report.Photo ??= cached.FirstOrDefault(r => r.Id == report.Id)?.Photo;
        await LocalRepository.Instance.CacheCommunityAsync(reports);
    }
    public async Task<ReportPage> FeedAsync(ReportCursor? after = null, bool mine = false, bool favorites = false)
    {
        var scope = AuthenticationService.Instance.Current?.UserId;
        var local = await LocalRepository.Instance.ReadAsync();
        SameScope(scope);
        if (scope == null)
        {
            Notice = "Modo invitado: reportes locales. Inicia sesión para consultar y publicar en la comunidad.";
            return new(local.Where(r => !favorites || r.Saved).ToList(), null);
        }
        (string UserId, string Token) identity;
        try { identity = await AuthenticationService.Instance.IdentityAsync(); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            SameScope(scope);
            Notice = "Sin conexión para renovar la sesión. Se muestra la copia local disponible.";
            var cached = await ReadAsync();
            return new(cached.Where(r => (!mine || !r.IsShared || r.OwnerId == scope) && (!favorites || r.Saved)).ToList(), null);
        }
        if (identity.UserId != scope) throw new OperationCanceledException("La cuenta cambió durante la operación.");
        try
        {
            var client = await CloudAsync();
            var page = favorites ? await client.FavoritesAsync(identity.UserId, identity.Token, after) : await client.FeedAsync(identity.UserId, identity.Token, after, mine);
            SameAccount(identity.UserId);
            await CacheAsync(identity.UserId, page.Reports);
            Notice = "Comunidad actualizada. Los ejemplos y los reportes privados permanecen locales.";
            if (after == null) page.Reports.InsertRange(0, local.Where(r => !favorites || r.Saved));
            return page;
        }
        catch (Exception ex) when (ex is CommunityException or HttpRequestException or TaskCanceledException)
        {
            SameAccount(identity.UserId);
            Notice = ex is CommunityException ? ex.Message : "Sin conexión con la comunidad. Se muestra la copia local disponible; los cambios compartidos necesitan conexión.";
            var cached = await ReadAsync();
            return new(cached.Where(r => (!mine || !r.IsShared || r.OwnerId == identity.UserId) && (!favorites || r.Saved)).ToList(), null);
        }
    }
    public async Task<PedestrianReport?> GetAsync(string id)
    {
        var scope = AuthenticationService.Instance.Current?.UserId;
        var local = await LocalRepository.Instance.ReadAsync();
        SameScope(scope);
        var own = local.FirstOrDefault(r => r.Id == id);
        if (own != null) return own;
        (string UserId, string Token) identity;
        try { identity = await AuthenticationService.Instance.IdentityAsync(); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            SameScope(scope);
            var cached = (await LocalRepository.Instance.ReadCommunityCacheAsync()).FirstOrDefault(r => r.Id == id);
            SameScope(scope);
            if (cached == null) throw;
            Notice = "Copia local: no fue posible renovar la sesión sin conexión.";
            return cached;
        }
        if (identity.UserId != scope) throw new OperationCanceledException("La cuenta cambió durante la operación.");
        try
        {
            var report = await (await CloudAsync()).GetAsync(id, identity.UserId, identity.Token);
            await CacheAsync(identity.UserId, [report]);
            Notice = "Reporte actualizado desde la comunidad.";
            return report;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException || ex is CommunityException { Code: not "NOT_FOUND" })
        {
            SameAccount(identity.UserId);
            var cached = (await LocalRepository.Instance.ReadCommunityCacheAsync()).FirstOrDefault(r => r.Id == id);
            if (cached == null) throw;
            Notice = ex is CommunityException ? ex.Message + " Se muestra una copia local." : "Copia local: no fue posible consultar el estado actual de la comunidad.";
            return cached;
        }
    }
    public async Task PublishAsync(PedestrianReport report)
    {
        var identity = await AuthenticationService.Instance.IdentityAsync();
        if (report.IsDemo) throw new ArgumentException("Los ejemplos no se publican.");
        var client = await CloudAsync();
        try { await client.PublishAsync(report, identity.UserId, identity.Token); }
        catch (CommunityException ex) when (ex.Code == "ALREADY_EXISTS")
        {
            var existing = await client.GetAsync(report.Id, identity.UserId, identity.Token);
            if (existing.OwnerId != identity.UserId || existing.Description != report.Description || existing.Address != report.Address ||
                existing.Latitude != report.Latitude || existing.Longitude != report.Longitude || existing.Category != report.Category || existing.Severity != report.Severity ||
                existing.Lighting != report.Lighting || existing.Pavement != report.Pavement || existing.Accessibility != report.Accessibility || existing.Noise != report.Noise || existing.Anonymous != report.Anonymous)
                throw new CommunityException("Este borrador ya se publicó con otros datos. Revísalo en Explorar antes de crear otro reporte.");
        }
        SameAccount(identity.UserId);
        Notice = "Reporte publicado en la comunidad.";
        try
        {
            var published = await client.GetAsync(report.Id, identity.UserId, identity.Token);
            published.Photo = report.Photo;
            await CacheAsync(identity.UserId, [published]);
        }
        catch (Exception ex) when (ex is CommunityException or IOException or HttpRequestException or TaskCanceledException or JsonException)
        { Notice = "Reporte publicado. No fue posible actualizar la copia local; pulsa Actualizar comunidad cuando haya conexión."; }
    }
    public async Task<PedestrianReport> ChangeAsync(PedestrianReport report, string action)
    {
        await mutationGate.WaitAsync();
        try
        {
            var copy = JsonSerializer.Deserialize<PedestrianReport>(JsonSerializer.Serialize(report))!;
            if (!copy.IsShared)
            {
                var scope = AuthenticationService.Instance.Current?.UserId;
                copy = (await LocalRepository.Instance.ReadAsync()).FirstOrDefault(r => r.Id == report.Id) ?? throw new InvalidOperationException("El reporte no pertenece al almacenamiento de esta sesión.");
                SameScope(scope);
                if (action == "save") copy.Saved = !copy.Saved;
                else if (action == "confirm") { copy.Confirmed = true; copy.ConfirmedAt = DateTimeOffset.UtcNow; }
                else if (action == "resolve") copy.Resolved = !copy.Resolved;
                else throw new ArgumentException("Acción no válida.");
                await LocalRepository.Instance.SaveAsync(copy);
                Notice = "Cambio guardado en este dispositivo.";
                return copy;
            }
            var identity = await AuthenticationService.Instance.IdentityAsync();
            var client = await CloudAsync();
            if (action == "save") { await client.SetFavoriteAsync(copy.Id, identity.UserId, identity.Token, !copy.Saved); copy.Saved = !copy.Saved; }
            else if (action == "confirm") { await client.ConfirmAsync(copy.Id, identity.UserId, identity.Token); copy.Confirmed = true; copy.ConfirmedAt = DateTimeOffset.UtcNow; }
            else if (action == "resolve")
            {
                if (copy.OwnerId != identity.UserId) throw new InvalidOperationException("Solo el autor puede resolver o reabrir este reporte.");
                await client.ResolveAsync(copy.Id, identity.Token, !copy.Resolved); copy.Resolved = !copy.Resolved;
            }
            else throw new ArgumentException("Acción no válida.");
            SameAccount(identity.UserId);
            // A confirmed remote write remains successful if the local cache is unavailable.
            try { await CacheAsync(identity.UserId, [copy]); Notice = "Cambio guardado en la comunidad."; }
            catch (Exception ex) when (ex is IOException or JsonException) { Notice = "Cambio guardado en la comunidad; la copia local no pudo actualizarse."; }
            return copy;
        }
        finally { mutationGate.Release(); }
    }
}
