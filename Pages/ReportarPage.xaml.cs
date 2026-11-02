using BogoMaps.Models;
using BogoMaps.Services;
using System.Globalization;
namespace BogoMaps.Pages;

public partial class ReportarPage : ContentPage
{
    private string category = "Aceras";
    private string? photo;
    public ReportarPage()
    {
        InitializeComponent();
    }

    private static Color Res(string key) => (Color)Application.Current!.Resources[key];

    private void OnCategoriaTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border tapped)
            return;

        category = new[] { "Aceras", "Iluminación", "Accesibilidad", "Cruces", "Obras", "Punto seguro" }[CategoriaGrid.Children.IndexOf(tapped)];
        foreach (var child in CategoriaGrid.Children.OfType<Border>())
        {
            var selected = child == tapped;
            child.BackgroundColor = Res(selected ? "BrandPrimary" : "SurfaceLow");
            if (child.Content is Label label)
                label.TextColor = selected ? Colors.White : Res("OnSurface");
        }
    }

    private void OnGravedadClicked(object? sender, EventArgs e)
    {
        if (sender is not Button pressed)
            return;

        foreach (var btn in new[] { BtnBajo, BtnModerado, BtnCritico })
        {
            btn.BackgroundColor = Colors.Transparent;
            btn.TextColor = Res("OnSurfaceVariant");
        }

        if (pressed == BtnBajo)
        {
            pressed.BackgroundColor = Res("SecondaryContainer");
            pressed.TextColor = Res("OnSecondaryContainer");
            GravedadLabel.Text = "Bajo";
            GravedadLabel.TextColor = Res("BrandSecondary");
        }
        else if (pressed == BtnModerado)
        {
            pressed.BackgroundColor = Res("TertiaryFixed");
            pressed.TextColor = Res("OnTertiaryFixed");
            GravedadLabel.Text = "Moderado";
            GravedadLabel.TextColor = Res("TertiaryContainer");
        }
        else
        {
            pressed.BackgroundColor = Res("ErrorContainer");
            pressed.TextColor = Res("OnErrorContainer");
            GravedadLabel.Text = "Crítico";
            GravedadLabel.TextColor = Res("BrandError");
        }
    }

    private async void OnGpsClicked(object? sender, EventArgs e)
    {
        try
        {
            var location = await DeviceLocation.GetAsync();
            LatitudeEntry.Text = location.Latitude.ToString(CultureInfo.InvariantCulture);
            LongitudeEntry.Text = location.Longitude.ToString(CultureInfo.InvariantCulture);
            UbicacionLabel.Text = "Ubicación GPS obtenida";
        }
        catch (Exception ex) { await DisplayAlert("GPS", ex.Message, "OK"); }
    }
    private async void OnFotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var file = await MediaPicker.Default.PickPhotoAsync();
            if (file == null) return;
            var destination = Path.Combine(FileSystem.AppDataDirectory, Guid.NewGuid() + Path.GetExtension(file.FileName));
            using var input = await file.OpenReadAsync();
            using var output = File.Create(destination);
            await input.CopyToAsync(output);
            if (photo != null) File.Delete(photo);
            photo = destination;
            PhotoButton.Text = "Foto adjunta · cambiar";
        }
        catch (Exception) { await DisplayAlert("Foto", "No fue posible adjuntar la foto. Revisa los permisos e inténtalo de nuevo.", "OK"); }
    }
    private async void OnPublicarClicked(object? sender, EventArgs e)
    {
        if (!PublicarBtn.IsEnabled) return;
        PublicarBtn.IsEnabled = false;
        try
        {
            if (!double.TryParse(LatitudeEntry.Text?.Replace(',', '.'), CultureInfo.InvariantCulture, out var latitude) ||
                !double.TryParse(LongitudeEntry.Text?.Replace(',', '.'), CultureInfo.InvariantCulture, out var longitude))
                throw new ArgumentException("Obtén tu ubicación GPS o introduce coordenadas válidas.");
            var report = new PedestrianReport { Category = category, Description = DescriptionEditor.Text?.Trim() ?? "",
                Address = AddressEntry.Text?.Trim() ?? "", Latitude = latitude, Longitude = longitude,
                Severity = GravedadLabel.Text, Photo = photo, Anonymous = AnonimoSwitch.IsToggled };
            if (string.IsNullOrWhiteSpace(report.Address)) throw new ArgumentException("Escribe una dirección o referencia del lugar.");
            await LocalRepository.Instance.SaveAsync(report);
            photo = null;
            DescriptionEditor.Text = AddressEntry.Text = LatitudeEntry.Text = LongitudeEntry.Text = "";
            AnonimoSwitch.IsToggled = false;
            PhotoButton.Text = "Adjuntar foto";
            UbicacionLabel.Text = "Selecciona tu ubicación con GPS o coordenadas";
            await DisplayAlert("Reporte guardado", "Disponible en tu mapa local. No se ha enviado a un servidor.", "OK");
            await Shell.Current.GoToAsync("//explorar");
        }
        catch (Exception ex) { await DisplayAlert("No se guardó el reporte", ex.Message, "OK"); }
        finally { PublicarBtn.IsEnabled = true; }
    }
}
