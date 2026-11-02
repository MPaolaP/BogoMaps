using System.Text.Json;
namespace BogoMaps.Services;

public sealed class AuthenticationService
{
    public static AuthenticationService Instance { get; } = new();
    private const string SessionKey = "firebase.session.v1";
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };
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
    public async Task SignInAsync(string email, string password, bool register = false) =>
        await AcceptAsync(await (await ClientAsync()).SignInAsync(email, password, register));
    public async Task ResetAsync(string email) => await (await ClientAsync()).ResetAsync(email);
    public async Task GoogleAsync()
    {
        var firebase = await ClientAsync();
#if ANDROID
        var idToken = await Platforms.Android.GoogleAccountSignIn.SignInAsync(webClientId!);
        await AcceptAsync(await firebase.GoogleAsync(idToken));
#else
        throw new PlatformNotSupportedException("El archivo recibido configura Android. Google en esta plataforma requiere su configuración nativa.");
#endif
    }
    private async Task AcceptAsync(FirebaseSession session)
    {
        // Do not expose an authenticated session if secure persistence fails.
        await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(session));
        Current = session;
        LocalRepository.Instance.AccountId = session.UserId;
    }
    public async Task<bool> RestoreAsync()
    {
        var stored = await SecureStorage.Default.GetAsync(SessionKey);
        if (stored == null) return false;
        try
        {
            var previous = JsonSerializer.Deserialize<FirebaseSession>(stored) ?? throw new JsonException();
            await AcceptAsync(await (await ClientAsync()).RefreshAsync(previous));
            return true;
        }
        catch (FirebaseAuthException ex) when (ex.Code is "INVALID_REFRESH_TOKEN" or "TOKEN_EXPIRED" or "USER_NOT_FOUND" or "USER_DISABLED" or "INVALID_GRANT")
        { SignOut(); return false; }
        catch (JsonException) { SignOut(); return false; }
    }
    public void SignOut()
    {
        SecureStorage.Default.Remove(SessionKey);
        Current = null;
        LocalRepository.Instance.AccountId = null;
    }
}
