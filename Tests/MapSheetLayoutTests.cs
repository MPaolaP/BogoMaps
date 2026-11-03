using BogoMaps.Models;

internal static class MapSheetLayoutTests
{
    public static void Run()
    {
        foreach (var height in new[] { 0d, 60, 112, 240, 480, 720, 1200, double.NaN, double.PositiveInfinity })
        {
            var layout = MapSheetLayout.ForHeight(height);
            if (!(layout.Expanded >= 0 && layout.Expanded <= layout.Half && layout.Half <= layout.Peek && layout.Peek <= layout.Height))
                throw new Exception("Sheet snap positions leave the viewport or overlap in the wrong order");
            if (layout.Clamp(-500) != layout.Expanded || layout.Clamp(9000) != layout.Peek || !double.IsFinite(layout.Clamp(double.NaN)))
                throw new Exception("Sheet drag is not bounded");
            foreach (var top in new[] { layout.Expanded, layout.Half, layout.Peek, (layout.Expanded + layout.Half) / 2 })
            {
                if (Math.Abs(top + layout.VisibleHeightAt(top) - layout.Height) > .001)
                    throw new Exception("The sheet scroll viewport extends below the screen during a drag or at a snap position");
            }
        }
        var phone = MapSheetLayout.ForHeight(720);
        if (phone.Snap(phone.Peek - 35, -35, MapSheetPosition.Peek) != MapSheetPosition.Half)
            throw new Exception("A deliberate short upward swipe should reveal the panel");
        if (phone.Snap(phone.Expanded, -600, MapSheetPosition.Peek) != MapSheetPosition.Expanded)
            throw new Exception("A full upward drag should reveal all information");
        if (phone.Snap(phone.Half + 35, 35, MapSheetPosition.Half) != MapSheetPosition.Peek)
            throw new Exception("A downward swipe should reveal more of the map");
        if (phone.Snap(phone.Half - 35, -35, MapSheetPosition.Half) != MapSheetPosition.Expanded)
            throw new Exception("Half-height panel should expand on an upward swipe");
        if (phone.Snap(phone.Peek, 2, MapSheetPosition.Peek) != MapSheetPosition.Peek)
            throw new Exception("A tap or tiny movement should not expand the panel");
        var resized = MapSheetLayout.ForHeight(360);
        if (resized.Top(MapSheetPosition.Half) >= phone.Top(MapSheetPosition.Half))
            throw new Exception("Resizing or the software keyboard must recalculate the panel positions");
        Console.WriteLine("PASS: map sheet: bounded positions, short and full drags, direction, tiny movement, invalid sizes and resized viewports.");
    }
}
