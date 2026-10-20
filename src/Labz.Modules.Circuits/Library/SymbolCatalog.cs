using Labz.Core.Drawing;

namespace Labz.Modules.Circuits;

public static class SymbolCatalog
{
    public static SymbolGeometry For(string type) => type switch
    {
        "resistor" => Resistor(),
        "capacitor" => Capacitor(),
        "inductor" => Inductor(),
        "diode" => Diode(arrows: false),
        "led" => Diode(arrows: true),
        "dc_voltage_source" => Source(rays: false),
        "solar_panel" => Source(rays: true),
        "battery" => Battery(),
        "switch" => Switch(),
        "ground" => Ground(),
        _ => throw new ArgumentException(Strings.UnknownType(type), nameof(type)),
    };

    private static SymbolGeometry Resistor() => TwoPin(
        "1",
        "2",
        Stroke((-40, 0), (-24, 0), (-16, 12), (-8, -12), (0, 12), (8, -12), (16, 12), (24, 0), (40, 0)));

    private static SymbolGeometry Capacitor() => TwoPin(
        "1",
        "2",
        Stroke((-40, 0), (-8, 0)),
        Stroke((-8, -16), (-8, 16)),
        Stroke((8, -16), (8, 16)),
        Stroke((8, 0), (40, 0)));

    private static SymbolGeometry Inductor()
    {
        var points = new List<CanvasPoint> { new(-40, 0), new(-24, 0) };
        for (var coil = 0; coil < 4; coil++)
        {
            var left = -24 + (coil * 12);
            points.Add(new CanvasPoint(left + 3, -10));
            points.Add(new CanvasPoint(left + 9, -10));
            points.Add(new CanvasPoint(left + 12, 0));
        }

        points.Add(new CanvasPoint(40, 0));
        return TwoPin("1", "2", new SymbolStroke(points));
    }

    private static SymbolGeometry Diode(bool arrows)
    {
        var strokes = new List<SymbolStroke>
        {
            Stroke((-40, 0), (-12, 0)),
            Stroke((-12, -14), (14, 0), (-12, 14), (-12, -14)),
            Stroke((14, -16), (14, 16)),
            Stroke((14, 0), (40, 0)),
        };
        if (arrows)
        {
            strokes.Add(Stroke((6, -18), (18, -28)));
            strokes.Add(Stroke((14, -28), (18, -28), (18, -24)));
            strokes.Add(Stroke((14, -14), (26, -24)));
            strokes.Add(Stroke((22, -24), (26, -24), (26, -20)));
        }

        return new SymbolGeometry(strokes, [Pin("A", -40, 0), Pin("K", 40, 0)]);
    }

    private static SymbolGeometry Source(bool rays)
    {
        var strokes = new List<SymbolStroke>
        {
            Stroke((-40, 0), (-18, 0)),
            Circle(18, 16),
            Stroke((18, 0), (40, 0)),
            Stroke((-12, -4), (-4, -4)),
            Stroke((4, -6), (12, -6)),
            Stroke((8, -10), (8, -2)),
        };
        if (rays)
        {
            strokes.Add(Stroke((0, -28), (0, -22)));
            strokes.Add(Stroke((0, 22), (0, 28)));
            strokes.Add(Stroke((-22, 0), (-28, 0)));
            strokes.Add(Stroke((22, 0), (28, 0)));
        }

        return new SymbolGeometry(strokes, [Pin("+", 40, 0), Pin("-", -40, 0)]);
    }

    private static SymbolGeometry Battery() => new(
        [
            Stroke((-40, 0), (-10, 0)),
            Stroke((-10, -8), (-10, 8)),
            Stroke((8, -18), (8, 18)),
            Stroke((8, 0), (40, 0)),
        ],
        [Pin("-", -40, 0), Pin("+", 40, 0)]);

    private static SymbolGeometry Switch() => new(
        [
            Stroke((-40, 0), (-14, 0)),
            Stroke((-14, 0), (18, -16)),
            Stroke((22, -3), (22, 3)),
            Stroke((22, 0), (40, 0)),
        ],
        [Pin("1", -40, 0), Pin("2", 40, 0)]);

    private static SymbolGeometry Ground() => new(
        [
            Stroke((0, -36), (0, -8)),
            Stroke((-18, -8), (18, -8)),
            Stroke((-12, 0), (12, 0)),
            Stroke((-6, 8), (6, 8)),
        ],
        [Pin("g", 0, -36)]);

    private static SymbolGeometry TwoPin(string left, string right, params SymbolStroke[] strokes) =>
        new(strokes, [Pin(left, -40, 0), Pin(right, 40, 0)]);

    private static SymbolPin Pin(string name, double x, double y) => new(name, new CanvasPoint(x, y));

    private static SymbolStroke Stroke(params (double X, double Y)[] points) =>
        new(points.Select(point => new CanvasPoint(point.X, point.Y)).ToArray());

    private static SymbolStroke Circle(double radius, int segments)
    {
        var points = new CanvasPoint[segments + 1];
        for (var index = 0; index <= segments; index++)
        {
            var angle = index * (Math.PI * 2 / segments);
            points[index] = new CanvasPoint(radius * Math.Cos(angle), radius * Math.Sin(angle));
        }

        return new SymbolStroke(points);
    }
}
