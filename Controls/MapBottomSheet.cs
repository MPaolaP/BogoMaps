using BogoMaps.Models;
using Microsoft.Maui.Controls.Shapes;

namespace BogoMaps.Controls;

public sealed class MapBottomSheet : Grid
{
    private readonly Border sheet;
    private readonly ScrollView scroller;
    private readonly Label title;
    private readonly Label subtitle;
    private readonly Button expand;
    private MapSheetLayout layout = MapSheetLayout.ForHeight(0);
    private MapSheetPosition position = MapSheetPosition.Peek;
    private MapSheetPosition dragStartPosition;
    private double dragStartTop;
    private double lastDrag;
    private bool dragging;
    private int motionVersion;
#if WINDOWS
    private double mouseStart;
    private bool mouseDragging;
#endif
    public event EventHandler? PositionChanged;
    public event EventHandler? CoverageChanged;
    public MapSheetPosition Position => position;
    public double VisibleHeight => layout.Height <= 0 ? 0 : Math.Max(0, layout.Height - sheet.TranslationY);
    public double MapVisibleHeight => Math.Max(0, sheet.TranslationY);
    public string Title { get => title.Text; set => title.Text = value; }
    public string Subtitle { get => subtitle.Text; set => subtitle.Text = value; }
    public View? SheetContent { get => scroller.Content; set => scroller.Content = value; }

    public MapBottomSheet()
    {
        // The empty overlay must pass map gestures through; only the actual sheet intercepts input.
        InputTransparent = true;
        CascadeInputTransparent = false;
        IsClippedToBounds = true;
        title = new Label { Text = "Explora Bogotá a pie", FontFamily = "JakartaBold", FontSize = 16,
            TextColor = CivicVisuals.Resource("OnSurface"), MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation };
        subtitle = new Label { Text = "Desliza hacia arriba para ver reportes", FontFamily = "JakartaRegular", FontSize = 11,
            TextColor = CivicVisuals.Resource("OnSurfaceVariant"), MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
        expand = new Button { Text = "Más ↑", FontFamily = "InterSemiBold", FontSize = 11, WidthRequest = 80,
            HeightRequest = 40, MinimumHeightRequest = 40, Padding = 6, CornerRadius = 12,
            BackgroundColor = CivicVisuals.Resource("PrimaryFixed"), TextColor = CivicVisuals.Resource("BrandPrimaryDark") };
        expand.Clicked += async (_, _) => await MoveToAsync(position switch {
            MapSheetPosition.Peek => MapSheetPosition.Half, MapSheetPosition.Half => MapSheetPosition.Expanded, _ => MapSheetPosition.Half });
        var map = new Button { Text = "Ver mapa", FontSize = 11, FontFamily = "InterSemiBold", HeightRequest = 32,
            MinimumHeightRequest = 32, Padding = 0, BackgroundColor = Colors.Transparent, TextColor = CivicVisuals.Resource("BrandPrimaryDark") };
        map.Clicked += async (_, _) => await MoveToAsync(MapSheetPosition.Peek);
        var header = new Grid { HeightRequest = 112, RowDefinitions = { new(new GridLength(20)), new(GridLength.Star) }, BackgroundColor = CivicVisuals.Resource("Surface") };
        var grip = new Border { WidthRequest = 40, HeightRequest = 4, BackgroundColor = CivicVisuals.Resource("OutlineVariant"),
            StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 2 }, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        var heading = new Grid { Padding = new Thickness(16, 0, 16, 8), ColumnSpacing = 12,
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        heading.Add(new VerticalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center, Children = { title, subtitle } });
        heading.Add(new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center, Children = { expand, map } }, 1);
        header.Add(grip); header.Add(heading, 0, 1);
        SemanticProperties.SetHint(header, "Arrastra esta cabecera hacia arriba o abajo. También puedes usar Más, Menos y Ver mapa.");
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += async (_, args) =>
        {
#if WINDOWS
            if (mouseDragging) return;
#endif
            if (args.StatusType == GestureStatus.Started) BeginDrag();
            else if (args.StatusType == GestureStatus.Running) Drag(args.TotalY);
            else if (args.StatusType == GestureStatus.Completed) await EndDragAsync(false);
            else if (args.StatusType == GestureStatus.Canceled) await EndDragAsync(true);
        };
        header.GestureRecognizers.Add(pan);
#if WINDOWS
        // WinUI manipulations are primarily touch gestures; support ordinary left-button dragging too.
        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += (_, args) =>
        {
            var routed = args.PlatformArgs?.PointerRoutedEventArgs;
            if (routed == null || routed.Pointer.PointerDeviceType.ToString() != "Mouse" || !routed.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;
            // Let actual buttons receive their own mouse release/click instead of stealing capture.
            for (var source = routed.OriginalSource as Microsoft.UI.Xaml.DependencyObject; source != null; source = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(source))
            {
                if (source is Microsoft.UI.Xaml.Controls.Primitives.ButtonBase) return;
                if (ReferenceEquals(source, header.Handler?.PlatformView)) break;
            }
            var point = args.GetPosition(this);
            if (point == null) return;
            mouseDragging = true; mouseStart = point.Value.Y; BeginDrag();
            if (header.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement native) native.CapturePointer(routed.Pointer);
        };
        pointer.PointerMoved += async (_, args) =>
        {
            if (!mouseDragging) return;
            var routed = args.PlatformArgs?.PointerRoutedEventArgs;
            if (routed != null && !routed.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            { mouseDragging = false; await EndDragAsync(false); return; }
            if (args.GetPosition(this) is { } point) Drag(point.Y - mouseStart);
        };
        pointer.PointerReleased += async (_, args) =>
        {
            if (!mouseDragging) return;
            mouseDragging = false;
            if (header.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement native) native.ReleasePointerCaptures();
            await EndDragAsync(false);
        };
        header.GestureRecognizers.Add(pointer);
#endif
        scroller = new ScrollView { Orientation = ScrollOrientation.Neither };
        var contents = new Grid { RowDefinitions = { new(new GridLength(112)), new(GridLength.Star) } };
        contents.Add(header); contents.Add(scroller, 0, 1);
        sheet = new Border { BackgroundColor = CivicVisuals.Resource("Surface"), StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24, 24, 0, 0) },
            VerticalOptions = LayoutOptions.Start, Content = contents,
            Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Opacity = .14f, Radius = 16, Offset = new Point(0, -4) } };
        sheet.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(TranslationY)) CoverageChanged?.Invoke(this, EventArgs.Empty); };
        Children.Add(sheet);
        UpdateHeader();
    }
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (Math.Abs(layout.Height - height) < .5) return;
        motionVersion++;
        sheet.CancelAnimations();
        dragging = false;
#if WINDOWS
        mouseDragging = false;
#endif
        layout = MapSheetLayout.ForHeight(height);
        sheet.IsVisible = layout.Height > 0;
        sheet.HeightRequest = Math.Max(0, layout.Height - layout.Expanded);
        sheet.TranslationY = layout.Top(position);
        scroller.Orientation = position == MapSheetPosition.Expanded ? ScrollOrientation.Vertical : ScrollOrientation.Neither;
        CoverageChanged?.Invoke(this, EventArgs.Empty);
    }
    public async Task MoveToAsync(MapSheetPosition target, bool animate = true)
    {
        var version = ++motionVersion;
        dragging = false;
        sheet.CancelAnimations();
        position = target;
        UpdateHeader();
        if (target != MapSheetPosition.Expanded && scroller.Handler != null && scroller.ScrollY > .5)
        {
            scroller.Orientation = ScrollOrientation.Vertical;
            await scroller.ScrollToAsync(0, 0, false);
        }
        if (version != motionVersion) return;
        scroller.Orientation = target == MapSheetPosition.Expanded ? ScrollOrientation.Vertical : ScrollOrientation.Neither;
        if (animate && layout.Height > 0) await sheet.TranslateTo(0, layout.Top(target), 210, Easing.CubicOut);
        else sheet.TranslationY = layout.Top(target);
        if (version != motionVersion) return;
        CoverageChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }
    public void CancelMotion()
    {
        motionVersion++;
        sheet.CancelAnimations();
        dragging = false;
#if WINDOWS
        mouseDragging = false;
#endif
        sheet.TranslationY = layout.Top(position);
        scroller.Orientation = position == MapSheetPosition.Expanded ? ScrollOrientation.Vertical : ScrollOrientation.Neither;
    }
    private void BeginDrag()
    {
        motionVersion++;
        sheet.CancelAnimations();
        dragging = true;
        dragStartTop = layout.Clamp(sheet.TranslationY);
        dragStartPosition = position;
        lastDrag = 0;
        scroller.Orientation = ScrollOrientation.Neither;
    }
    private void Drag(double total)
    {
        if (!dragging || !double.IsFinite(total)) return;
        lastDrag = total;
        sheet.TranslationY = layout.Clamp(dragStartTop + total);
    }
    private async Task EndDragAsync(bool cancelled)
    {
        if (!dragging) return;
        var target = cancelled ? dragStartPosition : layout.Snap(sheet.TranslationY, lastDrag, dragStartPosition);
        await MoveToAsync(target);
    }
    private void UpdateHeader()
    {
        expand.Text = position == MapSheetPosition.Expanded ? "Menos ↓" : "Más ↑";
        SemanticProperties.SetDescription(expand, position switch {
            MapSheetPosition.Peek => "Ampliar el panel a media pantalla", MapSheetPosition.Half => "Ampliar el panel a pantalla completa", _ => "Reducir el panel a media pantalla" });
    }
}
