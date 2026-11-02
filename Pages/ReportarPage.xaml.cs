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
                foreach (var image in content.Children.OfType<Image>()) image.Source = new[] { "ui_path", "ui_light", "ui_access", "ui_warning", "ui_tools", "ui_shield" }[index] + (selected ? "_white.png" : ".png");
            }
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
            SetLocation(location.Latitude, location.Longitude, true);
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
            using (var input = await file.OpenReadAsync())
            using (var output = File.Create(destination)) await input.CopyToAsync(output);
            if (photo != null) File.Delete(photo);
            photo = destination;
            PhotoButton.Text = "Foto adjunta · cambiar";
            PhotoPreview.Source = photo;
            PhotoPreview.IsVisible = true;
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
                Severity = GravedadLabel.Text, Photo = photo, Anonymous = AnonimoSwitch.IsToggled,
                Lighting = LightingPicker.SelectedItem as string ?? "Sin evaluar", Pavement = PavementPicker.SelectedItem as string ?? "Sin evaluar",
                Accessibility = AccessibilityPicker.SelectedItem as string ?? "Sin evaluar", Noise = NoisePicker.SelectedItem as string ?? "Sin evaluar" };
            if (string.IsNullOrWhiteSpace(report.Address)) throw new ArgumentException("Escribe una dirección o referencia del lugar.");
            await LocalRepository.Instance.SaveAsync(report);
            photo = null;
            DescriptionEditor.Text = AddressEntry.Text = LatitudeEntry.Text = LongitudeEntry.Text = "";
            AnonimoSwitch.IsToggled = false;
            LightingPicker.SelectedIndex = PavementPicker.SelectedIndex = AccessibilityPicker.SelectedIndex = NoisePicker.SelectedIndex = -1;
            AssessmentPanel.IsVisible = false;
            PhotoButton.Text = "Adjuntar foto";
            UbicacionLabel.Text = "Selecciona un punto en Bogotá";
            PhotoPreview.IsVisible = false;
            PhotoPreview.Source = null;
            if (locationPin != null) LocationMap.Map.Layers.Remove(locationPin);
            await DisplayAlert("Reporte guardado", "Disponible en tu mapa local. No se ha enviado a un servidor.", "OK");
            await Shell.Current.GoToAsync("//explorar");
        }
        catch (Exception ex) { await DisplayAlert("No se guardó el reporte", ex.Message, "OK"); }
        finally { PublicarBtn.IsEnabled = true; }
    }
}
