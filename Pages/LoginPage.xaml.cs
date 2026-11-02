using BogoMaps.Services;
namespace BogoMaps.Pages;

public partial class LoginPage : ContentPage
{
    private bool busy;
    private bool restoreAttempted;
    public LoginPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (restoreAttempted) return;
        restoreAttempted = true;
        await RunAsync("Restaurar sesión", async () =>
        {
            if (await AuthenticationService.Instance.RestoreAsync()) await ExploreAsync();
        });
    }

    private async void OnInvitadoTapped(object? sender, TappedEventArgs e) => await GuestAsync();
    private async void OnBackClicked(object? sender, EventArgs e) => await GuestAsync();
    private Task GuestAsync() => RunAsync("Acceso invitado", async () =>
    {
        AuthenticationService.Instance.SignOut();
        ClaveEntry.Text = "";
        await ExploreAsync();
    });
    private static Task ExploreAsync() => Shell.Current.GoToAsync("//explorar");
    private void OnUsuarioCompleted(object? sender, EventArgs e) => ClaveEntry.Focus();
    private void OnToggleClave(object? sender, EventArgs e)
    {
        ClaveEntry.IsPassword = !ClaveEntry.IsPassword;
        SemanticProperties.SetDescription(MostrarClaveButton, ClaveEntry.IsPassword ? "Mostrar contraseña" : "Ocultar contraseña");
    }

    private async void OnLoginClicked(object? sender, EventArgs e) => await RunAsync("Iniciar sesión", async () =>
    {
        await AuthenticationService.Instance.SignInAsync(UsuarioEntry.Text ?? "", ClaveEntry.Text ?? "");
        ClaveEntry.Text = "";
        await ExploreAsync();
    });
    private async void OnGoogleClicked(object? sender, EventArgs e) => await RunAsync("Continuar con Google", async () =>
    {
        await AuthenticationService.Instance.GoogleAsync();
        ClaveEntry.Text = "";
        await ExploreAsync();
    });
    private async void OnRegisterClicked(object? sender, EventArgs e) => await RunAsync("Crear cuenta", async () =>
    {
        if (!await DisplayAlert("Crear cuenta", "Se creará una cuenta con el correo y la contraseña introducidos en el formulario.", "Crear cuenta", "Cancelar")) return;
        await AuthenticationService.Instance.SignInAsync(UsuarioEntry.Text ?? "", ClaveEntry.Text ?? "", register: true);
        ClaveEntry.Text = "";
        await ExploreAsync();
    });
    private async void OnRecoveryClicked(object? sender, EventArgs e) => await RunAsync("Recuperar contraseña", async () =>
    {
        await AuthenticationService.Instance.ResetAsync(UsuarioEntry.Text ?? "");
        await DisplayAlert("Revisa tu correo", "Si existe una cuenta con ese correo, recibirás instrucciones para recuperar el acceso. Revisa también la carpeta de spam.", "OK");
    });
    private async void OnAppleClicked(object? sender, EventArgs e) => await DisplayAlert("Continuar con Apple",
        "El acceso con Apple necesita su configuración independiente. Puedes usar Google, correo o el acceso invitado.", "OK");

    private async Task RunAsync(string title, Func<Task> action)
    {
        if (busy) return;
        busy = true;
        LoginForm.IsEnabled = false;
        AuthBusy.IsVisible = AuthBusy.IsRunning = true;
        try { await action(); }
        catch (OperationCanceledException) { /* Closing Google's chooser is not a failed login. */ }
        catch (Exception ex) when (ex is FirebaseAuthException or ArgumentException or PlatformNotSupportedException or InvalidOperationException)
        { await DisplayAlert(title, ex.Message, "OK"); }
        catch (Exception)
        { await DisplayAlert(title, "No se pudo completar la operación. Revisa tu conexión e inténtalo de nuevo.", "OK"); }
        finally { busy = false; LoginForm.IsEnabled = true; AuthBusy.IsVisible = AuthBusy.IsRunning = false; }
    }
}
