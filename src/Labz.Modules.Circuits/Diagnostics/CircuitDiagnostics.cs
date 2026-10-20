namespace Labz.Modules.Circuits;

public enum CircuitProblemKind
{
    MissingGround,
    FloatingNode,
    VoltageSourceShort,
    UnconnectedPin,
}

public sealed record CircuitProblem(CircuitProblemKind Kind, string Message, IReadOnlyList<string> ComponentIds);

public static class CircuitDiagnostics
{
    public static IReadOnlyList<CircuitProblem> Check(CircuitModel circuit)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        var problems = new List<CircuitProblem>();
        var unconnected = UnconnectedPins(circuit);
        foreach (var (componentId, pin) in unconnected)
        {
            problems.Add(new CircuitProblem(
                CircuitProblemKind.UnconnectedPin,
                Strings.UnconnectedPin(componentId, pin),
                [componentId]));
        }

        var hasGround = circuit.Components.OfType<GroundComponent>().Any()
            || circuit.Nets.Any(net => net.Id == "0");
        if (!hasGround)
        {
            var ids = circuit.Components.Select(component => component.Id).Order(StringComparer.Ordinal).ToArray();
            problems.Add(new CircuitProblem(
                CircuitProblemKind.MissingGround,
                Strings.MissingGround(Join(ids)),
                ids));
        }

        AddTerminalShorts(circuit, problems);

        if (hasGround)
        {
            try
            {
                var topology = Topology.Build(circuit);
                var leds = circuit.Components.OfType<LedComponent>().Select(led => led.Id).ToHashSet(StringComparer.Ordinal);
                if (topology.TryGetVoltageLoop(leds, out var loop))
                {
                    AddShort(problems, loop, Strings.VoltageSourceLoop(Join(loop)));
                }

                if (topology.TryGetFloating(leds, out var nodes, out var components))
                {
                    var privatePins = unconnected.Select(pin => pin.ComponentId + "." + pin.Pin).ToHashSet(StringComparer.Ordinal);
                    var namedNodes = nodes.Where(node => !privatePins.Contains(node)).ToArray();
                    if (namedNodes.Length > 0)
                    {
                        problems.Add(new CircuitProblem(
                            CircuitProblemKind.FloatingNode,
                            Strings.FloatingNode(Join(namedNodes), Join(components)),
                            components.ToArray()));
                    }
                }
            }
            catch (MissingGroundException exception)
            {
                if (problems.All(problem => problem.Kind != CircuitProblemKind.MissingGround))
                {
                    problems.Add(new CircuitProblem(
                        CircuitProblemKind.MissingGround,
                        exception.Message,
                        exception.ComponentIds));
                }
            }
        }

        return problems;
    }

    private static List<(string ComponentId, string Pin)> UnconnectedPins(CircuitModel circuit)
    {
        var connected = circuit.Nets
            .SelectMany(net => net.Connections)
            .Select(connection => connection.ComponentId + "." + connection.Pin)
            .ToHashSet(StringComparer.Ordinal);
        var pins = new List<(string, string)>();
        foreach (var component in circuit.Components)
        {
            if (component is GroundComponent)
            {
                continue;
            }

            foreach (var pin in component.Pins)
            {
                if (!connected.Contains(component.Id + "." + pin))
                {
                    pins.Add((component.Id, pin));
                }
            }
        }

        return pins;
    }

    private static void AddTerminalShorts(CircuitModel circuit, List<CircuitProblem> problems)
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);

        string Find(string pin)
        {
            if (!parent.TryGetValue(pin, out var root))
            {
                parent[pin] = pin;
                return pin;
            }

            if (root != pin)
            {
                root = Find(root);
                parent[pin] = root;
            }

            return root;
        }

        void Union(string left, string right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (leftRoot != rightRoot)
            {
                parent[rightRoot] = leftRoot;
            }
        }

        foreach (var net in circuit.Nets)
        {
            if (net.Connections.Count < 2)
            {
                continue;
            }

            var first = net.Connections[0].ComponentId + "." + net.Connections[0].Pin;
            foreach (var connection in net.Connections.Skip(1))
            {
                Union(first, connection.ComponentId + "." + connection.Pin);
            }
        }

        var closed = circuit.Components.OfType<SwitchComponent>().Where(sw => sw.State == SwitchState.Closed).ToArray();
        foreach (var sw in closed)
        {
            Union(sw.Id + ".1", sw.Id + ".2");
        }

        foreach (var source in circuit.Components.Where(component => component.Pins.Contains("+") && component.Pins.Contains("-")))
        {
            var plus = source.Id + ".+";
            var minus = source.Id + ".-";
            if (Find(plus) != Find(minus))
            {
                continue;
            }

            var ids = new List<string> { source.Id };
            foreach (var sw in closed)
            {
                if (Find(sw.Id + ".1") == Find(plus))
                {
                    ids.Add(sw.Id);
                }
            }

            AddShort(problems, ids, Strings.VoltageShort(Join(ids)));
        }
    }

    private static void AddShort(List<CircuitProblem> problems, IReadOnlyList<string> componentIds, string message)
    {
        var ids = componentIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var existing = problems.FirstOrDefault(problem => problem.Kind == CircuitProblemKind.VoltageSourceShort
            && problem.ComponentIds.Order(StringComparer.Ordinal).SequenceEqual(ids));
        if (existing is not null)
        {
            return;
        }

        problems.Add(new CircuitProblem(CircuitProblemKind.VoltageSourceShort, message, ids));
    }

    private static string Join(IEnumerable<string> ids)
    {
        var ordered = ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return ordered.Length == 0 ? "—" : string.Join(", ", ordered);
    }
}
