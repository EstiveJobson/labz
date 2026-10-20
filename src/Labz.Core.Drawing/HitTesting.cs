namespace Labz.Core.Drawing;

public static class OrthogonalRouter
{
    public static IReadOnlyList<CanvasPoint> Route(CanvasPoint start, CanvasPoint end)
    {
        if (Nearly(start.X, end.X) || Nearly(start.Y, end.Y))
        {
            return [start, end];
        }

        return [start, new CanvasPoint(end.X, start.Y), end];
    }

    public static bool IsOrthogonal(IReadOnlyList<CanvasPoint> points)
    {
        if (points.Count < 2)
        {
            return false;
        }

        for (var index = 1; index < points.Count; index++)
        {
            var previous = points[index - 1];
            var current = points[index];
            var horizontal = Nearly(previous.Y, current.Y);
            var vertical = Nearly(previous.X, current.X);
            if (horizontal == vertical)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Nearly(double left, double right) => Math.Abs(left - right) < 1e-6;
}

public enum HitKind
{
    None,
    Body,
    Pin,
    Wire,
}

public readonly record struct HitResult(
    HitKind Kind,
    string? ComponentId,
    string? Pin,
    string? NetId,
    string? LeafComponentId,
    string? LeafPin)
{
    public static HitResult Miss { get; } = new(HitKind.None, null, null, null, null, null);
}

public static class HitTester
{
    public const double PinRadius = 8;

    public const double WireTolerance = 6;

    public static HitResult Hit(CanvasScene scene, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var point = new CanvasPoint(x, y);

        ScenePin? closestPin = null;
        var closest = PinRadius;
        foreach (var symbol in scene.Symbols)
        {
            foreach (var pin in symbol.Pins)
            {
                var distance = point.DistanceTo(pin.Position);
                if (distance <= closest)
                {
                    closest = distance;
                    closestPin = pin;
                }
            }
        }

        if (closestPin is not null)
        {
            return new HitResult(HitKind.Pin, closestPin.ComponentId, closestPin.Name, null, null, null);
        }

        SceneWire? closestWire = null;
        var wireDistance = WireTolerance;
        foreach (var wire in scene.Wires)
        {
            var distance = DistanceToPolyline(point, wire.Points);
            if (distance <= wireDistance)
            {
                wireDistance = distance;
                closestWire = wire;
            }
        }

        if (closestWire is not null)
        {
            return new HitResult(
                HitKind.Wire,
                null,
                null,
                closestWire.NetId,
                closestWire.LeafComponentId,
                closestWire.LeafPin);
        }

        for (var index = scene.Symbols.Count - 1; index >= 0; index--)
        {
            var symbol = scene.Symbols[index];
            if (InsideBounds(symbol, point))
            {
                return new HitResult(HitKind.Body, symbol.Id, null, null, null, null);
            }
        }

        return HitResult.Miss;
    }

    public static double DistanceToPolyline(CanvasPoint point, IReadOnlyList<CanvasPoint> points)
    {
        if (points.Count == 0)
        {
            return double.PositiveInfinity;
        }

        if (points.Count == 1)
        {
            return point.DistanceTo(points[0]);
        }

        var best = double.PositiveInfinity;
        for (var index = 1; index < points.Count; index++)
        {
            best = Math.Min(best, DistanceToSegment(point, points[index - 1], points[index]));
        }

        return best;
    }

    private static bool InsideBounds(SceneSymbol symbol, CanvasPoint point)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var stroke in symbol.Strokes)
        {
            foreach (var vertex in stroke.Points)
            {
                minX = Math.Min(minX, vertex.X);
                minY = Math.Min(minY, vertex.Y);
                maxX = Math.Max(maxX, vertex.X);
                maxY = Math.Max(maxY, vertex.Y);
            }
        }

        if (double.IsPositiveInfinity(minX))
        {
            return point.DistanceTo(symbol.Center) <= 24;
        }

        const double pad = 6;
        return point.X >= minX - pad && point.X <= maxX + pad && point.Y >= minY - pad && point.Y <= maxY + pad;
    }

    private static double DistanceToSegment(CanvasPoint point, CanvasPoint start, CanvasPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared < 1e-12)
        {
            return point.DistanceTo(start);
        }

        var t = (((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        var projection = new CanvasPoint(start.X + (t * dx), start.Y + (t * dy));
        return point.DistanceTo(projection);
    }
}
