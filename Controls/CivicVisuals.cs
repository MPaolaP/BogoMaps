namespace BogoMaps.Controls;

public enum CivicTone { Blue, Green, Amber, Red, Muted }

public static class CivicVisuals
{
    public static Color Surface(CivicTone tone) => Resource(tone switch
    {
        CivicTone.Green => "SecondaryContainer", CivicTone.Amber => "TertiaryFixed",
        CivicTone.Red => "ErrorContainer", CivicTone.Muted => "SurfaceContainer", _ => "PrimaryFixed"
    });
    public static Color Ink(CivicTone tone) => Resource(tone switch
    {
        CivicTone.Green => "OnSecondaryContainer", CivicTone.Amber => "OnTertiaryFixedVariant",
        CivicTone.Red => "OnErrorContainer", CivicTone.Muted => "OnSurfaceVariant", _ => "BrandPrimaryDark"
    });
    public static Color Resource(string key) => (Color)Application.Current!.Resources[key];
    public static string IconSource(string icon, CivicTone tone) => $"{icon}_{tone.ToString().ToLowerInvariant()}.png";
    public static (string Icon, CivicTone Tone) Category(string category) => category switch
    {
        "Iluminación" => ("ui_light", CivicTone.Amber), "Accesibilidad" => ("ui_access", CivicTone.Blue),
        "Cruces" => ("ui_warning", CivicTone.Red), "Obras" => ("ui_tools", CivicTone.Amber),
        "Punto seguro" => ("ui_shield", CivicTone.Green), _ => ("ui_path", CivicTone.Blue)
    };
    public static CivicTone State(bool resolved, string severity) => resolved ? CivicTone.Green : severity switch
    { "Crítico" => CivicTone.Red, "Moderado" => CivicTone.Amber, _ => CivicTone.Blue };
    public static CivicTone Assessment(string value) => value switch
    {
        "Buena" or "Bueno" or "Apto" or "Bajo" => CivicTone.Green,
        "Deficiente" or "Deteriorado" or "Con barreras" or "Medio" => CivicTone.Amber,
        "Sin alumbrado" or "Obstruido" or "Inaccesible" or "Alto" => CivicTone.Red,
        _ => CivicTone.Muted
    };
}
