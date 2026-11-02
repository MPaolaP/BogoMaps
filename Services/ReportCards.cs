using BogoMaps.Models;

namespace BogoMaps.Services;

public static class ReportCards
{
    public static void Fill(VerticalStackLayout container, IEnumerable<PedestrianReport> reports)
    {
        container.Children.Clear();
        foreach (var report in reports.OrderByDescending(r => r.CreatedAt))
        {
            var title = new Label { Text = report.Description, Style = (Style)Application.Current!.Resources["BogoH2"] };
            var summary = new Label { Text = $"{report.Address}\n{report.Summary}", Style = (Style)Application.Current.Resources["BogoBodySm"] };
            var button = new Button { Text = "Ver detalle", Style = (Style)Application.Current.Resources["BogoSecondaryButton"] };
            button.Clicked += async (_, _) => await Shell.Current.GoToAsync($"detalle?id={report.Id}");
            container.Children.Add(new Border { Style = (Style)Application.Current.Resources["BogoCard"], Content = new VerticalStackLayout { Spacing = 8, Children = { title, summary, button } } });
        }
        if (container.Children.Count == 0)
            container.Children.Add(new Label { Text = "Aún no hay reportes aquí. Crea uno desde Reportar.", Style = (Style)Application.Current!.Resources["BogoBodySm"] });
    }
}
