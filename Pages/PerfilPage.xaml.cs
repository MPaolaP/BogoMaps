using BogoMaps.Services;
namespace BogoMaps.Pages;
public partial class PerfilPage : BogoMaps.Controls.StitchPage
{
    private string filter = "Activos";
    public PerfilPage() => InitializeComponent();
    protected override async void OnAppearing() { base.OnAppearing(); await RefreshAsync(); }
    private async Task RefreshAsync()
    {
        NameLabel.Text = Preferences.Default.Get(AuthenticationService.Instance.ProfileKey("name"), AuthenticationService.Instance.Current?.Email ?? "Caminante");
        BioLabel.Text = Preferences.Default.Get(AuthenticationService.Instance.ProfileKey("bio"), "Recorriendo Bogotá a pie.");
        try
        {
            var reports = await LocalRepository.Instance.ReadAsync();
            ReportCount.Text = reports.Count.ToString();
            ConfirmationCount.Text = reports.Count(r => r.Confirmed).ToString();
            TabActivos.Text = $"Activos ({reports.Count(r => !r.Resolved)})";
            TabResueltos.Text = $"Resueltos ({reports.Count(r => r.Resolved)})";
            TabGuardados.Text = $"Guardados ({reports.Count(r => r.Saved)})";
            AvatarInitials.Text = string.Concat(NameLabel.Text.Split(new[] { ' ', '@', '.' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])));
            var avatar = Preferences.Default.Get(AuthenticationService.Instance.ProfileKey("avatar"), "");
            AvatarImage.IsVisible = File.Exists(avatar);
            AvatarImage.Source = AvatarImage.IsVisible ? avatar : null;
            ReportCards.Fill(ReportList, reports.Where(r => filter == "Guardados" ? r.Saved : r.Resolved == (filter == "Resueltos")));
            foreach (var button in new[] { TabActivos, TabResueltos, TabGuardados })
            {
                var selected = (string)button.CommandParameter == filter;
                button.BackgroundColor = (Color)Application.Current!.Resources[selected ? "SurfaceLowest" : "SurfaceContainer"];
                button.TextColor = (Color)Application.Current.Resources[selected ? "BrandPrimaryDark" : "OnSurfaceVariant"];
            }
        }
        catch (Exception) { await DisplayAlert("Perfil", "No fue posible leer los reportes locales.", "OK"); }
    }
    private async void OnEditClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Editar perfil", "Tu nombre", initialValue: NameLabel.Text, maxLength: 60);
        if (string.IsNullOrWhiteSpace(name)) return;
        var bio = await DisplayPromptAsync("Editar perfil", "Acerca de ti", initialValue: BioLabel.Text, maxLength: 250);
        if (bio == null) return;
        Preferences.Default.Set(AuthenticationService.Instance.ProfileKey("name"), name.Trim());
        Preferences.Default.Set(AuthenticationService.Instance.ProfileKey("bio"), bio.Trim());
        await RefreshAsync();
    }
    private async void OnTabClicked(object? sender, EventArgs e) { if (sender is Button button) { filter = (string)button.CommandParameter; await RefreshAsync(); } }
    private async void OnAvatarClicked(object? sender, EventArgs e)
    {
        try
        {
            var file = await MediaPicker.Default.PickPhotoAsync();
            if (file == null) return;
            var path = Path.Combine(FileSystem.AppDataDirectory, "avatar-" + Guid.NewGuid() + Path.GetExtension(file.FileName));
            using (var input = await file.OpenReadAsync())
            using (var output = File.Create(path)) await input.CopyToAsync(output);
            var key = AuthenticationService.Instance.ProfileKey("avatar");
            var old = Preferences.Default.Get(key, "");
            Preferences.Default.Set(key, path);
            if (File.Exists(old)) File.Delete(old);
            await RefreshAsync();
        }
        catch (Exception) { await DisplayAlert("Foto de perfil", "No fue posible cambiar la foto. Inténtalo de nuevo.", "OK"); }
    }
    private async void OnCerrarSesionClicked(object? sender, EventArgs e)
    {
        AuthenticationService.Instance.SignOut();
        await Shell.Current.GoToAsync("//login");
    }
}
