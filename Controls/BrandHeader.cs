namespace BogoMaps.Controls;

public sealed class BrandHeader : ContentView
{
    private readonly Label subtitle;
    private readonly Image mark;
    private readonly Button back;
    public bool ShowBack { get => back.IsVisible; set { back.IsVisible = value; mark.IsVisible = !value; } }
    public string SectionTitle { get => subtitle.Text; set => subtitle.Text = value; }
    public BrandHeader()
    {
        HeightRequest = 62;
        BackgroundColor = Color.FromArgb("#FAF8FF");
        var row = new Grid { Padding = new Thickness(16, 8), ColumnSpacing = 10,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        mark = new Image { Source = "bogo_mark.png", WidthRequest = 32, HeightRequest = 32, VerticalOptions = LayoutOptions.Center };
        back = new Button { ImageSource = "ui_arrow_left_muted.png", WidthRequest = 40, HeightRequest = 40, Padding = 8,
            IsVisible = false, BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#737686"), VerticalOptions = LayoutOptions.Center };
        SemanticProperties.SetDescription(back, "Volver a la pantalla anterior");
        back.Clicked += async (_, _) => { if (back.IsEnabled) { back.IsEnabled = false; try { await Shell.Current.GoToAsync(".."); } finally { back.IsEnabled = true; } } };
        subtitle = new Label { FontFamily = "JakartaRegular", FontSize = 11, TextColor = Color.FromArgb("#737686") };
        var titles = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center,
            Children = { new Label { Text = "BogoMaps", FontFamily = "JakartaBold", FontSize = 18, TextColor = Color.FromArgb("#131B2E") }, subtitle } };
        var profile = new ImageButton { Source = "tab_perfil_active.png", WidthRequest = 40, HeightRequest = 40, Padding = 10,
            CornerRadius = 14, BackgroundColor = Color.FromArgb("#EAEDFF"), VerticalOptions = LayoutOptions.Center };
        SemanticProperties.SetDescription(profile, "Ir a mi perfil");
        profile.Clicked += async (_, _) => await Shell.Current.GoToAsync("//perfil");
        row.Add(mark); row.Add(back); row.Add(titles, 1); row.Add(profile, 2);
        Content = row;
    }
}
