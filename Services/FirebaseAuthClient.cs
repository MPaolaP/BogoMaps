using System.Net.Http.Json;
using System.Text.Json;

namespace BogoMaps.Services;

public sealed record FirebaseSession(string UserId, string Email, string RefreshToken);

// Firebase REST validates credentials and Google ID tokens server-side. Never accept decoded JWT claims as a session.
public sealed class FirebaseAuthClient(HttpClient http, string apiKey)
{
    public async Task<FirebaseSession> SignInAsync(string email, string password, bool register = false)
    {
        ValidateEmail(email);
        if (string.IsNullOrEmpty(password) || (register && password.Length < 6))
            throw new ArgumentException("La contraseña debe tener al menos 6 caracteres para crear una cuenta.");
        return Session(await PostAsync(register ? "signUp" : "signInWithPassword", new { email = email.Trim(), password, returnSecureToken = true }));
    }

    public async Task<FirebaseSession> GoogleAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken)) throw new ArgumentException("Google no entregó una credencial válida.");
        var body = await new FormUrlEncodedContent(new Dictionary<string, string> { ["id_token"] = idToken, ["providerId"] = "google.com" }).ReadAsStringAsync();
        return Session(await PostAsync("signInWithIdp", new { postBody = body, requestUri = "http://localhost", returnSecureToken = true }));
    }

    public async Task ResetAsync(string email)
    {
        ValidateEmail(email);
        try { await PostAsync("sendOobCode", new { requestType = "PASSWORD_RESET", email = email.Trim() }); }
        catch (FirebaseAuthException ex) when (ex.Code == "EMAIL_NOT_FOUND") { /* Avoid disclosing account existence. */ }
    }

    public async Task<FirebaseSession> RefreshAsync(FirebaseSession previous)
    {
        using var response = await http.PostAsync($"https://securetoken.googleapis.com/v1/token?key={Uri.EscapeDataString(apiKey)}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["refresh_token"] = previous.RefreshToken }));
        var data = await ReadAsync(response);
        var userId = data.GetProperty("user_id").GetString()!;
        if (userId != previous.UserId) throw new InvalidDataException("La sesión no corresponde a la cuenta guardada.");
        return previous with { RefreshToken = data.GetProperty("refresh_token").GetString()! };
    }

    private async Task<JsonElement> PostAsync(string action, object payload)
    {
        using var response = await http.PostAsJsonAsync($"https://identitytoolkit.googleapis.com/v1/accounts:{action}?key={Uri.EscapeDataString(apiKey)}", payload);
        return await ReadAsync(response);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!response.IsSuccessStatusCode)
        {
            var code = document.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var message) ? message.GetString()?.Split(' ')[0] : "";
            throw new FirebaseAuthException(code ?? "", code switch
            {
                "INVALID_LOGIN_CREDENTIALS" or "EMAIL_NOT_FOUND" or "INVALID_PASSWORD" => "El correo o la contraseña no son correctos.",
                "EMAIL_EXISTS" => "Ya existe una cuenta con este correo. Inicia sesión o recupera tu contraseña.",
                "USER_DISABLED" => "Esta cuenta está deshabilitada.",
                "TOO_MANY_ATTEMPTS_TRY_LATER" => "Demasiados intentos. Espera un momento e inténtalo de nuevo.",
                "OPERATION_NOT_ALLOWED" => "Este método de acceso no está habilitado en Firebase.",
                "INVALID_REFRESH_TOKEN" or "TOKEN_EXPIRED" or "USER_NOT_FOUND" or "INVALID_GRANT" => "La sesión ha caducado. Inicia sesión de nuevo.",
                "INVALID_EMAIL" => "Introduce un correo electrónico válido.",
                "WEAK_PASSWORD" => "Usa una contraseña de al menos 6 caracteres.",
                _ => "No se pudo completar el acceso. Comprueba la conexión y la configuración de Firebase."
            });
        }
        return document.RootElement.Clone();
    }

    private static FirebaseSession Session(JsonElement data) => new(data.GetProperty("localId").GetString()!,
        data.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "", data.GetProperty("refreshToken").GetString()!);

    private static void ValidateEmail(string email)
    {
        if (!System.Net.Mail.MailAddress.TryCreate(email?.Trim(), out var address) || address.Address != email?.Trim())
            throw new ArgumentException("Introduce un correo electrónico válido. El acceso con celular aún no está habilitado.");
    }
}

public sealed class FirebaseAuthException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
