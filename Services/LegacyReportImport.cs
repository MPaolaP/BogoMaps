namespace BogoMaps.Services;

public static class LegacyReportImport
{
    public static string SourcePath => Path.Combine(FileSystem.AppDataDirectory, "reportes.json");
    public static bool Available => File.Exists(SourcePath);
    public static async Task<LegacyImportPreview> PreviewAsync(bool examples = false)
    {
        string json;
        if (examples)
        {
            using var input = await FileSystem.OpenAppPackageFileAsync("puntos.json");
            using var reader = new StreamReader(input);
            json = await reader.ReadToEndAsync();
        }
        else json = await File.ReadAllTextAsync(SourcePath);
        return LegacyPuntoAdapter.Read(json, Path.Combine(FileSystem.AppDataDirectory, "fotos"), examples);
    }
}
