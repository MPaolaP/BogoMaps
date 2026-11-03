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
    public static async Task<string?> DescribeAsync(Location location)
    {
        try
        {
            var places = await Geocoding.Default.GetPlacemarksAsync(location).WaitAsync(TimeSpan.FromSeconds(5));
            var place = places?.FirstOrDefault();
            if (place == null) return null;
            var address = string.Join(", ", new[] { $"{place.Thoroughfare} {place.SubThoroughfare}".Trim(), place.SubLocality ?? place.Locality }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            return string.IsNullOrWhiteSpace(address) ? null : address;
        }
        catch (Exception) { return null; } // Reverse geocoding is optional on desktop and offline devices.
    }

}
