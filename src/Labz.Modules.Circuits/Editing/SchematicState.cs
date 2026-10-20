using Labz.Core.Project;

namespace Labz.Modules.Circuits;

public readonly record struct PinRef(string ComponentId, string Pin);

public sealed class SchematicComponent
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public Dictionary<string, string> Params { get; } = new(StringComparer.Ordinal);

    public List<string> Pins { get; } = [];

    public double X { get; set; }

    public double Y { get; set; }

    public int Rotation { get; set; }
}

public sealed class SchematicNet
{
    public string Id { get; set; } = string.Empty;

    public List<PinRef> Connections { get; } = [];
}

public sealed class SchematicState
{
    public List<SchematicComponent> Components { get; } = [];

    public List<SchematicNet> Nets { get; } = [];

    public List<UsageProfileDocument> UsageProfiles { get; } = [];

    public List<ProjectHistoryEntry> History { get; } = [];

    public SchematicMemento Capture() => SchematicMemento.Capture(this);

    public SchematicComponent? Find(string id) =>
        Components.FirstOrDefault(component => component.Id == id);
}

public sealed record ComponentSnapshot(
    string Id,
    string Type,
    IReadOnlyList<KeyValuePair<string, string>> Params,
    IReadOnlyList<string> Pins,
    double X,
    double Y,
    int Rotation);

public sealed record NetSnapshot(string Id, IReadOnlyList<PinRef> Connections);

public sealed record UsageSnapshot(string Target, double HoursPerDay);

public sealed record HistorySnapshot(DateTimeOffset At, string Source, string Change);

public sealed class SchematicMemento
{
    public required IReadOnlyList<ComponentSnapshot> Components { get; init; }

    public required IReadOnlyList<NetSnapshot> Nets { get; init; }

    public required IReadOnlyList<UsageSnapshot> UsageProfiles { get; init; }

    public required IReadOnlyList<HistorySnapshot> History { get; init; }

    public static SchematicMemento Capture(SchematicState state) => new()
    {
        Components = state.Components.Select(component => new ComponentSnapshot(
            component.Id,
            component.Type,
            component.Params.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
            component.Pins.ToArray(),
            component.X,
            component.Y,
            component.Rotation)).ToArray(),
        Nets = state.Nets.Select(net => new NetSnapshot(net.Id, net.Connections.ToArray())).ToArray(),
        UsageProfiles = state.UsageProfiles.Select(profile => new UsageSnapshot(profile.Target, profile.HoursPerDay)).ToArray(),
        History = state.History.Select(entry => new HistorySnapshot(entry.At, entry.Source, entry.Change)).ToArray(),
    };

    public bool SameAs(SchematicMemento other)
    {
        if (Components.Count != other.Components.Count
            || Nets.Count != other.Nets.Count
            || UsageProfiles.Count != other.UsageProfiles.Count
            || History.Count != other.History.Count)
        {
            return false;
        }

        for (var index = 0; index < Components.Count; index++)
        {
            var left = Components[index];
            var right = other.Components[index];
            if (left.Id != right.Id
                || left.Type != right.Type
                || left.X != right.X
                || left.Y != right.Y
                || left.Rotation != right.Rotation
                || !left.Pins.SequenceEqual(right.Pins)
                || !left.Params.SequenceEqual(right.Params))
            {
                return false;
            }
        }

        for (var index = 0; index < Nets.Count; index++)
        {
            if (Nets[index].Id != other.Nets[index].Id
                || !Nets[index].Connections.SequenceEqual(other.Nets[index].Connections))
            {
                return false;
            }
        }

        return UsageProfiles.SequenceEqual(other.UsageProfiles)
            && History.SequenceEqual(other.History);
    }

    public void Restore(SchematicState state)
    {
        state.Components.Clear();
        foreach (var snapshot in Components)
        {
            var component = new SchematicComponent
            {
                Id = snapshot.Id,
                Type = snapshot.Type,
                X = snapshot.X,
                Y = snapshot.Y,
                Rotation = snapshot.Rotation,
            };
            component.Pins.AddRange(snapshot.Pins);
            foreach (var (key, value) in snapshot.Params)
            {
                component.Params[key] = value;
            }

            state.Components.Add(component);
        }

        state.Nets.Clear();
        foreach (var snapshot in Nets)
        {
            var net = new SchematicNet { Id = snapshot.Id };
            net.Connections.AddRange(snapshot.Connections);
            state.Nets.Add(net);
        }

        state.UsageProfiles.Clear();
        state.UsageProfiles.AddRange(UsageProfiles.Select(profile => new UsageProfileDocument
        {
            Target = profile.Target,
            HoursPerDay = profile.HoursPerDay,
        }));

        state.History.Clear();
        state.History.AddRange(History.Select(entry => new ProjectHistoryEntry
        {
            At = entry.At,
            Source = entry.Source,
            Change = entry.Change,
        }));
    }
}
