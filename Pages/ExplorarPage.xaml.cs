using BogoMaps.Controls;
using BogoMaps.Models;
using BogoMaps.Services;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Nts;

namespace BogoMaps.Pages;

public partial class ExplorarPage : BogoMaps.Controls.StitchPage, IQueryAttributable
{
    private List<PedestrianReport> reports = [];
    private string category = "Todos";
    private MemoryLayer? points;
    private string? selectedId;
    private bool focusRequested;
    private PedestrianReport? selected;
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("reportId", out var value)) { selectedId = value.ToString(); focusRequested = true; }
    }
    public ExplorarPage()
    {
        InitializeComponent();
        CityMap.Map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer("BogoMaps/1.0 (pedestrian local map)"));
        CityMap.Map.Tapped += (_, args) =>
        {
            if (points == null) return;
            var feature = args.GetMapInfo(new[] { points }).Feature;
            if (feature?["ReportId"] is string id)
            {
                args.Handled = true;
                selectedId = id;
                ShowSelected(reports.FirstOrDefault(r => r.Id == id));
            }
        };
        var center = SphericalMercator.FromLonLat(-74.0721, 4.6505);
        CityMap.Map.Navigator.CenterOnAndZoomTo(new MPoint(center.x, center.y), 20);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            reports = await LocalRepository.Instance.ReadAsync();
            if (focusRequested)
            {
                category = "Todos";
                UpdateChips(FilterChips.Children.OfType<Border>().First());
                SearchEntry.Text = "";
            }
            Render();
        }
        catch (Exception) { await DisplayAlert("Reportes", "No se pudieron leer los datos locales. No se han reemplazado.", "OK"); }
    }
    private void Render()
    {
        var query = SearchEntry.Text?.Trim() ?? "";
        var filtered = reports.Where(r => (category == "Todos" || r.Category == category) &&
            ($"{r.Address} {r.Description}".Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
        ReportCards.Fill(ReportList, filtered);
        ShowSelected(filtered.FirstOrDefault(r => r.Id == selectedId) ?? filtered.OrderByDescending(r => r.CreatedAt).FirstOrDefault());
        if (focusRequested && selected != null)
        {
            var center = SphericalMercator.FromLonLat(selected.Longitude, selected.Latitude);
            CityMap.Map.Navigator.CenterOnAndZoomTo(new MPoint(center.x, center.y), 5);
            focusRequested = false;
        }
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
    private void ShowSelected(PedestrianReport? report)
    {
        selected = report;
        selectedId = report?.Id;
        SelectedTitle.Text = report?.Description ?? "Explora Bogotá a pie";
        SelectedAddress.Text = report?.Address ?? "Todavía no hay reportes en esta selección. Añade un punto para empezar.";
        SelectedStatus.Text = report == null ? "TU RED PEATONAL" : $"{report.Category.ToUpperInvariant()} · {(report.Resolved ? "RESUELTO" : "ACTIVO")}";
        var appearance = report == null ? (Icon: "ui_compass", Tone: CivicTone.Blue) : CivicVisuals.Category(report.Category);
        SelectedIcon.SetAppearance(appearance.Icon, appearance.Tone);
        var stateTone = report == null ? CivicTone.Blue : CivicVisuals.State(report.Resolved, report.Severity);
        SelectedBadge.BackgroundColor = CivicVisuals.Surface(stateTone);
        SelectedStatus.TextColor = CivicVisuals.Ink(stateTone);
        SelectedUpdated.Text = report == null ? "Bogotá D.C." : $"Reportado el {report.CreatedAt.ToLocalTime():dd MMM · HH:mm}";
        SelectedAction.Text = report == null ? "Crear un reporte" : "Ver estado del punto →";
        SelectedPhoto.IsVisible = report?.Photo != null && File.Exists(report.Photo);
        SelectedPhoto.Source = SelectedPhoto.IsVisible ? report!.Photo : null;
    }
    private async void OnSelectedClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(selected == null ? "//reportar" : $"detalle?id={selected.Id}");
    private void OnZoomInClicked(object? sender, EventArgs e) => CityMap.Map.Navigator.ZoomIn();
    private void OnZoomOutClicked(object? sender, EventArgs e) => CityMap.Map.Navigator.ZoomOut();
    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => Render();
    private void OnChipTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border tapped || tapped.Parent is not HorizontalStackLayout row) return;
        var index = row.Children.IndexOf(tapped);
        category = new[] { "Todos", "Iluminación", "Accesibilidad", "Aceras", "Cruces", "Obras", "Punto seguro" }[index];
        UpdateChips(tapped);
        Render();
    }
    private void UpdateChips(Border selectedChip)
    {
        foreach (var child in FilterChips.Children.OfType<Border>())
        {
            var selected = child == selectedChip;
            child.BackgroundColor = (Microsoft.Maui.Graphics.Color)Application.Current!.Resources[selected ? "BrandPrimary" : "SurfaceLowest"];
            if (child.Content is HorizontalStackLayout contents)
            {
                foreach (var label in contents.Children.OfType<Label>()) label.TextColor = selected ? Colors.White : CivicVisuals.Resource("OnSurface");
                var index = FilterChips.Children.IndexOf(child);
                var appearance = index == 0 ? (Icon: "ui_compass", Tone: CivicTone.Blue) : CivicVisuals.Category(new[] { "Todos", "Iluminación", "Accesibilidad", "Aceras", "Cruces", "Obras", "Punto seguro" }[index]);
                foreach (var image in contents.Children.OfType<Microsoft.Maui.Controls.Image>()) image.Source = selected ? $"{appearance.Icon}_white.png" : CivicVisuals.IconSource(appearance.Icon, appearance.Tone);
            }
        }
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
