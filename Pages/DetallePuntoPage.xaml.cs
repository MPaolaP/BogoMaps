using BogoMaps.Controls;
using BogoMaps.Models;
using BogoMaps.Services;
namespace BogoMaps.Pages;
public partial class DetallePuntoPage : BogoMaps.Controls.StitchPage, IQueryAttributable
{
    private string? id;
    private PedestrianReport? report;
    public DetallePuntoPage() => InitializeComponent();
    public void ApplyQueryAttributes(IDictionary<string, object> query) => id = query.TryGetValue("id", out var value) ? value.ToString() : null;
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            report = (await LocalRepository.Instance.ReadAsync()).FirstOrDefault(r => r.Id == id);
            if (report == null) { await DisplayAlert("Reporte", "Este reporte no está disponible.", "OK"); await Shell.Current.GoToAsync(".."); return; }
            Render();
        }
        catch (Exception) { await DisplayAlert("Reporte", "No fue posible cargar el reporte.", "OK"); }
    }
    private void Render()
    {
        if (report == null) return;
        ReportTitle.Text = report.Description;
        AddressLabel.Text = report.Address;
        StatusLabel.Text = report.Summary;
        LightingValue.Text = report.Lighting;
        PavementValue.Text = report.Pavement;
        AccessibilityValue.Text = report.Accessibility;
        NoiseValue.Text = report.Noise;
        ApplyAssessment(LightingBadge, LightingValue, report.Lighting);
        ApplyAssessment(PavementBadge, PavementValue, report.Pavement);
        ApplyAssessment(AccessibilityBadge, AccessibilityValue, report.Accessibility);
        ApplyAssessment(NoiseBadge, NoiseValue, report.Noise);
        GuardarBtn.ImageSource = report.Saved ? "ui_save_green.png" : "ui_save_blue.png";
        DetailsLabel.Text = $"Creado: {report.CreatedAt.ToLocalTime():g}\n{(report.Anonymous ? "Anónimo" : "Reporte local")}\nCoordenadas: {report.Latitude:F6}, {report.Longitude:F6}" +
            (report.ConfirmedAt.HasValue ? $"\nConfirmado en este dispositivo: {report.ConfirmedAt.Value.ToLocalTime():g}" : "");
        GuardarBtn.Text = report.Saved ? "Quitar de guardados" : "Guardar punto";
        ConfirmarBtn.IsEnabled = !report.Confirmed;
        ConfirmarBtn.Text = report.Confirmed ? "Estado confirmado localmente" : "Confirmar estado";
        ResolvedBtn.Text = report.Resolved ? "Reabrir reporte" : "Marcar como resuelto";
        PhotoImage.IsVisible = report.Photo != null && File.Exists(report.Photo);
        PhotoImage.Source = PhotoImage.IsVisible ? report.Photo : null;
        NoPhoto.IsVisible = !PhotoImage.IsVisible;
        EvidenceLabel.Text = PhotoImage.IsVisible ? "Foto adjunta al reporte, visible en la cabecera." : "Este reporte todavía no tiene una foto adjunta.";
    }
    private static void ApplyAssessment(Border badge, Label label, string value)
    {
        var tone = CivicVisuals.Assessment(value);
        badge.BackgroundColor = CivicVisuals.Surface(tone);
        label.TextColor = CivicVisuals.Ink(tone);
    }
    private async Task UpdateAsync(Action<PedestrianReport> change)
    {
        if (report == null) return;
        var previous = System.Text.Json.JsonSerializer.Serialize(report);
        try { change(report); await LocalRepository.Instance.SaveAsync(report); }
        catch (Exception) { report = System.Text.Json.JsonSerializer.Deserialize<PedestrianReport>(previous); await DisplayAlert("No se guardó", "Inténtalo de nuevo. El cambio no se ha aplicado.", "OK"); }
        Render();
    }
    private async void OnGuardarClicked(object? sender, EventArgs e) => await UpdateAsync(r => r.Saved = !r.Saved);
    private async void OnConfirmarClicked(object? sender, EventArgs e) => await UpdateAsync(r => { r.Confirmed = true; r.ConfirmedAt = DateTimeOffset.UtcNow; });
    private async void OnResolvedClicked(object? sender, EventArgs e) => await UpdateAsync(r => r.Resolved = !r.Resolved);
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
