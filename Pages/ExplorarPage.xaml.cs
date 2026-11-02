using BogoMaps.Models;
using BogoMaps.Services;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Nts;

namespace BogoMaps.Pages;

public partial class ExplorarPage : ContentPage
{
    private List<PedestrianReport> reports = [];
    private string category = "Todos";
    private MemoryLayer? points;
    public ExplorarPage()
    {
        InitializeComponent();
        CityMap.Map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer("BogoMaps/1.0 (pedestrian local map)"));
        CityMap.Map.Tapped += async (_, args) =>
        {
            if (points == null) return;
            var feature = args.GetMapInfo(new[] { points }).Feature;
            if (feature?["ReportId"] is string id)
            {
                args.Handled = true;
                await Shell.Current.GoToAsync($"detalle?id={id}");
            }
        };
        var center = SphericalMercator.FromLonLat(-74.0721, 4.6505);
        CityMap.Map.Navigator.CenterOnAndZoomTo(new MPoint(center.x, center.y), 20);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { reports = await LocalRepository.Instance.ReadAsync(); Render(); }
        catch (Exception) { await DisplayAlert("Reportes", "No se pudieron leer los datos locales. No se han reemplazado.", "OK"); }
    }
    private void Render()
    {
        var query = SearchEntry.Text?.Trim() ?? "";
        var filtered = reports.Where(r => (category == "Todos" || r.Category == category) &&
            ($"{r.Address} {r.Description}".Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
        ReportCards.Fill(ReportList, filtered);
        if (points != null) CityMap.Map.Layers.Remove(points);
        points = new MemoryLayer { Name = "Reportes", Features = filtered.Select(r =>
        {
            var position = SphericalMercator.FromLonLat(r.Longitude, r.Latitude);
            var feature = new GeometryFeature { Geometry = new NetTopologySuite.Geometries.Point(position.x, position.y), Styles = { new SymbolStyle { Fill = new Mapsui.Styles.Brush(r.Resolved ? Mapsui.Styles.Color.Green : Mapsui.Styles.Color.Red), SymbolScale = .6 } } };
            feature["ReportId"] = r.Id;
            return feature;
        }).ToArray() };
        CityMap.Map.Layers.Add(points);
        CityMap.Map.Refresh();
    }
    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => Render();
    private void OnChipTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border tapped || tapped.Parent is not HorizontalStackLayout row) return;
        var index = row.Children.IndexOf(tapped);
        category = new[] { "Todos", "Iluminación", "Accesibilidad", "Aceras", "Cruces", "Obras", "Punto seguro" }[index];
        foreach (var child in row.Children.OfType<Border>())
        {
            var selected = child == tapped;
            child.BackgroundColor = (Microsoft.Maui.Graphics.Color)Application.Current!.Resources[selected ? "BrandPrimary" : "SurfaceLowest"];
            if (child.Content is Label label) label.TextColor = selected ? Colors.White : (Microsoft.Maui.Graphics.Color)Application.Current.Resources["OnSurface"];
        }
        Render();
    }
    private async void OnNearMeClicked(object? sender, EventArgs e)
    {
        try
        {
            var location = await DeviceLocation.GetAsync();
            var point = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
            CityMap.Map.Navigator.CenterOnAndZoomTo(new MPoint(point.x, point.y), 5);
        }
        catch (Exception ex) { await DisplayAlert("Ubicación", ex.Message, "OK"); }
    }
}
