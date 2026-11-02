namespace BogoMaps.Controls;

public sealed class BottomNavigation : ContentView
{
    private readonly List<Button> buttons = [];
    private string activeTab = "explorar";
    public string ActiveTab { get => activeTab; set { activeTab = value; Render(); } }
    public BottomNavigation()
    {
        HeightRequest = 70;
        BackgroundColor = Colors.White;
        var row = new Grid { Padding = new Thickness(6), ColumnSpacing = 2,
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) } };
        var names = new[] { "Explorar", "Reportar", "Guardados", "Perfil" };
        var routes = new[] { "explorar", "reportar", "guardados", "perfil" };
        for (var i = 0; i < routes.Length; i++)
        {
            var route = routes[i];
            var button = new Button { Text = names[i], ImageSource = $"tab_{route}.png", ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Top, 3),
                FontFamily = "InterSemiBold", FontSize = 10, Padding = new Thickness(2), CornerRadius = 14, CommandParameter = route };
            button.Clicked += async (_, _) => { if (IsEnabled) { IsEnabled = false; try { await Shell.Current.GoToAsync($"//{route}"); } finally { IsEnabled = true; } } };
            buttons.Add(button); row.Add(button, i);
        }
        Content = row;
        Render();
    }
    private void Render()
    {
        foreach (var button in buttons)
        {
            var selected = (string)button.CommandParameter == activeTab;
            button.TextColor = Color.FromArgb(selected ? "#004AC6" : "#737686");
            button.BackgroundColor = selected ? Color.FromArgb("#EAEDFF") : Colors.Transparent;
            SemanticProperties.SetDescription(button, selected ? $"{button.Text}, pestaña actual" : button.Text);
        }
    }
}
