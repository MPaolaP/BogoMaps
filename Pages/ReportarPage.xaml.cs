using BogoMaps.Models;
using BogoMaps.Services;

namespace BogoMaps.Pages;

public partial class ReportarPage : ContentPage
{
    private const string UbicacionVacia = "Aún no has elegido una ubicación";

    // Coinciden con la categoría y la gravedad preseleccionadas en el XAML.
    private CategoriaPunto _categoria = CategoriaPunto.Aceras;
    private string _tituloCategoria = "Acera rota / Obstáculo";
    private GravedadPunto _gravedad = GravedadPunto.Moderado;

    private Location? _ubicacion;
    private string? _fotoArchivo;

    public ReportarPage()
    {
        InitializeComponent();
    }

    private static Color Res(string key) => (Color)Application.Current!.Resources[key];

    private void OnCategoriaTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border tapped)
            return;

        foreach (var child in CategoriaGrid.Children.OfType<Border>())
        {
            var selected = child == tapped;
            child.BackgroundColor = Res(selected ? "BrandPrimary" : "SurfaceLow");
            if (child.Content is Label label)
                label.TextColor = selected ? Colors.White : Res("OnSurface");
        }

        // "Seguro" es un punto recomendado: va en Seguridad y no tiene sentido con gravedad alta.
        var parametro = e.Parameter as string;
        if (parametro == "Seguro")
        {
            _categoria = CategoriaPunto.Seguridad;
            AplicarGravedad(GravedadPunto.Bajo);
        }
        else if (Enum.TryParse<CategoriaPunto>(parametro, out var categoria))
        {
            _categoria = categoria;
        }

        // El texto de la tarjeta lleva un emoji y un espacio antes del nombre.
        if ((tapped.Content as Label)?.Text is { } texto)
            _tituloCategoria = texto[(texto.IndexOf(' ') + 1)..];
    }

    private void OnGravedadClicked(object? sender, EventArgs e)
    {
        if (sender == BtnBajo)
            AplicarGravedad(GravedadPunto.Bajo);
        else if (sender == BtnModerado)
            AplicarGravedad(GravedadPunto.Moderado);
        else
            AplicarGravedad(GravedadPunto.Critico);
    }

    private void AplicarGravedad(GravedadPunto gravedad)
    {
        _gravedad = gravedad;

        foreach (var btn in new[] { BtnBajo, BtnModerado, BtnCritico })
        {
            btn.BackgroundColor = Colors.Transparent;
            btn.TextColor = Res("OnSurfaceVariant");
        }

        var (boton, fondo, texto, etiqueta, colorEtiqueta) = gravedad switch
        {
            GravedadPunto.Bajo => (BtnBajo, Res("SecondaryContainer"), Res("OnSecondaryContainer"), "Bajo", Res("BrandSecondary")),
            GravedadPunto.Moderado => (BtnModerado, Res("TertiaryFixed"), Res("OnTertiaryFixed"), "Moderado", Res("TertiaryContainer")),
            _ => (BtnCritico, Res("ErrorContainer"), Res("OnErrorContainer"), "Crítico", Res("BrandError"))
        };

        boton.BackgroundColor = fondo;
        boton.TextColor = texto;
        GravedadLabel.Text = etiqueta;
        GravedadLabel.TextColor = colorEtiqueta;
    }

    private async void OnGpsClicked(object? sender, EventArgs e)
    {
        var textoOriginal = GpsBtn.Text;
        GpsBtn.IsEnabled = false;
        GpsBtn.Text = "Obteniendo ubicación...";

        try
        {
            var permiso = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (permiso != PermissionStatus.Granted)
            {
                await DisplayAlert("Ubicación", "Necesitamos tu permiso de ubicación para ubicar el reporte.", "OK");
                return;
            }

            var ubicacion = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(15)));

            if (ubicacion is null)
            {
                await DisplayAlert("Ubicación", "No pudimos obtener tu ubicación. Inténtalo de nuevo.", "OK");
                return;
            }

            _ubicacion = ubicacion;
            UbicacionLabel.Text = await DescribirUbicacionAsync(ubicacion);
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException)
        {
            await DisplayAlert("Ubicación", "La ubicación no está disponible o está desactivada en este dispositivo.", "OK");
        }
        finally
        {
            GpsBtn.Text = textoOriginal;
            GpsBtn.IsEnabled = true;
        }
    }

    /// <summary>Intenta convertir las coordenadas en una dirección legible; si no se puede, devuelve las coordenadas.</summary>
    private static async Task<string> DescribirUbicacionAsync(Location ubicacion)
    {
        var coordenadas = $"{ubicacion.Latitude:0.00000}, {ubicacion.Longitude:0.00000}";

        try
        {
            var lugares = await Geocoding.Default.GetPlacemarksAsync(ubicacion).WaitAsync(TimeSpan.FromSeconds(5));
            var lugar = lugares?.FirstOrDefault();
            if (lugar is null)
                return coordenadas;

            var partes = new[]
            {
                $"{lugar.Thoroughfare} {lugar.SubThoroughfare}".Trim(),
                lugar.SubLocality ?? lugar.Locality
            }.Where(s => !string.IsNullOrWhiteSpace(s));

            var direccion = string.Join(", ", partes);
            return string.IsNullOrWhiteSpace(direccion) ? coordenadas : direccion;
        }
        catch (Exception)
        {
            // El servicio de direcciones no está disponible en todas las plataformas; las coordenadas bastan.
            return coordenadas;
        }
    }

    private async void OnFotoClicked(object? sender, EventArgs e)
    {
        var opcion = await DisplayActionSheet("Evidencia visual", "Cancelar", null, "Tomar foto", "Elegir de la galería");

        try
        {
            FileResult? foto;
            if (opcion == "Tomar foto")
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    await DisplayAlert("Cámara", "Este dispositivo no tiene una cámara disponible. Puedes elegir una foto de la galería.", "OK");
                    return;
                }

                foto = await MediaPicker.Default.CapturePhotoAsync();
            }
            else if (opcion == "Elegir de la galería")
            {
                foto = await MediaPicker.Default.PickPhotoAsync();
            }
            else
            {
                return;
            }

            if (foto is null)
                return;

            var anterior = _fotoArchivo;
            _fotoArchivo = await PuntosService.Compartido.GuardarFotoAsync(foto);
            if (anterior is not null)
                PuntosService.EliminarFoto(anterior);

            MostrarFoto();
        }
        catch (PermissionException)
        {
            await DisplayAlert("Cámara", "Necesitamos tu permiso para usar la cámara o las fotos.", "OK");
        }
        catch (Exception)
        {
            await DisplayAlert("Foto", "No pudimos obtener la foto. Inténtalo de nuevo.", "OK");
        }
    }

    private void MostrarFoto()
    {
        var hayFoto = _fotoArchivo is not null;
        FotoBorde.IsVisible = hayFoto;
        FotoImage.Source = hayFoto ? ImageSource.FromFile(PuntosService.RutaFoto(_fotoArchivo!)) : null;
        FotoBtn.Text = hayFoto ? "\U0001F4F7 Cambiar foto" : "\U0001F4F7 Agregar foto";
    }

    private async void OnPublicarClicked(object? sender, EventArgs e)
    {
        if (_ubicacion is null)
        {
            await DisplayAlert("Falta la ubicación", "Toca \"Usar mi ubicación GPS actual\" para ubicar el reporte en el mapa.", "OK");
            return;
        }

        PublicarBtn.IsEnabled = false;
        PublicarBtn.Text = "Publicando reporte...";

        try
        {
            var reporte = new PuntoUrbano
            {
                Titulo = _tituloCategoria,
                Ubicacion = UbicacionLabel.Text,
                Categoria = _categoria,
                Gravedad = _gravedad,
                Latitud = _ubicacion.Latitude,
                Longitud = _ubicacion.Longitude,
                Descripcion = DescripcionEditor.Text?.Trim() ?? "",
                FotoArchivo = _fotoArchivo,
                FechaReporte = DateTime.UtcNow,
                Detalles =
                [
                    new DetallePunto { Titulo = "Estado", Valor = "Reporte nuevo" },
                    new DetallePunto { Titulo = "Reportado por", Valor = AnonimoSwitch.IsToggled ? "Anónimo" : "Un vecino" }
                ]
            };

            await PuntosService.Compartido.AgregarReporteAsync(reporte);

            PublicarBtn.BackgroundColor = Res("BrandSecondary");
            PublicarBtn.Text = "¡Reporte Registrado!";
            await Task.Delay(700);

            ReiniciarFormulario();
            await Shell.Current.GoToAsync($"//explorar?puntoId={reporte.Id}");
        }
        catch (Exception)
        {
            await DisplayAlert("Reporte", "No pudimos guardar el reporte. Inténtalo de nuevo.", "OK");
            ReiniciarBotonPublicar();
        }
    }

    private void ReiniciarBotonPublicar()
    {
        PublicarBtn.ClearValue(Button.BackgroundColorProperty);
        PublicarBtn.Text = "Publicar Reporte en el Mapa";
        PublicarBtn.IsEnabled = true;
    }

    private void ReiniciarFormulario()
    {
        _ubicacion = null;
        UbicacionLabel.Text = UbicacionVacia;

        // La foto ya quedó asociada al reporte publicado, así que no se borra.
        _fotoArchivo = null;
        MostrarFoto();

        DescripcionEditor.Text = "";
        AnonimoSwitch.IsToggled = false;
        ReiniciarBotonPublicar();
    }
}
