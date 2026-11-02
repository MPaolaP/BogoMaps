using BogoMaps.Services;
namespace BogoMaps.Pages;
public partial class PerfilPage : ContentPage
{
    private string filter = "Activos";
    public PerfilPage() => InitializeComponent();
    protected override async void OnAppearing() { base.OnAppearing(); await RefreshAsync(); }
    private async Task RefreshAsync()
    {
        NameLabel.Text = Preferences.Default.Get("profile.name", "Caminante");
        BioLabel.Text = Preferences.Default.Get("profile.bio", "Recorriendo Bogotá a pie.");
        try
        {
            var reports = await LocalRepository.Instance.ReadAsync();
            CountLabel.Text = $"{reports.Count} reportes · {reports.Count(r => r.Confirmed)} confirmaciones locales";
            ReportCards.Fill(ReportList, reports.Where(r => filter == "Guardados" ? r.Saved : r.Resolved == (filter == "Resueltos")));
            foreach (var button in new[] { TabActivos, TabResueltos, TabGuardados })
                button.BackgroundColor = (Color)Application.Current!.Resources[button.Text == filter ? "PrimaryFixed" : "SurfaceLowest"];
        }
        catch (Exception) { await DisplayAlert("Perfil", "No fue posible leer los reportes locales.", "OK"); }
    }
    private async void OnEditClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Editar perfil", "Tu nombre", initialValue: NameLabel.Text, maxLength: 60);
        if (string.IsNullOrWhiteSpace(name)) return;
        var bio = await DisplayPromptAsync("Editar perfil", "Acerca de ti", initialValue: BioLabel.Text, maxLength: 250);
        if (bio == null) return;
        Preferences.Default.Set("profile.name", name.Trim());
        Preferences.Default.Set("profile.bio", bio.Trim());
        await RefreshAsync();
    }
    private async void OnTabClicked(object? sender, EventArgs e) { if (sender is Button button) { filter = button.Text; await RefreshAsync(); } }
    private async void OnCerrarSesionClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//login");
}
