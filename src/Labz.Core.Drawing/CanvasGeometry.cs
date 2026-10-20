namespace Labz.Core.Drawing;

public readonly record struct CanvasPoint(double X, double Y)
{
    public double DistanceTo(CanvasPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

public static class GridSnap
{
    public const double Size = 20;

    public static double Snap(double value) =>
        Math.Round(value / Size, MidpointRounding.AwayFromZero) * Size;

    public static CanvasPoint Snap(CanvasPoint point) => new(Snap(point.X), Snap(point.Y));
}

/// <summary>Clockwise rotation in a y-down canvas, in 90° steps.</summary>
public static class SymbolTransform
{
    public static int Normalize(double rotation)
    {
        var quadrants = (int)Math.Round(rotation / 90d, MidpointRounding.AwayFromZero);
        var wrapped = quadrants % 4;
        if (wrapped < 0)
        {
            wrapped += 4;
        }

        return wrapped * 90;
    }

    public static CanvasPoint Rotate(CanvasPoint local, int rotationClockwise)
    {
        return Normalize(rotationClockwise) switch
        {
            0 => local,
            90 => new CanvasPoint(-local.Y, local.X),
            180 => new CanvasPoint(-local.X, -local.Y),
            270 => new CanvasPoint(local.Y, -local.X),
            _ => local,
        };
    }

    public static CanvasPoint ToWorld(CanvasPoint local, double originX, double originY, int rotationClockwise)
    {
        var rotated = Rotate(local, rotationClockwise);
        return new CanvasPoint(originX + rotated.X, originY + rotated.Y);
    }
}
