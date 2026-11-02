using Android.OS;
using AndroidX.Credentials;
using AndroidX.Core.Content;
using Xamarin.GoogleAndroid.Libraries.Identity.GoogleId;

namespace BogoMaps.Platforms.Android;

public static class GoogleAccountSignIn
{
    private static bool pending;
    public static async Task<string> SignInAsync(string webClientId)
    {
        if (pending) throw new InvalidOperationException("Ya hay una solicitud de Google en curso.");
        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No hay una actividad Android disponible.");
        pending = true;
        using var cancellation = new CancellationSignal();
        try
        {
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var option = new GetGoogleIdOption.Builder().SetFilterByAuthorizedAccounts(false)!
                .SetServerClientId(webClientId)!.SetAutoSelectEnabled(false)!.Build()!;
            var request = new GetCredentialRequest.Builder().AddCredentialOption(option)!.Build()!;
            var callback = new CredentialCallback(completion);
            CredentialManager.Create(activity).GetCredentialAsync(activity, request, cancellation,
                ContextCompat.GetMainExecutor(activity), callback);
            return await completion.Task.WaitAsync(TimeSpan.FromMinutes(2));
        }
        finally { cancellation.Cancel(); pending = false; }
    }

    private sealed class CredentialCallback(TaskCompletionSource<string> completion) : Java.Lang.Object, ICredentialManagerCallback
    {
        public void OnResult(Java.Lang.Object? result)
        {
            try
            {
                if (result is not GetCredentialResponse response || response.Credential is not CustomCredential credential)
                    throw new InvalidOperationException("Google no entregó una credencial válida.");
                var token = GoogleIdTokenCredential.CreateFrom(credential.Data).IdToken;
                if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Google no entregó una credencial válida.");
                completion.TrySetResult(token);
            }
            catch (Exception) { completion.TrySetException(new InvalidOperationException("No se pudo leer la credencial Google.")); }
        }
        public void OnError(Java.Lang.Object error)
        {
            if (error.Class?.Name == "androidx.credentials.exceptions.GetCredentialCancellationException")
                completion.TrySetCanceled();
            else completion.TrySetException(new InvalidOperationException("Google no pudo completar el acceso. Comprueba Google Play Services, tu conexión y las huellas SHA registradas en Firebase."));
        }
    }
}
