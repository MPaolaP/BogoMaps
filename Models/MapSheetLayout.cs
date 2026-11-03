namespace BogoMaps.Models;

public enum MapSheetPosition { Peek, Half, Expanded }

// Offsets are measured from the top of the map viewport in device-independent units.
public sealed record MapSheetLayout(double Height, double Expanded, double Half, double Peek)
{
    public static MapSheetLayout ForHeight(double height)
    {
        var h = double.IsFinite(height) ? Math.Max(0, height) : 0;
        var expanded = h >= 320 ? 8 : 0;
        var maxVisible = h - expanded;
        var peekVisible = Math.Min(112, maxVisible);
        var halfVisible = Math.Clamp(h * .52, peekVisible, maxVisible);
        return new(h, expanded, h - halfVisible, h - peekVisible);
    }
    public double Top(MapSheetPosition position) => position switch
    { MapSheetPosition.Expanded => Expanded, MapSheetPosition.Half => Half, _ => Peek };
    public double Clamp(double top) => double.IsFinite(top) ? Math.Clamp(top, Expanded, Peek) : Peek;
    public double VisibleHeightAt(double top) => Height - Clamp(top);
    public MapSheetPosition Snap(double top, double drag, MapSheetPosition start)
    {
        var target = Enum.GetValues<MapSheetPosition>().MinBy(p => Math.Abs(Top(p) - Clamp(top)));
        // A short, deliberate swipe advances one position even before reaching the midpoint.
        if (drag <= -28 && target <= start) target = (MapSheetPosition)Math.Min(2, (int)start + 1);
        if (drag >= 28 && target >= start) target = (MapSheetPosition)Math.Max(0, (int)start - 1);
        return target;
    }
}
