namespace TaskDock.Services;

public static class ScreenEdgeDetector
{
    public static bool IsAtRightEdge(int cursorX, int cursorY, int left, int top, int right, int bottom, int tolerance = 3)
    {
        if (right <= left || bottom <= top || tolerance < 1) return false;
        return cursorX >= right - tolerance && cursorX < right
            && cursorY >= top && cursorY < bottom;
    }
}
