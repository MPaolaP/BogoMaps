using BogoMaps.Controls;
using BogoMaps.Models;
using BogoMaps.Services;
namespace BogoMaps.Pages;
public partial class DetallePuntoPage : BogoMaps.Controls.StitchPage, IQueryAttributable
{
    private string? id;
    private PedestrianReport? report;
    private bool updating;
    public DetallePuntoPage() => InitializeComponent();
    public void ApplyQueryAttributes(IDictionary<string, object> query) => id = query.TryGetValue("id", out var value) ? value.ToString() : null;
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        report = null;
        PhotoImage.IsVisible = false; PhotoImage.Source = null;
        NoPhoto.IsVisible = true;
        ReportTitle.Text = AddressLabel.Text = StatusLabel.Text = DetailsLabel.Text = EvidenceLabel.Text = "";
        LightingValue.Text = PavementValue.Text = AccessibilityValue.Text = NoiseValue.Text = "…";
        DetailConnectionNotice.Text = "Cargando reporte…";
        GuardarBtn.IsEnabled = ConfirmarBtn.IsEnabled = false;
        ResolvedBtn.IsVisible = false;
        try
        {
            report = await ReportData.Instance.GetAsync(id ?? "");
            if (report == null) { await DisplayAlert("Reporte", "Este reporte no está disponible.", "OK"); await Shell.Current.GoToAsync(".."); return; }
            Render();
        }
        catch (Exception ex) { await DisplayAlert("Reporte", ex is CommunityException or FirebaseAuthException ? ex.Message : "No fue posible cargar el reporte.", "OK"); }
    }
    private void Render()
    {
        if (report == null) return;
        DetailConnectionNotice.Text = report.IsShared ? ReportData.Instance.Notice : "Reporte local de este dispositivo.";
        ReportTitle.Text = report.Description;
        AddressLabel.Text = report.Address;
        StatusLabel.Text = (report.IsDemo ? "EJEMPLO FICTICIO · " : "") + report.Summary;
        LightingValue.Text = report.Lighting;
        PavementValue.Text = report.Pavement;
        AccessibilityValue.Text = report.Accessibility;
        NoiseValue.Text = report.Noise;
        ApplyAssessment(LightingBadge, LightingValue, report.Lighting);
        ApplyAssessment(PavementBadge, PavementValue, report.Pavement);
        ApplyAssessment(AccessibilityBadge, AccessibilityValue, report.Accessibility);
        ApplyAssessment(NoiseBadge, NoiseValue, report.Noise);
        GuardarBtn.ImageSource = report.Saved ? "ui_save_green.png" : "ui_save_blue.png";
        DetailsLabel.Text = $"Creado: {report.CreatedAt.ToLocalTime():g}\n{(report.Anonymous ? "Anónimo" : report.IsShared ? "Reporte comunitario" : "Reporte local")}\nCoordenadas: {report.Latitude:F6}, {report.Longitude:F6}" +
            (report.ConfirmedAt.HasValue ? $"\nConfirmado en este dispositivo: {report.ConfirmedAt.Value.ToLocalTime():g}" : "");
        GuardarBtn.IsEnabled = !updating;
        ResolvedBtn.IsVisible = !report.IsShared || report.OwnerId == AuthenticationService.Instance.Current?.UserId;
        ConfirmationInfo.Text = report.IsShared ? "Tu confirmación se registra para tu cuenta. No certifica la seguridad de una ruta." : "La confirmación se conserva en este dispositivo.";
        GuardarBtn.Text = report.Saved ? "Quitar de guardados" : "Guardar punto";
        ConfirmarBtn.IsEnabled = !report.Confirmed;
        ConfirmarBtn.Text = report.Confirmed ? (report.IsShared ? "Confirmado por ti" : "Confirmado localmente") : "Confirmar estado";
        ResolvedBtn.Text = report.Resolved ? "Reabrir reporte" : "Marcar como resuelto";
        PhotoImage.IsVisible = report.Photo != null && File.Exists(report.Photo);
        PhotoImage.Source = PhotoImage.IsVisible ? report.Photo : null;
        NoPhoto.IsVisible = !PhotoImage.IsVisible;
        EvidenceLabel.Text = PhotoImage.IsVisible ? (report.IsShared ? "Foto local del autor. La fotografía no está publicada." : "Foto adjunta al reporte, visible en la cabecera.") : "Este reporte no tiene una foto compartida.";
    }
    private static void ApplyAssessment(Border badge, Label label, string value)
    {
        var tone = CivicVisuals.Assessment(value);
        badge.BackgroundColor = CivicVisuals.Surface(tone);
        label.TextColor = CivicVisuals.Ink(tone);
    }
    private async Task UpdateAsync(string action)
    {
        if (report == null || updating) return;
        updating = true;
        Content.IsEnabled = false;
        try { report = await ReportData.Instance.ChangeAsync(report, action); }
        catch (Exception ex) { await DisplayAlert("No se confirmó el cambio", ex is CommunityException or FirebaseAuthException or InvalidOperationException ? ex.Message : "No fue posible guardar el cambio. Revisa la conexión.", "OK"); }
        finally { updating = false; Content.IsEnabled = true; Render(); }
    }
    private async void OnGuardarClicked(object? sender, EventArgs e) => await UpdateAsync("save");
    private async void OnConfirmarClicked(object? sender, EventArgs e) => await UpdateAsync("confirm");
    private async void OnResolvedClicked(object? sender, EventArgs e) => await UpdateAsync("resolve");
    private async void OnReportarCambioClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//reportar");
    private async void OnViewMapClicked(object? sender, EventArgs e)
    {
        if (report != null) await Shell.Current.GoToAsync($"//explorar?reportId={report.Id}");
    }
    private async void OnDirectionsClicked(object? sender, EventArgs e)
    {
        if (report == null) return;
        try { await Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync(report.Latitude, report.Longitude, new MapLaunchOptions { Name = report.Address, NavigationMode = NavigationMode.Walking }); }
        catch (Exception) { await DisplayAlert("Indicaciones", "No hay una aplicación de mapas disponible.", "OK"); }
    }
}
