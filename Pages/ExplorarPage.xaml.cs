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
    private int selectionVersion;
    private bool updatingSelection;
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
        ReportSheet.CoverageChanged += (_, _) => UpdateMapOverlays();
        ReportSheet.PositionChanged += async (_, _) =>
        {
            UpdateSheetDetails();
            if (ReportSheet.Position == MapSheetPosition.Expanded) await ReloadSelectedAsync();
        };
        CityMap.Map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer("BogoMaps/1.0 (pedestrian local map)"));
        CityMap.Map.Tapped += async (_, args) =>
        {
            if (points == null) return;
            var feature = args.GetMapInfo(new[] { points }).Feature;
            if (feature?["ReportId"] is string id)
            {
                args.Handled = true;
                selectedId = id;
                ShowSelected(reports.FirstOrDefault(r => r.Id == id));
                SearchEntry.Unfocus();
                await ReportSheet.MoveToAsync(MapSheetPosition.Half);
            }
            else await ReportSheet.MoveToAsync(MapSheetPosition.Peek);
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
            await ReportSheet.MoveToAsync(MapSheetPosition.Peek, false);
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
            if (ReportSheet.Position == MapSheetPosition.Expanded) await ReloadSelectedAsync();
        }
        catch (Exception ex) { await DisplayAlert("Reportes", ex is CommunityException or FirebaseAuthException ? ex.Message : "No se pudieron actualizar los reportes. Los datos locales se conservan.", "OK"); }
    }
    protected override void OnDisappearing()
    {
        selectionVersion++;
        ReportSheet.CancelMotion();
        base.OnDisappearing();
    }
    private void Render()
    {
        var query = SearchEntry.Text?.Trim() ?? "";
        var filtered = reports.Where(r => (category == "Todos" || r.Category == category) &&
            ($"{r.Address} {r.Description}".Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
        ReportCards.Fill(ReportList, filtered, OnReportChosenAsync);
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
        selectionVersion++;
        selected = report;
        selectedId = report?.Id;
        ReportSheet.Title = report?.Description ?? "Explora Bogotá a pie";
        ReportSheet.Subtitle = report?.Address ?? "Desliza hacia arriba para ver reportes";
        SelectedFullDescription.Text = report?.Description ?? "";
        SelectedAddress.Text = report?.Address ?? "Todavía no hay reportes en esta selección. Añade un punto para empezar.";
        SelectedStatus.Text = report == null ? "TU RED PEATONAL" : $"{(report.IsDemo ? "PRUEBA · " : "")}{report.Category.ToUpperInvariant()} · {(report.Resolved ? "RESUELTO" : "ACTIVO")}";
        var appearance = report == null ? (Icon: "ui_compass", Tone: CivicTone.Blue) : CivicVisuals.Category(report.Category);
        SelectedIcon.SetAppearance(appearance.Icon, appearance.Tone);
        var stateTone = report == null ? CivicTone.Blue : CivicVisuals.State(report.Resolved, report.Severity);
        SelectedBadge.BackgroundColor = CivicVisuals.Surface(stateTone);
        SelectedStatus.TextColor = CivicVisuals.Ink(stateTone);
        SelectedUpdated.Text = report == null ? "Bogotá D.C." : $"Reportado el {report.CreatedAt.ToLocalTime():dd MMM · HH:mm}";
        SelectedAction.Text = report == null ? "Crear un reporte" : "Más información ↑";
        SelectedPhoto.IsVisible = report?.Photo != null && File.Exists(report.Photo);
        SelectedPhoto.Source = SelectedPhoto.IsVisible ? report!.Photo : null;
        SheetLighting.Text = report?.Lighting ?? "Sin evaluar";
        SheetPavement.Text = report?.Pavement ?? "Sin evaluar";
        SheetAccessibility.Text = report?.Accessibility ?? "Sin evaluar";
        SheetNoise.Text = report?.Noise ?? "Sin evaluar";
        foreach (var label in new[] { SheetLighting, SheetPavement, SheetAccessibility, SheetNoise }) label.TextColor = CivicVisuals.Ink(CivicVisuals.Assessment(label.Text));
        SheetSaveButton.Text = report?.Saved == true ? "Quitar de guardados" : "Guardar punto";
        SheetSaveButton.ImageSource = report?.Saved == true ? "ui_save_green.png" : "ui_save_blue.png";
        SheetConfirmButton.Text = report?.Confirmed == true ? "Confirmado por ti" : "Confirmar estado";
        SheetConfirmButton.IsEnabled = report != null && !report.Confirmed;
        SheetResolveButton.Text = report?.Resolved == true ? "Reabrir reporte" : "Marcar como resuelto";
        SheetResolveButton.IsVisible = report != null && (!report.IsShared || report.OwnerId == AuthenticationService.Instance.Current?.UserId);
        SheetConnectionNotice.Text = report?.IsShared == true ? ReportData.Instance.Notice : "Datos guardados en este dispositivo.";
        UpdateSheetDetails();
    }
    private void UpdateSheetDetails()
    {
        ExpandedDetails.IsVisible = selected != null && ReportSheet.Position == MapSheetPosition.Expanded;
        SelectedAction.IsVisible = selected == null || ReportSheet.Position != MapSheetPosition.Expanded;
        SheetActions.IsEnabled = selected != null && !updatingSelection;
    }
    private void UpdateMapOverlays()
    {
        var available = ReportSheet.MapVisibleHeight;
        MapAttribution.Margin = new Thickness(10, 0, 10, ReportSheet.VisibleHeight + 8);
        MapZoomButtons.Margin = new Thickness(12, 0, 12, ReportSheet.VisibleHeight + 34);
        MapAttribution.IsVisible = available > 145;
        MapZoomButtons.IsVisible = available > 270;
        SearchOverlay.IsVisible = available > 135;
        if (!SearchOverlay.IsVisible && SearchEntry.IsFocused) SearchEntry.Unfocus();
    }
    private async void OnSelectedClicked(object? sender, EventArgs e)
    {
        if (selected == null) await Shell.Current.GoToAsync("//reportar");
        else await ReportSheet.MoveToAsync(MapSheetPosition.Expanded);
    }
    private async Task OnReportChosenAsync(PedestrianReport report)
    {
        ShowSelected(report);
        var point = SphericalMercator.FromLonLat(report.Longitude, report.Latitude);
        CenterMap(new MPoint(point.x, point.y), 5);
        if (ReportSheet.Position == MapSheetPosition.Expanded) await ReloadSelectedAsync();
        else await ReportSheet.MoveToAsync(MapSheetPosition.Expanded);
    }
    private async Task ReloadSelectedAsync()
    {
        if (selected == null || updatingSelection) return;
        var version = selectionVersion;
        var id = selected.Id;
        var scope = AuthenticationService.Instance.Current?.UserId;
        SheetActions.IsEnabled = false;
        try
        {
            var current = await ReportData.Instance.GetAsync(id);
            if (version != selectionVersion || scope != AuthenticationService.Instance.Current?.UserId) return;
            if (current == null) { SheetConnectionNotice.Text = "Este reporte ya no está disponible."; return; }
            var index = reports.FindIndex(r => r.Id == id);
            if (index >= 0) reports[index] = current;
            ShowSelected(current);
        }
        catch (Exception ex)
        {
            if (version == selectionVersion) SheetConnectionNotice.Text = ex is CommunityException or FirebaseAuthException ? ex.Message : "No fue posible actualizar el punto; se muestra la información disponible.";
        }
        finally { if (selected?.Id == id) UpdateSheetDetails(); }
    }
    private async void OnSheetSaveClicked(object? sender, EventArgs e) => await ChangeSelectionAsync("save");
    private async void OnSheetConfirmClicked(object? sender, EventArgs e) => await ChangeSelectionAsync("confirm");
    private async void OnSheetResolveClicked(object? sender, EventArgs e) => await ChangeSelectionAsync("resolve");
    private async Task ChangeSelectionAsync(string action)
    {
        if (selected == null || updatingSelection) return;
        updatingSelection = true;
        SheetActions.IsEnabled = false;
        var id = selected.Id;
        var scope = AuthenticationService.Instance.Current?.UserId;
        try
        {
            var current = await ReportData.Instance.ChangeAsync(selected, action);
            if (scope != AuthenticationService.Instance.Current?.UserId) return;
            var index = reports.FindIndex(r => r.Id == id);
            if (index >= 0) reports[index] = current;
            if (selected?.Id == id) { selectedId = id; Render(); SheetConnectionNotice.Text = ReportData.Instance.Notice; }
        }
        catch (Exception ex) { await DisplayAlert("No se confirmó el cambio", ex is CommunityException or FirebaseAuthException or InvalidOperationException ? ex.Message : "No fue posible guardar el cambio. Revisa la conexión.", "OK"); }
        finally { updatingSelection = false; UpdateSheetDetails(); }
    }
    private async void OnSearchCompleted(object? sender, EventArgs e)
    {
        SearchEntry.Unfocus();
        await ReportSheet.MoveToAsync(MapSheetPosition.Expanded);
    }
    private async void OnSearchFocused(object? sender, FocusEventArgs e)
    {
        if (ReportSheet.Position != MapSheetPosition.Peek) await ReportSheet.MoveToAsync(MapSheetPosition.Peek);
    }
    private void OnZoomInClicked(object? sender, EventArgs e) => CityMap.Map.Navigator.ZoomIn();
    private void OnZoomOutClicked(object? sender, EventArgs e) => CityMap.Map.Navigator.ZoomOut();
    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => Render();
    private async void OnChipTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border tapped || tapped.Parent is not HorizontalStackLayout row) return;
        var index = row.Children.IndexOf(tapped);
        category = new[] { "Todos", "Iluminación", "Accesibilidad", "Aceras", "Cruces", "Obras", "Punto seguro" }[index];
        UpdateChips(tapped);
        Render();
        if (ReportSheet.Position == MapSheetPosition.Peek) await ReportSheet.MoveToAsync(MapSheetPosition.Half);
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
            await ReportSheet.MoveToAsync(MapSheetPosition.Half);
        }
        catch (Exception ex) { await DisplayAlert("Ubicación", ex.Message, "OK"); }
    }
}
