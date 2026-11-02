namespace BogoMaps.Controls;

// A mobile composition remains readable when hosted in a resizable Windows window.
public class StitchPage : ContentPage
{
    public StitchPage()
    {
        BackgroundColor = Color.FromArgb("#F2F3FF");
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
    }
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (Content is not View frame || width <= 0) return;
        var target = Math.Min(width, 440);
        if (Math.Abs(frame.WidthRequest - target) > .5) frame.WidthRequest = target;
        frame.HorizontalOptions = LayoutOptions.Center;
    }
}
