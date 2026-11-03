using System.Net.Http.Json;
using System.Text.Json;

namespace BogoMaps.Services;

public sealed record FirebaseSession(string UserId, string Email, string RefreshToken,
    [property: System.Text.Json.Serialization.JsonIgnore] string IdToken = "",
    [property: System.Text.Json.Serialization.JsonIgnore] DateTimeOffset ExpiresAt = default);

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
        return previous with { RefreshToken = data.GetProperty("refresh_token").GetString()!,
            IdToken = data.GetProperty("id_token").GetString()!, ExpiresAt = Expiry(data, "expires_in") };
    }

    private async Task<JsonElement> PostAsync(string action, object payload)
    {
        using var response = await http.PostAsJsonAsync($"https://identitytoolkit.googleapis.com/v1/accounts:{action}?key={Uri.EscapeDataString(apiKey)}", payload);
        return await ReadAsync(response, action);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, string? action = null)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!response.IsSuccessStatusCode)
        {
            var code = "UNKNOWN";
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var message)) code = message.GetString()?.Split(' ')[0] ?? code;
                if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                    foreach (var detail in details.EnumerateArray())
                        if (detail.TryGetProperty("reason", out var reason)) code = reason.GetString() ?? code;
            }
            // Preserve only a bounded machine-readable code, never Google's raw message or request data.
            code = code.Length <= 80 && code.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_') ? code : "UNKNOWN";
            throw new FirebaseAuthException(code ?? "", code switch
            {
                "INVALID_LOGIN_CREDENTIALS" or "EMAIL_NOT_FOUND" or "INVALID_PASSWORD" => "El correo o la contraseña no son correctos.",
                "EMAIL_EXISTS" => "Ya existe una cuenta con este correo. Inicia sesión o recupera tu contraseña.",
                "USER_DISABLED" => "Esta cuenta está deshabilitada.",
                "TOO_MANY_ATTEMPTS_TRY_LATER" => "Demasiados intentos. Espera un momento e inténtalo de nuevo.",
                "OPERATION_NOT_ALLOWED" when action == "signInWithIdp" => "El acceso con Google está deshabilitado. Actívalo en Firebase → Authentication → Método de acceso → Google y guarda el cambio.",
                "OPERATION_NOT_ALLOWED" or "PASSWORD_LOGIN_DISABLED" => "El acceso con correo y contraseña está deshabilitado. Actívalo en Firebase → Authentication → Método de acceso → Correo electrónico/contraseña y guarda el cambio.",
                "CONFIGURATION_NOT_FOUND" => "Firebase Authentication no está inicializado en este proyecto. Abre Authentication en Firebase y configura sus métodos de acceso.",
                "API_KEY_ANDROID_APP_BLOCKED" => "La API key está restringida a Android y Firebase está rechazando esta app de Windows. Configura una API key para el cliente Windows sin restricción de aplicación Android y limita sus APIs a Firebase Authentication y Secure Token.",
                "API_KEY_HTTP_REFERRER_BLOCKED" or "API_KEY_IP_ADDRESS_BLOCKED" => "Las restricciones de la API key no permiten solicitudes desde esta aplicación nativa. Revisa las restricciones de aplicación en Google Cloud.",
                "API_KEY_INVALID" or "INVALID_API_KEY" => "Firebase está rechazando la API key. Comprueba la configuración cliente del proyecto.",
                "API_KEY_SERVICE_BLOCKED" or "SERVICE_DISABLED" or "ACCESS_NOT_CONFIGURED" => "La configuración de Google Cloud no permite Firebase Authentication o Secure Token. Comprueba que estén habilitados y permitidos por la API key.",
                "INVALID_REFRESH_TOKEN" or "TOKEN_EXPIRED" or "USER_NOT_FOUND" or "INVALID_GRANT" => "La sesión ha caducado. Inicia sesión de nuevo.",
                "INVALID_EMAIL" => "Introduce un correo electrónico válido.",
                "WEAK_PASSWORD" => "Usa una contraseña de al menos 6 caracteres.",
                _ => $"Firebase rechazó la operación (HTTP {(int)response.StatusCode}; código {code}). Este código permite identificar la configuración que falta; no se han guardado tus credenciales."
            });
        }
        return document.RootElement.Clone();
    }

    private static FirebaseSession Session(JsonElement data) => new(data.GetProperty("localId").GetString()!,
        data.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "", data.GetProperty("refreshToken").GetString()!, data.GetProperty("idToken").GetString()!, Expiry(data, "expiresIn"));

    private static DateTimeOffset Expiry(JsonElement data, string field)
    {
        var seconds = data.TryGetProperty(field, out var value) && int.TryParse(value.ToString(), out var number) ? number : 3600;
        return DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(seconds, 1, 86400));
    }

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
