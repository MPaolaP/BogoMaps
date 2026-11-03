using Mapsui.UI.Maui.Extensions;
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
    private const double SheetPeekHeight = 112;
    private double sheetPanStartTranslation;
    private List<PedestrianReport> reports = [];
    private string category = "Todos";
    private MemoryLayer? points;
    private MemoryLayer? userLocation;
    private MPoint? pendingCenter;
    private double pendingResolution;
    private string? selectedId;
    private bool focusRequested;
    private PedestrianReport? selected;
    private ReportCursor? next;
    private bool refreshing;
    private bool scopeInitialized;
    private string? visibleScope;
    private async Task RefreshCommunityAsync(bool more)
    {
        if (refreshing) return;
        refreshing = true;
        var startScope = AuthenticationService.Instance.Current?.UserId;
        RefreshCommunityButton.IsEnabled = MoreCommunityButton.IsEnabled = false;
        try
        {
            var page = await ReportData.Instance.FeedAsync(more ? next : null);
            reports = more ? reports.Concat(page.Reports).DistinctBy(r => r.Id).ToList() : page.Reports;
            next = page.Next;
            MoreCommunityButton.IsVisible = next != null;
            CommunityNotice.Text = ReportData.Instance.Notice;
        }
        finally
        {
            refreshing = false; RefreshCommunityButton.IsEnabled = MoreCommunityButton.IsEnabled = true;
            if (AuthenticationService.Instance.Current?.UserId != startScope) { reports = []; selectedId = null; next = null; MoreCommunityButton.IsVisible = false; Render(); }
        }
    }
    private async void OnRefreshCommunityClicked(object? sender, EventArgs e) => await RefreshFromButtonAsync(false);
    private async void OnMoreCommunityClicked(object? sender, EventArgs e) => await RefreshFromButtonAsync(true);
    private async Task RefreshFromButtonAsync(bool more)
    {
        try { await RefreshCommunityAsync(more); Render(); }
        catch (Exception ex) { await DisplayAlert("Comunidad", ex is CommunityException or FirebaseAuthException ? ex.Message : "No fue posible actualizar. Tus datos locales se conservan.", "OK"); }
    }
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("reportId", out var value)) { selectedId = value.ToString(); focusRequested = true; }
    }
    public ExplorarPage()
    {
        InitializeComponent();
        CityMap.SizeChanged += (_, _) => ApplyPendingCenter();
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
        CenterMap(new MPoint(center.x, center.y), 20);
    }

    private async void OnSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        var collapsedTranslation = Math.Max(0, BottomSheet.Height - SheetPeekHeight);
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                sheetPanStartTranslation = BottomSheet.TranslationY;
                break;
            case GestureStatus.Running:
                BottomSheet.TranslationY = Math.Clamp(sheetPanStartTranslation + e.TotalY, 0, collapsedTranslation);
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var target = BottomSheet.TranslationY < collapsedTranslation / 2 ? 0 : collapsedTranslation;
                await BottomSheet.TranslateTo(0, target, 220, Easing.CubicOut);
                break;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var scope = AuthenticationService.Instance.Current?.UserId;
        if (!scopeInitialized || visibleScope != scope)
        {
            scopeInitialized = true; visibleScope = scope;
            reports = []; selectedId = null; focusRequested = false; category = "Todos";
            if (userLocation != null) { CityMap.Map.Layers.Remove(userLocation); userLocation = null; }
            UpdateChips(FilterChips.Children.OfType<Border>().First());
            SearchEntry.Text = "";
            var center = SphericalMercator.FromLonLat(-74.0721, 4.6505);
            CenterMap(new MPoint(center.x, center.y), 20);
            Render();
        }
        CommunityNotice.Text = "Actualizando reportes…";
        try
        {
            await RefreshCommunityAsync(false);
            if (focusRequested && selectedId != null && !reports.Any(r => r.Id == selectedId))
            {
                var target = await ReportData.Instance.GetAsync(selectedId);
                if (target != null) reports.Add(target);
            }
            if (focusRequested)
            {
                category = "Todos";
                UpdateChips(FilterChips.Children.OfType<Border>().First());
                SearchEntry.Text = "";
            }
            Render();
        }
        catch (Exception ex) { await DisplayAlert("Reportes", ex is CommunityException or FirebaseAuthException ? ex.Message : "No se pudieron actualizar los reportes. Los datos locales se conservan.", "OK"); }
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
            CenterMap(new MPoint(center.x, center.y), 5);
            focusRequested = false;
        }
        if (points != null) CityMap.Map.Layers.Remove(points);
        points = new MemoryLayer { Name = "Reportes", Features = filtered.Select(r =>
        {
            var position = SphericalMercator.FromLonLat(r.Longitude, r.Latitude);
            var feature = new GeometryFeature { Geometry = new NetTopologySuite.Geometries.Point(position.x, position.y), Styles = { new SymbolStyle { Fill = new Mapsui.Styles.Brush(CivicVisuals.Ink(CivicVisuals.State(r.Resolved, r.Severity)).ToMapsui()), SymbolScale = .6 } } };
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
        SelectedStatus.Text = report == null ? "TU RED PEATONAL" : $"{(report.IsDemo ? "PRUEBA · " : "")}{report.Category.ToUpperInvariant()} · {(report.Resolved ? "RESUELTO" : "ACTIVO")}";
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
    private void CenterMap(MPoint point, double resolution)
    {
        pendingCenter = point;
        pendingResolution = resolution;
        ApplyPendingCenter();
    }
    private void ApplyPendingCenter()
    {
        if (pendingCenter == null || CityMap.Width <= 0 || CityMap.Height <= 0) return;
        CityMap.Map.Navigator.CenterOnAndZoomTo(pendingCenter, pendingResolution);
        pendingCenter = null;
    }
    private async void OnNearMeClicked(object? sender, EventArgs e)
    {
        try
        {
            var location = await DeviceLocation.GetAsync();
            if (location.Latitude is < 3.7 or > 4.9 || location.Longitude is < -74.6 or > -73.9)
            {
                await DisplayAlert("Cerca de mí", "Tu ubicación está fuera del área de Bogotá. Puedes explorar la ciudad usando el mapa.", "OK");
                return;
            }
            var point = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
            CenterMap(new MPoint(point.x, point.y), 5);
            if (userLocation != null) CityMap.Map.Layers.Remove(userLocation);
            userLocation = new MemoryLayer { Name = "Tu ubicación", Features = new[] { new GeometryFeature {
                Geometry = new NetTopologySuite.Geometries.Point(point.x, point.y),
                Styles = { new SymbolStyle { Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Blue), SymbolScale = .5 } } } } };
            CityMap.Map.Layers.Add(userLocation);
            var search = SearchEntry.Text?.Trim() ?? "";
            var nearest = reports.Where(r => (category == "Todos" || r.Category == category) && $"{r.Address} {r.Description}".Contains(search, StringComparison.OrdinalIgnoreCase))
                .Select(r => new { Report = r, Km = Location.CalculateDistance(location, r.Latitude, r.Longitude, DistanceUnits.Kilometers) })
                .Where(x => x.Km <= 30).OrderBy(x => x.Km).FirstOrDefault();
            ShowSelected(nearest?.Report);
            CityMap.Map.Refresh();
        }
        catch (Exception ex) { await DisplayAlert("Ubicación", ex.Message, "OK"); }
    }
}
