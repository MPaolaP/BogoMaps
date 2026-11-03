using BogoMaps.Services;
namespace BogoMaps.Pages;
public partial class PerfilPage : BogoMaps.Controls.StitchPage
{
    private string filter = "Activos";
    private ReportCursor? nextProfile;
    private List<BogoMaps.Models.PedestrianReport> profileRows = [];
    private bool refreshingProfile;
    private async void OnMoreProfileClicked(object? sender, EventArgs e) => await RefreshAsync(true);
    private bool samplesBusy;
    public PerfilPage()
    {
        InitializeComponent();
#if DEBUG
        DemoPanel.IsVisible = true;
#endif
    }
    protected override async void OnAppearing() { base.OnAppearing(); await RefreshAsync(); }
    private async Task RefreshAsync(bool more = false)
    {
        if (refreshingProfile) return;
        refreshingProfile = true;
        var startScope = AuthenticationService.Instance.Current?.UserId;
        MoreProfileButton.IsEnabled = false;
        TabActivos.IsEnabled = TabResueltos.IsEnabled = TabGuardados.IsEnabled = false;
        ReportList.Children.Clear();
        ReportCount.Text = ConfirmationCount.Text = "…";
        AvatarImage.IsVisible = false;
        AvatarInitials.Text = "";
        ProfileCommunityNotice.Text = "Actualizando reportes…";
        StoragePathLabel.Text = LocalRepository.Instance.StoragePath;
        NameLabel.Text = Preferences.Default.Get(AuthenticationService.Instance.ProfileKey("name"), AuthenticationService.Instance.Current?.Email ?? "Caminante");
        BioLabel.Text = Preferences.Default.Get(AuthenticationService.Instance.ProfileKey("bio"), "Recorriendo Bogotá a pie.");
        try
        {
            var page = await ReportData.Instance.FeedAsync(more ? nextProfile : null, mine: filter != "Guardados", favorites: filter == "Guardados");
            profileRows = more ? profileRows.Concat(page.Reports).DistinctBy(r => r.Id).ToList() : page.Reports;
            nextProfile = page.Next;
            MoreProfileButton.IsVisible = nextProfile != null;
            ProfileCommunityNotice.Text = ReportData.Instance.Notice + " Los contadores corresponden a reportes cargados.";
            var all = await ReportData.Instance.ReadAsync();
            var reports = all.Where(r => !r.IsShared || r.OwnerId == AuthenticationService.Instance.Current?.UserId).ToList();
            ReportCount.Text = reports.Count.ToString();
            ConfirmationCount.Text = all.Count(r => r.Confirmed).ToString();
            TabActivos.Text = $"Activos ({reports.Count(r => !r.Resolved)})";
            TabResueltos.Text = $"Resueltos ({reports.Count(r => r.Resolved)})";
            TabGuardados.Text = $"Guardados ({all.Count(r => r.Saved)})";
            AvatarInitials.Text = string.Concat(NameLabel.Text.Split(new[] { ' ', '@', '.' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])));
            var avatar = Preferences.Default.Get(AuthenticationService.Instance.ProfileKey("avatar"), "");
            AvatarImage.IsVisible = File.Exists(avatar);
            AvatarImage.Source = AvatarImage.IsVisible ? avatar : null;
            ReportCards.Fill(ReportList, profileRows.Where(r => filter == "Guardados" ? r.Saved : r.Resolved == (filter == "Resueltos")));
            foreach (var button in new[] { TabActivos, TabResueltos, TabGuardados })
            {
                var selected = (string)button.CommandParameter == filter;
                button.BackgroundColor = (Color)Application.Current!.Resources[selected ? "SurfaceLowest" : "SurfaceContainer"];
                button.TextColor = (Color)Application.Current.Resources[selected ? "BrandPrimaryDark" : "OnSurfaceVariant"];
            }
        }
        catch (Exception ex) { await DisplayAlert("Perfil", ex is CommunityException or FirebaseAuthException ? ex.Message : "No fue posible actualizar los reportes.", "OK"); }
        finally
        {
            refreshingProfile = false; MoreProfileButton.IsEnabled = true; TabActivos.IsEnabled = TabResueltos.IsEnabled = TabGuardados.IsEnabled = true;
            if (AuthenticationService.Instance.Current?.UserId != startScope)
            {
                ReportList.Children.Clear(); profileRows = []; ReportCount.Text = ConfirmationCount.Text = "0";
                NameLabel.Text = "Caminante"; BioLabel.Text = ""; AvatarImage.IsVisible = false; AvatarInitials.Text = "";
                StoragePathLabel.Text = LocalRepository.Instance.StoragePath;
            }
        }
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
    private async void OnLoadSamplesClicked(object? sender, EventArgs e) => await UpdateSamplesAsync(remove: false);
    private async void OnRemoveSamplesClicked(object? sender, EventArgs e) => await UpdateSamplesAsync(remove: true);
    private async Task UpdateSamplesAsync(bool remove)
    {
        if (samplesBusy) return;
        samplesBusy = true;
        DemoPanel.IsEnabled = false;
        try
        {
            if (!await DisplayAlert(remove ? "Retirar ejemplos" : "Cargar ejemplos",
                remove ? "Se retirarán solo los ejemplos de esta cuenta, incluidos los cambios que hayas hecho sobre ellos. Tus reportes propios se conservan."
                       : "Se añadirán hasta seis reportes ficticios marcados [Prueba]. No representan el estado real de esos lugares. No se envían a Firebase.",
                remove ? "Retirar" : "Cargar", "Cancelar")) return;
            var changed = remove ? await LocalRepository.Instance.RemoveSamplesAsync() : await LocalRepository.Instance.LoadSamplesAsync();
            await RefreshAsync();
            await DisplayAlert("Datos de prueba", remove ? $"Se retiraron {changed} ejemplos." : $"Se añadieron {changed} ejemplos. Abre Explorar para verlos en el mapa.", "OK");
        }
        catch (Exception) { await DisplayAlert("Datos de prueba", "No fue posible actualizar los ejemplos. No se han reemplazado tus datos locales.", "OK"); }
        finally { samplesBusy = false; DemoPanel.IsEnabled = true; }
    }
    private async void OnCopyStoragePathClicked(object? sender, EventArgs e)
    {
        try { await Clipboard.Default.SetTextAsync(LocalRepository.Instance.StoragePath); await DisplayAlert("Archivo local", "Ruta copiada. Cierra la app antes de editar el archivo manualmente y conserva una copia de seguridad.", "OK"); }
        catch (Exception) { await DisplayAlert("Archivo local", "No fue posible copiar la ruta.", "OK"); }
    }
    private async void OnCerrarSesionClicked(object? sender, EventArgs e)
    {
        try { await AuthenticationService.Instance.SignOutAsync(); await Shell.Current.GoToAsync("//login"); }
        catch (Exception) { await DisplayAlert("Cerrar sesión", "No fue posible cerrar la sesión. Inténtalo de nuevo.", "OK"); }
    }
}
