using Microsoft.Maui.Controls.Shapes;
namespace BogoMaps.Controls;

// Local vector artwork and an explicit tone keep icons legible on every platform.
public sealed class IconBadge : ContentView
{
    private readonly Border badge;
    private readonly Image image;
    private string icon = "ui_path";
    private CivicTone tone = CivicTone.Blue;
    public string Icon { get => icon; set { icon = value; Render(); } }
    public CivicTone Tone { get => tone; set { tone = value; Render(); } }
    public IconBadge()
    {
        WidthRequest = HeightRequest = 40;
        HorizontalOptions = LayoutOptions.Start;
        image = new Image { WidthRequest = 22, HeightRequest = 22, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, InputTransparent = true };
        badge = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 }, Content = image };
        Content = badge;
        InputTransparent = true;
        Render();
    }
    public void SetAppearance(string icon, CivicTone tone)
    {
        this.icon = icon;
        this.tone = tone;
        Render();
    }
    private void Render()
    {
        badge.BackgroundColor = CivicVisuals.Surface(tone);
        image.Source = CivicVisuals.IconSource(icon, tone);
    }
}
