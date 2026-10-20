using Labz.Core.Drawing;

namespace Labz.Modules.Circuits;

public static class SceneBuilder
{
    public static CanvasScene Build(SchematicState state, string? selectedId, IReadOnlySet<string>? highlighted)
    {
        ArgumentNullException.ThrowIfNull(state);
        highlighted ??= new HashSet<string>(StringComparer.Ordinal);
        var symbols = new List<SceneSymbol>();
        var labels = new List<SceneLabel>();
        var pins = new Dictionary<string, CanvasPoint>(StringComparer.Ordinal);

        foreach (var component in state.Components)
        {
            if (!ComponentLibrary.TryGet(component.Type, out var definition) || definition is null)
            {
                continue;
            }

            var strokes = definition.Symbol.Strokes
                .Select(stroke => new SymbolStroke(stroke.Points
                    .Select(point => SymbolTransform.ToWorld(point, component.X, component.Y, component.Rotation))
                    .ToArray()))
                .ToArray();
            var scenePins = definition.Symbol.Pins.Select(pin =>
            {
                var position = SymbolTransform.ToWorld(pin.Local, component.X, component.Y, component.Rotation);
                pins[component.Id + "." + pin.Name] = position;
                return new ScenePin
                {
                    ComponentId = component.Id,
                    Name = pin.Name,
                    Position = position,
                };
            }).ToArray();

            symbols.Add(new SceneSymbol
            {
                Id = component.Id,
                Center = new CanvasPoint(component.X, component.Y),
                Strokes = strokes,
                Pins = scenePins,
                Selected = component.Id == selectedId,
                Highlighted = highlighted.Contains(component.Id),
            });
            labels.Add(new SceneLabel
            {
                Position = new CanvasPoint(component.X, component.Y - 52),
                Text = component.Id,
            });
            foreach (var pin in scenePins)
            {
                labels.Add(new SceneLabel
                {
                    Position = new CanvasPoint(pin.Position.X + 8, pin.Position.Y - 14),
                    Text = pin.Name,
                });
            }
        }

        return new CanvasScene(symbols, Route(state, pins), labels);
    }

    private static List<SceneWire> Route(SchematicState state, IReadOnlyDictionary<string, CanvasPoint> pins)
    {
        var wires = new List<SceneWire>();
        foreach (var net in state.Nets)
        {
            var located = new List<(PinRef Pin, CanvasPoint Point)>();
            foreach (var connection in net.Connections)
            {
                if (pins.TryGetValue(connection.ComponentId + "." + connection.Pin, out var point))
                {
                    located.Add((connection, point));
                }
            }

            if (located.Count < 2)
            {
                continue;
            }

            if (located.Count == 2)
            {
                wires.Add(new SceneWire
                {
                    NetId = net.Id,
                    Points = OrthogonalRouter.Route(located[0].Point, located[1].Point),
                });
                continue;
            }

            var busY = Median(located.Select(item => item.Point.Y));
            var minX = located.Min(item => item.Point.X);
            var maxX = located.Max(item => item.Point.X);
            wires.Add(new SceneWire
            {
                NetId = net.Id,
                Points = [new CanvasPoint(minX, busY), new CanvasPoint(maxX, busY)],
            });
            foreach (var (pin, point) in located)
            {
                if (Math.Abs(point.Y - busY) < 1e-6)
                {
                    continue;
                }

                wires.Add(new SceneWire
                {
                    NetId = net.Id,
                    Points = OrthogonalRouter.Route(point, new CanvasPoint(point.X, busY)),
                    LeafComponentId = pin.ComponentId,
                    LeafPin = pin.Pin,
                });
            }
        }

        return wires;
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        var middle = ordered.Length / 2;
        if (ordered.Length % 2 == 1)
        {
            return ordered[middle];
        }

        return (ordered[middle - 1] + ordered[middle]) / 2;
    }
}
