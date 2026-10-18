namespace Labz.Modules.Circuits;

/// <summary>
/// Nodes, pin map, and the DC connectivity used to reject a missing ground, a floating node, or a voltage-source loop.
/// </summary>
internal sealed class Topology
{
    private readonly List<string> _nodeIds;
    private readonly Dictionary<string, int> _pins = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _internal = new(StringComparer.Ordinal);
    private readonly List<List<string>> _componentsAtNode = [];

    private Topology(CircuitModel circuit, List<string> nodeIds, Dictionary<string, int> userNetNodes)
    {
        Circuit = circuit;
        _nodeIds = nodeIds;
        UserNetNodes = userNetNodes;
    }

    public CircuitModel Circuit { get; }

    public IReadOnlyList<string> NodeIds => _nodeIds;

    public IReadOnlyDictionary<string, int> UserNetNodes { get; }

    public int Pin(string componentId, string pin) => _pins[componentId + "." + pin];

    public bool TryInternal(string componentId, string role, out int node) =>
        _internal.TryGetValue(componentId + ":" + role, out node);

    public static Topology Build(CircuitModel circuit)
    {
        CircuitValidator.ValidateStructure(circuit);
        var pinNet = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var net in circuit.Nets)
        {
            foreach (var connection in net.Connections)
            {
                pinNet[connection.ComponentId + "." + connection.Pin] = net.Id;
            }
        }

        foreach (var component in circuit.Components)
        {
            foreach (var pin in component.Pins)
            {
                var key = component.Id + "." + pin;
                if (!pinNet.ContainsKey(key))
                {
                    pinNet[key] = key;
                }
            }
        }

        var groundNets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ground in circuit.Components.OfType<GroundComponent>())
        {
            groundNets.Add(pinNet[ground.Id + ".g"]);
        }

        if (pinNet.ContainsValue("0") || circuit.Nets.Any(net => net.Id == "0"))
        {
            groundNets.Add("0");
        }

        if (groundNets.Count == 0)
        {
            throw new MissingGroundException(
                Strings.MissingGround(Join(circuit.Components.Select(component => component.Id))),
                circuit.Components.Select(component => component.Id).ToArray());
        }

        var nodeIds = new List<string> { "0" };
        var nodeIndex = new Dictionary<string, int>(StringComparer.Ordinal) { ["0"] = 0 };
        void AddNode(string id)
        {
            if (id == "0" || nodeIndex.ContainsKey(id))
            {
                return;
            }

            nodeIndex[id] = nodeIds.Count;
            nodeIds.Add(id);
        }

        foreach (var netId in pinNet.Values)
        {
            if (!groundNets.Contains(netId))
            {
                AddNode(netId);
            }
        }

        var topology = new Topology(
            circuit,
            nodeIds,
            circuit.Nets.ToDictionary(
                net => net.Id,
                net => groundNets.Contains(net.Id) ? 0 : nodeIndex[net.Id],
                StringComparer.Ordinal));

        foreach (var _ in nodeIds)
        {
            topology._componentsAtNode.Add([]);
        }

        foreach (var (key, netId) in pinNet)
        {
            var index = groundNets.Contains(netId) ? 0 : nodeIndex[netId];
            topology._pins[key] = index;
            var componentId = key[..key.IndexOf('.')];
            topology._componentsAtNode[index].Add(componentId);
        }

        foreach (var component in circuit.Components)
        {
            switch (component)
            {
                case BatteryComponent battery when battery.InternalResistance > 0:
                    topology.AddInternal(battery.Id, "series", battery.Id + ".int");
                    break;
                case SolarPanelComponent panel when panel.SeriesResistance > 0:
                    topology.AddInternal(panel.Id, "series", panel.Id + ".int");
                    break;
                case DiodeComponent diode when diode.Model.SeriesResistance > 0:
                    topology.AddInternal(diode.Id, "junction", diode.Id + ".j");
                    break;
            }
        }

        return topology;
    }

    public bool TryGetVoltageLoop(IReadOnlySet<string> ledsOn, out IReadOnlyList<string> componentIds)
    {
        var parent = new Dictionary<int, int>();
        var members = new Dictionary<int, List<string>>();

        int Find(int node)
        {
            if (!parent.ContainsKey(node))
            {
                parent[node] = node;
                members[node] = [];
            }

            if (parent[node] != node)
            {
                parent[node] = Find(parent[node]);
            }

            return parent[node];
        }

        foreach (var (left, right, componentId) in IdealEdges(ledsOn))
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (leftRoot == rightRoot)
            {
                var involved = members[leftRoot].Append(componentId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                componentIds = involved;
                return true;
            }

            parent[rightRoot] = leftRoot;
            members[leftRoot].Add(componentId);
            members[leftRoot].AddRange(members[rightRoot]);
        }

        componentIds = [];
        return false;
    }

    public bool TryGetFloating(
        IReadOnlySet<string> ledsConducting,
        out IReadOnlyList<string> nodeIds,
        out IReadOnlyList<string> componentIds)
    {
        var adjacent = new List<(int To, string ComponentId)>[NodeIds.Count];
        for (var i = 0; i < adjacent.Length; i++)
        {
            adjacent[i] = [];
        }

        foreach (var (left, right, componentId) in ConductiveEdges(ledsConducting))
        {
            adjacent[left].Add((right, componentId));
            adjacent[right].Add((left, componentId));
        }

        var seen = new bool[NodeIds.Count];
        var queue = new Queue<int>();
        queue.Enqueue(0);
        seen[0] = true;
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var (to, _) in adjacent[node])
            {
                if (!seen[to])
                {
                    seen[to] = true;
                    queue.Enqueue(to);
                }
            }
        }

        var floating = new List<string>();
        var components = new HashSet<string>(StringComparer.Ordinal);
        for (var node = 1; node < NodeIds.Count; node++)
        {
            if (seen[node])
            {
                continue;
            }

            floating.Add(NodeIds[node]);
            foreach (var componentId in _componentsAtNode[node])
            {
                components.Add(componentId);
            }
        }

        nodeIds = floating;
        componentIds = components.Order(StringComparer.Ordinal).ToArray();
        return floating.Count > 0;
    }

    private void AddInternal(string componentId, string role, string nodeId)
    {
        var index = _nodeIds.Count;
        _nodeIds.Add(nodeId);
        _componentsAtNode.Add([componentId]);
        _internal[componentId + ":" + role] = index;
    }

    private IEnumerable<(int Left, int Right, string ComponentId)> ConductiveEdges(IReadOnlySet<string> ledsConducting)
    {
        foreach (var component in Circuit.Components)
        {
            switch (component)
            {
                case ResistorComponent:
                    yield return (Pin(component.Id, "1"), Pin(component.Id, "2"), component.Id);
                    break;
                case InductorComponent:
                    yield return (Pin(component.Id, "1"), Pin(component.Id, "2"), component.Id);
                    break;
                case DiodeComponent diode:
                    foreach (var edge in DiodeEdges(diode))
                    {
                        yield return edge;
                    }

                    break;
                case LedComponent when ledsConducting.Contains(component.Id):
                    yield return (Pin(component.Id, "A"), Pin(component.Id, "K"), component.Id);
                    break;
                case DcVoltageSourceComponent:
                    yield return (Pin(component.Id, "+"), Pin(component.Id, "-"), component.Id);
                    break;
                case BatteryComponent battery:
                    foreach (var edge in SeriesEdges(battery.Id, battery.InternalResistance > 0))
                    {
                        yield return edge;
                    }

                    break;
                case SolarPanelComponent panel:
                    foreach (var edge in SeriesEdges(panel.Id, panel.SeriesResistance > 0))
                    {
                        yield return edge;
                    }

                    break;
                case SwitchComponent { State: SwitchState.Closed }:
                    yield return (Pin(component.Id, "1"), Pin(component.Id, "2"), component.Id);
                    break;
            }
        }
    }

    private IEnumerable<(int Left, int Right, string ComponentId)> DiodeEdges(DiodeComponent diode)
    {
        var anode = Pin(diode.Id, "A");
        var cathode = Pin(diode.Id, "K");
        if (!TryInternal(diode.Id, "junction", out var junction))
        {
            yield return (anode, cathode, diode.Id);
            yield break;
        }

        yield return (anode, junction, diode.Id);
        yield return (junction, cathode, diode.Id);
    }

    private IEnumerable<(int Left, int Right, string ComponentId)> SeriesEdges(string id, bool hasSeries)
    {
        var positive = Pin(id, "+");
        var negative = Pin(id, "-");
        if (!hasSeries)
        {
            yield return (positive, negative, id);
            yield break;
        }

        var series = _internal[id + ":series"];
        yield return (positive, series, id);
        yield return (series, negative, id);
    }

    private IEnumerable<(int Left, int Right, string ComponentId)> IdealEdges(IReadOnlySet<string> ledsOn)
    {
        foreach (var component in Circuit.Components)
        {
            switch (component)
            {
                case DcVoltageSourceComponent:
                    yield return (Pin(component.Id, "+"), Pin(component.Id, "-"), component.Id);
                    break;
                case InductorComponent:
                    yield return (Pin(component.Id, "1"), Pin(component.Id, "2"), component.Id);
                    break;
                case SwitchComponent { State: SwitchState.Closed }:
                    yield return (Pin(component.Id, "1"), Pin(component.Id, "2"), component.Id);
                    break;
                case LedComponent when ledsOn.Contains(component.Id):
                    yield return (Pin(component.Id, "A"), Pin(component.Id, "K"), component.Id);
                    break;
                case BatteryComponent battery:
                    yield return IdealSourceEdge(battery.Id, battery.InternalResistance > 0);
                    break;
                case SolarPanelComponent panel:
                    yield return IdealSourceEdge(panel.Id, panel.SeriesResistance > 0);
                    break;
            }
        }
    }

    private (int Left, int Right, string ComponentId) IdealSourceEdge(string id, bool hasSeries)
    {
        var negative = Pin(id, "-");
        var positive = hasSeries ? _internal[id + ":series"] : Pin(id, "+");
        return (positive, negative, id);
    }

    private static string Join(IEnumerable<string> ids)
    {
        var ordered = ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return ordered.Length == 0 ? "—" : string.Join(", ", ordered);
    }
}
