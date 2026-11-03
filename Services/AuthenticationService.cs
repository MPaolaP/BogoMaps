using System.Text.Json;
namespace BogoMaps.Services;

public sealed class AuthenticationService
{
    public static AuthenticationService Instance { get; } = new();
    private const string SessionKey = "firebase.session.v1";
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SemaphoreSlim stateGate = new(1, 1);
    private FirebaseAuthClient? client;
    private string? webClientId;
    public FirebaseSession? Current { get; private set; }
    public string ProfileKey(string field) => Current == null ? $"profile.{field}" : $"profile.{Current.UserId}.{field}";
    private async Task<FirebaseAuthClient> ClientAsync()
    {
        if (client != null) return client;
        using var stream = await FileSystem.OpenAppPackageFileAsync("firebase-client.json");
        using var config = await JsonDocument.ParseAsync(stream);
        webClientId = config.RootElement.GetProperty("WebClientId").GetString();
        return client = new FirebaseAuthClient(http, config.RootElement.GetProperty("ApiKey").GetString()!);
    }
    public async Task SignInAsync(string email, string password, bool register = false)
    {
        await stateGate.WaitAsync();
        try { await AcceptAsync(await (await ClientAsync()).SignInAsync(email, password, register)); }
        finally { stateGate.Release(); }
    }
    public async Task ResetAsync(string email) => await (await ClientAsync()).ResetAsync(email);
    public async Task GoogleAsync()
    {
        await stateGate.WaitAsync();
        try
        {
            var firebase = await ClientAsync();
#if ANDROID
            var idToken = await Platforms.Android.GoogleAccountSignIn.SignInAsync(webClientId!);
            await AcceptAsync(await firebase.GoogleAsync(idToken));
#elif WINDOWS
            throw new PlatformNotSupportedException("Google en Windows aún no está implementado. Puedes usar correo/contraseña o explorar como invitado.");
#else
            throw new PlatformNotSupportedException("Google en esta plataforma requiere su configuración nativa.");
#endif
        }
        finally { stateGate.Release(); }
    }
    private async Task AcceptAsync(FirebaseSession session)
    {
        await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(session));
        Current = session;
        LocalRepository.Instance.AccountId = session.UserId;
    }
    public async Task<(string UserId, string Token)> IdentityAsync()
    {
        await stateGate.WaitAsync();
        try
        {
            var session = Current ?? throw new InvalidOperationException("Inicia sesión para usar los reportes comunitarios.");
            if (session.IdToken.Length == 0 || session.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
            {
                try { await AcceptAsync(await (await ClientAsync()).RefreshAsync(session)); }
                catch (FirebaseAuthException ex) when (SessionExpired(ex.Code)) { SignOutCore(); throw; }
            }
            return (Current!.UserId, Current.IdToken);
        }
        finally { stateGate.Release(); }
    }
    public async Task<bool> RestoreAsync()
    {
        await stateGate.WaitAsync();
        try
        {
            var stored = await SecureStorage.Default.GetAsync(SessionKey);
            if (stored == null) return false;
            try
            {
                var previous = JsonSerializer.Deserialize<FirebaseSession>(stored) ?? throw new JsonException();
                await AcceptAsync(await (await ClientAsync()).RefreshAsync(previous));
                return true;
            }
            catch (FirebaseAuthException ex) when (SessionExpired(ex.Code)) { SignOutCore(); return false; }
            catch (JsonException) { SignOutCore(); return false; }
        }
        finally { stateGate.Release(); }
    }
    private static bool SessionExpired(string code) => code is "INVALID_REFRESH_TOKEN" or "TOKEN_EXPIRED" or "USER_NOT_FOUND" or "USER_DISABLED" or "INVALID_GRANT";
    public async Task SignOutAsync()
    {
        await stateGate.WaitAsync();
        try { SignOutCore(); }
        finally { stateGate.Release(); }
    }
    private void SignOutCore()
    {
        SecureStorage.Default.Remove(SessionKey);
        Current = null;
        LocalRepository.Instance.AccountId = null;
    }
}
