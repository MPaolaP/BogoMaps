using BogoMaps.Models;
using BogoMaps.Services;
using Mapsui;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Maui;

namespace BogoMaps.Pages;

[QueryProperty(nameof(PuntoId), "puntoId")]
public partial class ExplorarPage : ContentPage
{
    // Centro aproximado de la zona con más puntos (Chapinero) y resolución inicial del mapa.
    private const double CentroLat = 4.6486;
    private const double CentroLon = -74.0620;
    private const double ResolucionInicial = 35;

    // Si el usuario está más lejos que esto de cualquier punto, asumimos que no está en Bogotá.
    private const double DistanciaMaximaKm = 30;

    private IReadOnlyList<PuntoUrbano> _puntos = [];
    private CategoriaPunto? _categoriaActiva;

    /// <summary>Id del punto a enfocar al llegar desde otra pantalla (p. ej. un reporte recién publicado).</summary>
    public string? PuntoId { get; set; }

    public ExplorarPage()
    {
        InitializeComponent();

        Mapa.Map.Layers.Add(OpenStreetMap.CreateTileLayer("BogoMaps"));
        Mapa.PinClicked += OnPinClicked;
        Mapa.MapClicked += (_, _) => TarjetaPunto.IsVisible = false;
        Mapa.IsMyLocationButtonVisible = false;
        Mapa.IsNorthingButtonVisible = false;

        Mapa.SizeChanged += OnMapaSizeChanged;
    }

    // El navegador de Mapsui ignora el centrado hasta que el control tiene tamaño, así que se hace una sola vez aquí.
    private void OnMapaSizeChanged(object? sender, EventArgs e)
    {
        if (Mapa.Width <= 0 || Mapa.Height <= 0)
            return;

        Mapa.SizeChanged -= OnMapaSizeChanged;
        CentrarEn(CentroLat, CentroLon, ResolucionInicial);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Se recarga en cada visita para incluir los reportes publicados desde la pestaña Reportar.
        _puntos = await PuntosService.Compartido.ObtenerAsync();
        TodosLabel.Text = $"Todos ({_puntos.Count})";
        ActualizarPines();

        if (PuntoId is not null)
        {
            var id = PuntoId;
            PuntoId = null;

            if (_puntos.FirstOrDefault(p => p.Id.ToString() == id) is { } enfocado)
            {
                CentrarEn(enfocado.Latitud, enfocado.Longitud, 6);
                MostrarPunto(enfocado);
            }
        }
    }

    private static Color Res(string key) => (Color)Application.Current!.Resources[key];

    private void CentrarEn(double lat, double lon, double resolucion)
    {
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);
        Mapa.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), resolucion);
    }

    private IEnumerable<PuntoUrbano> PuntosVisibles()
    {
        var texto = BusquedaEntry.Text?.Trim();

        return _puntos.Where(p =>
            (_categoriaActiva is null || p.Categoria == _categoriaActiva) &&
            (string.IsNullOrEmpty(texto) ||
             p.Titulo.Contains(texto, StringComparison.CurrentCultureIgnoreCase) ||
             p.Ubicacion.Contains(texto, StringComparison.CurrentCultureIgnoreCase)));
    }

    private void ActualizarPines()
    {
        Mapa.Pins.Clear();

        foreach (var p in PuntosVisibles())
        {
            Mapa.Pins.Add(new Pin(Mapa)
            {
                Position = new Position(p.Latitud, p.Longitud),
                Label = p.Titulo,
                Type = PinType.Pin,
                Color = ColorGravedad(p.Gravedad),
                Scale = 0.8f,
                Tag = p
            });
        }

        if (TarjetaPunto.IsVisible && TarjetaPunto.BindingContext is PuntoUrbano actual && !PuntosVisibles().Contains(actual))
            TarjetaPunto.IsVisible = false;

        Mapa.Refresh();
    }

    private static Color ColorGravedad(GravedadPunto gravedad) => gravedad switch
    {
        GravedadPunto.Bajo => Res("BrandSecondary"),
        GravedadPunto.Moderado => Res("TertiaryContainer"),
        _ => Res("BrandError")
    };

    private void OnPinClicked(object? sender, PinClickedEventArgs e)
    {
        // Mostramos nuestra tarjeta en lugar del callout por defecto de Mapsui.
        e.Handled = true;

        if (e.Pin?.Tag is PuntoUrbano punto)
            MostrarPunto(punto);
    }

    private void MostrarPunto(PuntoUrbano p)
    {
        TarjetaPunto.BindingContext = p;

        var (fondo, texto, etiqueta) = p.Gravedad switch
        {
            GravedadPunto.Bajo => (Res("SecondaryContainer"), Res("OnSecondaryContainer"), "Seguro"),
            GravedadPunto.Moderado => (Res("TertiaryFixed"), Res("OnTertiaryFixed"), "Requiere atención"),
            _ => (Res("ErrorContainer"), Res("OnErrorContainer"), "Crítico")
        };

        EstadoBorde.BackgroundColor = fondo;
        EstadoLabel.TextColor = texto;
        EstadoLabel.Text = etiqueta;

        TituloLabel.Text = p.Titulo;
        UbicacionLabel.Text = p.Ubicacion;
        // Un reporte nuevo aún no tiene calificación ni confirmaciones.
        CalificacionLabel.Text = p.Calificacion > 0 ? $"★ {p.Calificacion:0.0}" : "Nuevo";
        ConfirmacionesLabel.Text = p.Confirmaciones > 0 ? $"{p.Confirmaciones} peatones" : "Sin confirmar";

        DescripcionLabel.Text = p.Descripcion;
        DescripcionLabel.IsVisible = !string.IsNullOrWhiteSpace(p.Descripcion);

        var rutaFoto = p.FotoArchivo is null ? null : PuntosService.RutaFoto(p.FotoArchivo);
        var hayFoto = rutaFoto is not null && File.Exists(rutaFoto);
        FotoImage.Source = hayFoto ? ImageSource.FromFile(rutaFoto) : null;
        FotoBorde.IsVisible = hayFoto;

        MostrarDetalle(Detalle1Titulo, Detalle1Valor, p.Detalles.ElementAtOrDefault(0));
        MostrarDetalle(Detalle2Titulo, Detalle2Valor, p.Detalles.ElementAtOrDefault(1));

        TarjetaPunto.IsVisible = true;
    }

    private static void MostrarDetalle(Label titulo, Label valor, DetallePunto? detalle)
    {
        titulo.Text = detalle?.Titulo ?? "";
        valor.Text = detalle?.Valor ?? "";
    }

    private void OnChipTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border tapped || tapped.Parent is not HorizontalStackLayout row)
            return;

        foreach (var child in row.Children.OfType<Border>())
        {
            var selected = child == tapped;
            child.BackgroundColor = Res(selected ? "BrandPrimary" : "SurfaceLowest");
            if (child.Content is Label label)
                label.TextColor = selected ? Colors.White : Res("OnSurface");
        }

        _categoriaActiva = Enum.TryParse<CategoriaPunto>(e.Parameter as string, out var categoria) ? categoria : null;
        ActualizarPines();
    }

    private void OnBusquedaChanged(object? sender, TextChangedEventArgs e) => ActualizarPines();

    private async void OnNearMeClicked(object? sender, EventArgs e)
    {
        try
        {
            var permiso = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (permiso != PermissionStatus.Granted)
            {
                await DisplayAlert("Ubicación", "Necesitamos tu permiso de ubicación para mostrar los puntos cercanos.", "OK");
                return;
            }

            var ubicacion = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));

            if (ubicacion is null)
            {
                await DisplayAlert("Ubicación", "No pudimos obtener tu ubicación. Inténtalo de nuevo.", "OK");
                return;
            }

            var cercano = PuntosVisibles()
                .Select(p => (Punto: p, Km: Location.CalculateDistance(ubicacion, p.Latitud, p.Longitud, DistanceUnits.Kilometers)))
                .OrderBy(t => t.Km)
                .FirstOrDefault();

            if (cercano.Punto is null || cercano.Km > DistanciaMaximaKm)
            {
                await DisplayAlert("Cerca de mí", "Parece que no estás en Bogotá, así que no hay puntos cercanos para mostrar.", "OK");
                return;
            }

            Mapa.MyLocationLayer.UpdateMyLocation(new Position(ubicacion.Latitude, ubicacion.Longitude));
            CentrarEn(ubicacion.Latitude, ubicacion.Longitude, 6);
            MostrarPunto(cercano.Punto);
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException)
        {
            await DisplayAlert("Ubicación", "La ubicación no está disponible o está desactivada en este dispositivo.", "OK");
        }
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        await DisplayAlert("¡Gracias, vecino!", "Tu aporte ayuda a mantener Bogotá caminable.", "OK");
    }
}
