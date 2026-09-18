namespace TaskDock.Services;

public static class ScreenEdgeDetector
{
    public static bool IsAtEdge(int cursorX, int cursorY, int left, int top, int right, int bottom, string? edge, int tolerance = 3)
    {
        if (right <= left || bottom <= top || tolerance < 1) return false;
        var insideHorizontally = cursorX >= left && cursorX < right;
        var insideVertically = cursorY >= top && cursorY < bottom;
        return edge?.ToLowerInvariant() switch
        {
            "left" => cursorX >= left && cursorX < left + tolerance && insideVertically,
            "top" => cursorY >= top && cursorY < top + tolerance && insideHorizontally,
            "bottom" => cursorY >= bottom - tolerance && cursorY < bottom && insideHorizontally,
            _ => cursorX >= right - tolerance && cursorX < right && insideVertically
        };
    }

    public static bool IsAtRightEdge(int cursorX, int cursorY, int left, int top, int right, int bottom, int tolerance = 3)
        => IsAtEdge(cursorX, cursorY, left, top, right, bottom, "Right", tolerance);
}
