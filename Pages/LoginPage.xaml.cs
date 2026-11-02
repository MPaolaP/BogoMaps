namespace BogoMaps.Pages;
public partial class LoginPage : ContentPage
{
    public LoginPage() => InitializeComponent();
    private async void OnInvitadoTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("//explorar");
    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(UsuarioEntry.Text)) Preferences.Default.Set("profile.name", UsuarioEntry.Text.Trim());
        await Shell.Current.GoToAsync("//explorar");
    }
}
