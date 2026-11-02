namespace BogoMaps.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage() => InitializeComponent();

    private async void OnInvitadoTapped(object? sender, TappedEventArgs e) => await ExploreAsync();
    private async void OnBackClicked(object? sender, EventArgs e) => await ExploreAsync();
    private static Task ExploreAsync() => Shell.Current.GoToAsync("//explorar");

    private void OnUsuarioCompleted(object? sender, EventArgs e) => ClaveEntry.Focus();

    private void OnToggleClave(object? sender, EventArgs e)
    {
        ClaveEntry.IsPassword = !ClaveEntry.IsPassword;
        SemanticProperties.SetDescription(MostrarClaveButton, ClaveEntry.IsPassword ? "Mostrar contraseña" : "Ocultar contraseña");
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsuarioEntry.Text) || string.IsNullOrWhiteSpace(ClaveEntry.Text))
        {
            await DisplayAlert("Iniciar sesión", "Introduce tu correo o celular y tu contraseña.", "OK");
            return;
        }
        await AuthenticationPendingAsync("Iniciar sesión");
    }

    private Task AuthenticationPendingAsync(string title) => DisplayAlert(title,
        "El acceso con cuentas aún no está configurado. Puedes usar «Explorar como peatón invitado» para continuar con tus datos locales. No se ha iniciado sesión ni enviado tus credenciales.", "Entendido");

    private async void OnGoogleClicked(object? sender, EventArgs e) => await AuthenticationPendingAsync("Continuar con Google");
    private async void OnAppleClicked(object? sender, EventArgs e) => await AuthenticationPendingAsync("Continuar con Apple");
    private async void OnRecoveryClicked(object? sender, EventArgs e) => await AuthenticationPendingAsync("Recuperar contraseña");
    private async void OnRegisterClicked(object? sender, EventArgs e) => await AuthenticationPendingAsync("Crear cuenta");
}
