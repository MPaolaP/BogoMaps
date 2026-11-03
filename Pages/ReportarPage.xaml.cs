using BogoMaps.Controls;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using BogoMaps.Models;
using BogoMaps.Services;
using System.Globalization;
namespace BogoMaps.Pages;

public partial class ReportarPage : BogoMaps.Controls.StitchPage
{
    private string category = "Aceras";
    private string? photo;
    private MemoryLayer? locationPin;
    private string draftId = Guid.NewGuid().ToString("N");
    private string? draftAccount;
    private bool draftInitialized;
    public ReportarPage()
    {
        InitializeComponent();
        LocationMap.Map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer("BogoMaps/1.0 (pedestrian local map)"));
        var center = SphericalMercator.FromLonLat(-74.0721, 4.6505);
        LocationMap.Map.Navigator.CenterOnAndZoomTo(new MPoint(center.x, center.y), 18);
        LocationMap.Map.Tapped += (_, args) =>
        {
            var point = SphericalMercator.ToLonLat(args.WorldPosition.X, args.WorldPosition.Y);
            if (point.lat is < 3.7 or > 4.9 || point.lon is < -74.6 or > -73.9) return;
            SetLocation(point.lat, point.lon, false);
            args.Handled = true;
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var account = AuthenticationService.Instance.Current?.UserId;
        if (draftInitialized && account != draftAccount) ClearDraft();
        draftInitialized = true;
        draftAccount = account;
        CommunitySwitch.IsEnabled = account != null;
        if (account == null) CommunitySwitch.IsToggled = false;
        CommunityHint.Text = account == null ? "Inicia sesión para publicar. Puedes guardar el reporte en este dispositivo."
            : "Al activar esta opción, otras cuentas podrán consultar el reporte. La foto se conserva solo en este dispositivo; aún no se comparten fotografías.";
        AuthorLabel.Text = Preferences.Default.Get(AuthenticationService.Instance.ProfileKey("name"), AuthenticationService.Instance.Current?.Email ?? "Caminante invitado");
    }
    private void OnAssessmentClicked(object? sender, EventArgs e) => AssessmentPanel.IsVisible = !AssessmentPanel.IsVisible;
    private void OnManualLocationClicked(object? sender, EventArgs e) => ManualCoordinates.IsVisible = !ManualCoordinates.IsVisible;
    private void SetLocation(double latitude, double longitude, bool center)
    {
        LatitudeEntry.Text = latitude.ToString(CultureInfo.InvariantCulture);
        LongitudeEntry.Text = longitude.ToString(CultureInfo.InvariantCulture);
        UbicacionLabel.Text = $"Bogotá · {latitude:F5}, {longitude:F5}";
        var point = SphericalMercator.FromLonLat(longitude, latitude);
        if (locationPin != null) LocationMap.Map.Layers.Remove(locationPin);
        locationPin = new MemoryLayer { Features = new[] { new GeometryFeature { Geometry = new NetTopologySuite.Geometries.Point(point.x, point.y), Styles = { new Mapsui.Styles.SymbolStyle { Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Blue), SymbolScale = .8 } } } } };
        LocationMap.Map.Layers.Add(locationPin);
        if (center) LocationMap.Map.Navigator.CenterOnAndZoomTo(new MPoint(point.x, point.y), 4);
        LocationMap.Map.Refresh();
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
            if (child.Content is Grid content)
            {
                foreach (var label in content.Children.OfType<Label>()) label.TextColor = selected ? Colors.White : Res("OnSurface");
                var index = CategoriaGrid.Children.IndexOf(child);
                var appearance = CivicVisuals.Category(new[] { "Aceras", "Iluminación", "Accesibilidad", "Cruces", "Obras", "Punto seguro" }[index]);
                foreach (var image in content.Children.OfType<Image>()) image.Source = selected ? $"{appearance.Icon}_white.png" : CivicVisuals.IconSource(appearance.Icon, appearance.Tone);
            }
        }
        if (category == "Punto seguro") OnGravedadClicked(BtnBajo, EventArgs.Empty);
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
        if (!GpsButton.IsEnabled) return;
        var draft = draftId;
        var original = GpsButton.Text;
        GpsButton.IsEnabled = false;
        GpsButton.Text = "Obteniendo ubicación…";
        try
        {
            var location = await DeviceLocation.GetAsync();
            if (draft != draftId) return;
            SetLocation(location.Latitude, location.Longitude, true);
            var address = await DeviceLocation.DescribeAsync(location);
            if (draft != draftId) return;
            if (address != null && string.IsNullOrWhiteSpace(AddressEntry.Text)) AddressEntry.Text = address.Length <= 300 ? address : address[..300];
            if (address != null) UbicacionLabel.Text = address;
        }
        catch (Exception ex) { await DisplayAlert("GPS", ex.Message, "OK"); }
        finally { GpsButton.Text = original; GpsButton.IsEnabled = true; }
    }
    private async void OnFotoClicked(object? sender, EventArgs e)
    {
        if (!PhotoButton.IsEnabled) return;
        var draft = draftId;
        PhotoButton.IsEnabled = false;
        try
        {
            var option = await DisplayActionSheet("Evidencia visual", "Cancelar", null, "Tomar foto", "Elegir de la galería");
            FileResult? file;
            if (option == "Tomar foto")
            {
                if (!MediaPicker.Default.IsCaptureSupported) { await DisplayAlert("Cámara", "No hay una cámara disponible. Puedes elegir una foto de la galería.", "OK"); return; }
                file = await MediaPicker.Default.CapturePhotoAsync();
            }
            else if (option == "Elegir de la galería") file = await MediaPicker.Default.PickPhotoAsync();
            else return;
            if (file == null || draft != draftId) return;
            var destination = Path.Combine(FileSystem.AppDataDirectory, Guid.NewGuid() + Path.GetExtension(file.FileName));
            using (var input = await file.OpenReadAsync())
            using (var output = File.Create(destination)) await input.CopyToAsync(output);
            if (draft != draftId) { File.Delete(destination); return; }
            var previous = photo;
            photo = destination;
            PhotoButton.Text = "Cambiar foto";
            PhotoPreview.Source = photo;
            PhotoPreview.IsVisible = true;
            // Replacing the image is successful even if an old file is temporarily locked by Windows.
            if (previous != null) try { File.Delete(previous); } catch (IOException) { }
        }
        catch (Exception) { await DisplayAlert("Foto", "No fue posible obtener la foto. Revisa los permisos o elige otra desde la galería.", "OK"); }
        finally { PhotoButton.IsEnabled = true; }
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
            var report = new PedestrianReport { Id = draftId, Category = category, Description = DescriptionEditor.Text?.Trim() ?? "",
                Address = AddressEntry.Text?.Trim() ?? "", Latitude = latitude, Longitude = longitude,
                Severity = GravedadLabel.Text, Photo = photo, Anonymous = AnonimoSwitch.IsToggled,
                Lighting = LightingPicker.SelectedItem as string ?? "Sin evaluar", Pavement = PavementPicker.SelectedItem as string ?? "Sin evaluar",
                Accessibility = AccessibilityPicker.SelectedItem as string ?? "Sin evaluar", Noise = NoisePicker.SelectedItem as string ?? "Sin evaluar" };
            if (string.IsNullOrWhiteSpace(report.Address)) throw new ArgumentException("Escribe una dirección o referencia del lugar.");
            var shared = CommunitySwitch.IsToggled;
            if (shared) await ReportData.Instance.PublishAsync(report);
            else await LocalRepository.Instance.SaveAsync(report);
            var message = shared ? ReportData.Instance.Notice : "Guardado en este dispositivo. No se ha enviado a la comunidad.";
            ClearDraft();
            await DisplayAlert(shared ? "Reporte publicado" : "Reporte guardado", message, "OK");
            await Shell.Current.GoToAsync("//explorar");
        }
        catch (Exception ex) { await DisplayAlert("No se confirmó el guardado", ex is CommunityException or FirebaseAuthException or ArgumentException or InvalidOperationException ? ex.Message : "No fue posible confirmar el guardado. Conservamos el borrador para que puedas reintentarlo; antes revisa Actualizar comunidad.", "OK"); }
        finally { PublicarBtn.IsEnabled = true; }
    }
    private void ClearDraft()
    {
        draftId = Guid.NewGuid().ToString("N");
        photo = null;
        DescriptionEditor.Text = AddressEntry.Text = LatitudeEntry.Text = LongitudeEntry.Text = "";
        AnonimoSwitch.IsToggled = CommunitySwitch.IsToggled = false;
        LightingPicker.SelectedIndex = PavementPicker.SelectedIndex = AccessibilityPicker.SelectedIndex = NoisePicker.SelectedIndex = -1;
        AssessmentPanel.IsVisible = false;
        PhotoButton.Text = "Tomar o adjuntar foto";
        UbicacionLabel.Text = "Selecciona un punto en Bogotá";
        PhotoPreview.IsVisible = false;
        PhotoPreview.Source = null;
        if (locationPin != null) LocationMap.Map.Layers.Remove(locationPin);
        ManualCoordinates.IsVisible = false;
        var center = SphericalMercator.FromLonLat(-74.0721, 4.6505);
        LocationMap.Map.Navigator.CenterOnAndZoomTo(new MPoint(center.x, center.y), 18);
    }

}
