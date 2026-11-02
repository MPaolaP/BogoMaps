using System.Net;
using System.Text;
using System.Text.Json;
using BogoMaps.Services;

internal static class AuthClientTests
{
    public static async Task RunAsync()
    {
        var calls = 0;
        var endpoint = "signInWithPassword";
        var response = "{\"localId\":\"uid-1\",\"email\":\"test@example.com\",\"refreshToken\":\"refresh-test\"}";
        var status = HttpStatusCode.OK;
        var handler = new Handler(async request =>
        {
            calls++;
            if (!request.RequestUri!.AbsolutePath.Contains(endpoint)) throw new Exception("Wrong Firebase endpoint");
            var body = await request.Content!.ReadAsStringAsync();
            if (endpoint == "signInWithIdp")
            {
                using var payload = JsonDocument.Parse(body);
                if (!payload.RootElement.GetProperty("postBody").GetString()!.Contains("providerId=google.com")) throw new Exception("Missing Google provider");
            }
            return new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        });
        var client = new FirebaseAuthClient(new HttpClient(handler), "public-test-key");
        var session = await client.SignInAsync("test@example.com", "password-test");
        if (session.UserId != "uid-1" || session.RefreshToken != "refresh-test") throw new Exception("Session mapping failed");
        endpoint = "signUp";
        await client.SignInAsync("test@example.com", "password-test", true);
        endpoint = "signInWithIdp";
        await client.GoogleAsync("google-token-test");
        endpoint = "sendOobCode";
        response = "{}";
        await client.ResetAsync("test@example.com");
        var beforeInvalidInput = calls;
        try { await client.SignInAsync("not-an-email", "password-test"); throw new Exception("Invalid email accepted"); }
        catch (ArgumentException) { }
        if (calls != beforeInvalidInput) throw new Exception("Invalid input reached Firebase");
        endpoint = "signInWithPassword";
        status = HttpStatusCode.BadRequest;
        response = "{\"error\":{\"message\":\"INVALID_LOGIN_CREDENTIALS\"}}";
        try { await client.SignInAsync("test@example.com", "wrong"); throw new Exception("Failed credentials accepted"); }
        catch (FirebaseAuthException ex) when (ex.Code == "INVALID_LOGIN_CREDENTIALS") { }
        endpoint = "sendOobCode";
        response = "{\"error\":{\"message\":\"EMAIL_NOT_FOUND\"}}";
        await client.ResetAsync("test@example.com");
        endpoint = "/token";
        status = HttpStatusCode.OK;
        response = "{\"user_id\":\"uid-1\",\"refresh_token\":\"rotated-refresh\"}";
        var refreshed = await client.RefreshAsync(session);
        if (refreshed.RefreshToken != "rotated-refresh") throw new Exception("Refresh rotation lost");
        response = "{\"user_id\":\"another-user\",\"refresh_token\":\"rotated-refresh\"}";
        try { await client.RefreshAsync(session); throw new Exception("Mismatched identity accepted"); }
        catch (InvalidDataException) { }
        Console.WriteLine("PASS: Firebase client: sign-in, sign-up, Google token exchange, reset, validation, rejection, refresh rotation and identity mismatch (mock HTTP; no remote accounts created).");
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handle(request);
    }
}
