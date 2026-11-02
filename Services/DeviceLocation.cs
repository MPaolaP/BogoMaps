namespace BogoMaps.Services;

public static class DeviceLocation
{
    public static async Task<Location> GetAsync()
    {
        var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (permission != PermissionStatus.Granted)
            throw new InvalidOperationException("Permite el acceso a tu ubicación en los ajustes del dispositivo o introduce las coordenadas manualmente.");
        return await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(20)))
            ?? throw new InvalidOperationException("No se pudo obtener tu ubicación. Comprueba que el GPS esté encendido.");
    }
}
