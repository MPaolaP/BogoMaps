using BogoMaps.Controls;
using BogoMaps.Models;
namespace BogoMaps.Services;

public static class ReportCards
{
    private static Style Style(string name) => (Style)Application.Current!.Resources[name];
    public static void Fill(VerticalStackLayout container, IEnumerable<PedestrianReport> reports)
    {
        container.Children.Clear();
        foreach (var report in reports.OrderByDescending(r => r.CreatedAt))
        {
            var row = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
            var category = CivicVisuals.Category(report.Category);
            row.Add(new IconBadge { Icon = category.Icon, Tone = category.Tone });
            row.Add(new VerticalStackLayout { Spacing = 4, Children = {
                new Label { Text = (report.IsDemo && !report.Description.StartsWith("[Prueba]") ? "[Prueba] " : "") + report.Description, Style = Style("BogoLabel"), FontSize = 13 },
                new Label { Text = report.Address, Style = Style("BogoBodySm"), FontSize = 11 },
                new Label { Text = $"{report.Category} · {report.CreatedAt.ToLocalTime():dd MMM}", Style = Style("BogoBodySm"), FontSize = 10 } } }, 1);
            var body = new VerticalStackLayout { Spacing = 10, Children = { row } };
            if (report.Photo != null && File.Exists(report.Photo))
                body.Children.Add(new Border { StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                    Content = new Image { Source = report.Photo, HeightRequest = 130, Aspect = Aspect.AspectFill } });
            var tone = CivicVisuals.State(report.Resolved, report.Severity);
            var state = new Border { BackgroundColor = CivicVisuals.Surface(tone), StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }, Padding = new Thickness(8, 4),
                HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center,
                Content = new Label { Text = report.Resolved ? "Resuelto" : $"Activo · {report.Severity}", TextColor = CivicVisuals.Ink(tone), FontFamily = "InterSemiBold", FontSize = 10 } };
            var button = new Button { Text = "Ver detalle →", FontFamily = "InterSemiBold", FontSize = 11,
                HorizontalOptions = LayoutOptions.Start, Padding = new Thickness(12, 0), HeightRequest = 36, MinimumHeightRequest = 36,
                CornerRadius = 10, TextColor = Color.FromArgb("#004AC6"), BackgroundColor = Color.FromArgb("#EAEDFF") };
            button.Clicked += async (_, _) => await Shell.Current.GoToAsync($"detalle?id={report.Id}");
            var footer = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 6 };
            footer.Add(state); footer.Add(button, 1);
            body.Children.Add(footer);
            container.Children.Add(new Border { Style = Style("BogoCard"), Content = body });
        }
        if (container.Children.Count == 0)
            container.Children.Add(new Border { Style = Style("BogoCard"), BackgroundColor = Color.FromArgb("#EAEDFF"),
                Content = new VerticalStackLayout { Spacing = 8, Children = {
                    new IconBadge { Icon = "ui_compass", Tone = CivicTone.Blue },
                    new Label { Text = "Tu próxima caminata empieza aquí", Style = Style("BogoH2") },
                    new Label { Text = "No hay reportes en esta selección. Añade uno desde Reportar o cambia el filtro.", Style = Style("BogoBodySm") } } } });
    }
}
