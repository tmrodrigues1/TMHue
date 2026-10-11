namespace TMHue.Core.Models;

public readonly record struct PixelBounds(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;

    public (int X, int Y) PlaceLens(int x, int y, int width, int height, int offset = 24)
    {
        var targetX = x + offset + width <= Right ? x + offset : x - offset - width;
        var targetY = y + offset + height <= Bottom ? y + offset : y - offset - height;
        return (Math.Clamp(targetX, Left, Math.Max(Left, Right - width)),
            Math.Clamp(targetY, Top, Math.Max(Top, Bottom - height)));
    }
}
