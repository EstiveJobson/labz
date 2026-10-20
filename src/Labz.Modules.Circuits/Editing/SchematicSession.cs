using Labz.Core.Drawing;
using Labz.Core.Project;

namespace Labz.Modules.Circuits;

public sealed class SchematicSession
{
    private readonly UndoStack _undo = new();
    private readonly TimeProvider _clock;
    private string? _dragId;
    private double _dragStartX;
    private double _dragStartY;

    private SchematicSession(ProjectDocument document, TimeProvider clock)
    {
        _clock = clock;
        Id = document.Id;
        Name = document.Name;
        CreatedAt = document.CreatedAt;
        Idea = document.Idea;
        Analyses = document.Modules.Circuits.Analyses;
        State = new SchematicState();
        CopyInto(document, State);
        SchematicNormalizer.Normalize(State);
    }

    public SchematicState State { get; }

    public string Id { get; private set; }

    public string Name { get; set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string Idea { get; set; }

    public List<AnalysisDocument> Analyses { get; }

    public bool CanUndo => _undo.CanUndo;

    public bool CanRedo => _undo.CanRedo;

    public int UndoDepth => _undo.Count;

    public event Action<bool>? Changed;

    public static SchematicSession FromDocument(ProjectDocument document, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new SchematicSession(document, clock ?? TimeProvider.System);
    }

    public static SchematicSession CreateBlank(string name, DateTimeOffset createdAt, string? idea = null, TimeProvider? clock = null)
    {
        var document = new ProjectDocument
        {
            Id = Guid.NewGuid().ToString("D"),
            Name = name,
            CreatedAt = createdAt,
            Idea = idea ?? string.Empty,
            Modules = new ProjectModules
            {
                Circuits = new CircuitDocument
                {
                    Analyses = [new AnalysisDocument { Type = "op" }],
                },
            },
        };
        return FromDocument(document, clock);
    }

    public ProjectDocument ToDocument()
    {
        var circuit = new CircuitDocument
        {
            Analyses = Analyses.Select(analysis => new AnalysisDocument
            {
                Type = analysis.Type,
                Step = analysis.Step,
                Stop = analysis.Stop,
            }).ToList(),
            UsageProfiles = State.UsageProfiles.Select(profile => new UsageProfileDocument
            {
                Target = profile.Target,
                HoursPerDay = profile.HoursPerDay,
            }).ToList(),
        };

        foreach (var component in State.Components)
        {
            circuit.Components.Add(new ComponentDocument
            {
                Id = component.Id,
                Type = component.Type,
                Params = new Dictionary<string, string>(component.Params, StringComparer.Ordinal),
                Pins = component.Pins.ToList(),
                Layout = new LayoutDocument
                {
                    X = component.X,
                    Y = component.Y,
                    Rotation = component.Rotation,
                },
            });
        }

        foreach (var net in State.Nets)
        {
            circuit.Nets.Add(new NetDocument
            {
                Id = net.Id,
                Connections = net.Connections.Select(connection => new PinEndpoint
                {
                    ComponentId = connection.ComponentId,
                    Pin = connection.Pin,
                }).ToList(),
            });
        }

        return new ProjectDocument
        {
            SchemaVersion = ProjectVersions.Current,
            Id = Id,
            Name = Name,
            CreatedAt = CreatedAt,
            Idea = Idea,
            Modules = new ProjectModules { Circuits = circuit },
            History = State.History.Select(entry => new ProjectHistoryEntry
            {
                At = entry.At,
                Source = entry.Source,
                Change = entry.Change,
            }).ToList(),
        };
    }

    public string Place(string type, double x, double y)
    {
        var definition = ComponentLibrary.Get(type);
        var id = ComponentIds.Next(definition.IdPrefix, State.Components.Select(component => component.Id));
        Edit(EditKind.Place, true, $"place {id}", state =>
        {
            var component = new SchematicComponent
            {
                Id = id,
                Type = definition.Type,
                X = GridSnap.Snap(x),
                Y = GridSnap.Snap(y),
            };
            component.Pins.AddRange(definition.Pins);
            foreach (var parameter in definition.Params)
            {
                if (parameter.DefaultText is not null)
                {
                    component.Params[parameter.Name] = parameter.DefaultText;
                }
            }

            state.Components.Add(component);
        });
        return id;
    }

    public void Move(string id, double x, double y)
    {
        Edit(EditKind.Move, false, $"move {id}", state =>
        {
            var component = Require(state, id);
            component.X = GridSnap.Snap(x);
            component.Y = GridSnap.Snap(y);
        });
    }

    public void BeginDrag(string id)
    {
        var component = State.Find(id) ?? throw new ArgumentException(Strings.UnknownComponent("?", id), nameof(id));
        _dragId = id;
        _dragStartX = component.X;
        _dragStartY = component.Y;
    }

    public void PreviewDrag(string id, double x, double y)
    {
        if (_dragId != id)
        {
            return;
        }

        var component = State.Find(id);
        if (component is null)
        {
            return;
        }

        component.X = GridSnap.Snap(x);
        component.Y = GridSnap.Snap(y);
    }

    public void CommitDrag(string id)
    {
        if (_dragId != id)
        {
            return;
        }

        var component = State.Find(id);
        _dragId = null;
        if (component is null)
        {
            return;
        }

        var endX = component.X;
        var endY = component.Y;
        component.X = _dragStartX;
        component.Y = _dragStartY;
        Move(id, endX, endY);
    }

    public void Rotate(string id)
    {
        Edit(EditKind.Rotate, false, $"rotate {id}", state =>
        {
            var component = Require(state, id);
            component.Rotation = SymbolTransform.Normalize(component.Rotation + 90);
        });
    }

    public void Delete(string id)
    {
        Edit(EditKind.Delete, true, $"delete {id}", state =>
        {
            var component = Require(state, id);
            state.Components.Remove(component);
            foreach (var net in state.Nets)
            {
                net.Connections.RemoveAll(connection => connection.ComponentId == id);
            }
        });
    }

    public bool TrySetParam(string id, string name, string text, out string? error)
    {
        var component = State.Find(id) ?? throw new ArgumentException(Strings.UnknownComponent("?", id), nameof(id));
        if (!ParamEditor.TryAccept(component.Type, name, text, out error))
        {
            return false;
        }

        var definition = ComponentLibrary.Get(component.Type);
        var parameter = definition.Params.First(candidate => candidate.Name == name);
        Edit(EditKind.Value, true, $"{id}.{name}", state =>
        {
            var target = Require(state, id);
            if (string.IsNullOrWhiteSpace(text) && parameter.Optional)
            {
                target.Params.Remove(name);
            }
            else
            {
                target.Params[name] = text.Trim();
            }
        });
        return true;
    }

    public void SetSwitchClosed(string id, bool closed)
    {
        if (!TrySetParam(id, "state", closed ? "closed" : "open", out var error))
        {
            throw new ArgumentException(error, nameof(id));
        }
    }

    public void ApplyDiodePreset(string id, bool schottky)
    {
        var component = State.Find(id) ?? throw new ArgumentException(Strings.UnknownComponent("?", id), nameof(id));
        if (component.Type != "diode")
        {
            throw new ArgumentException(Strings.UnknownType(component.Type), nameof(id));
        }

        var preset = ComponentLibrary.DiodePreset(schottky);
        Edit(EditKind.Value, true, $"preset {id}", state =>
        {
            var target = Require(state, id);
            foreach (var (name, value) in preset)
            {
                target.Params[name] = value;
            }
        });
    }

    public void ConnectPins(string firstId, string firstPin, string secondId, string secondPin)
    {
        Edit(EditKind.Wire, true, $"wire {firstId}.{firstPin} {secondId}.{secondPin}", state =>
        {
            if (firstId == secondId && firstPin == secondPin)
            {
                return;
            }

            RequirePin(state, firstId, firstPin);
            RequirePin(state, secondId, secondPin);
            var first = new PinRef(firstId, firstPin);
            var second = new PinRef(secondId, secondPin);
            var firstNet = FindNet(state, first);
            var secondNet = FindNet(state, second);
            if (firstNet is not null && ReferenceEquals(firstNet, secondNet))
            {
                return;
            }

            if (firstNet is null && secondNet is null)
            {
                var net = new SchematicNet { Id = NextNetId(state) };
                net.Connections.Add(first);
                net.Connections.Add(second);
                state.Nets.Add(net);
                return;
            }

            if (firstNet is null)
            {
                secondNet!.Connections.Add(first);
                return;
            }

            if (secondNet is null)
            {
                firstNet.Connections.Add(second);
                return;
            }

            SchematicNormalizer.Merge(state, firstNet, secondNet);
        });
    }

    public void ConnectPinToNet(string componentId, string pin, string netId)
    {
        Edit(EditKind.Wire, true, $"wire {componentId}.{pin} {netId}", state =>
        {
            RequirePin(state, componentId, pin);
            var net = state.Nets.FirstOrDefault(candidate => candidate.Id == netId)
                ?? throw new ArgumentException(Strings.NetIdRequired(), nameof(netId));
            var endpoint = new PinRef(componentId, pin);
            var current = FindNet(state, endpoint);
            if (ReferenceEquals(current, net))
            {
                return;
            }

            if (current is null)
            {
                net.Connections.Add(endpoint);
                return;
            }

            SchematicNormalizer.Merge(state, net, current);
        });
    }

    public void DeleteWire(string netId, string? leafComponentId, string? leafPin)
    {
        Edit(EditKind.Wire, true, $"delete wire {netId}", state =>
        {
            var net = state.Nets.FirstOrDefault(candidate => candidate.Id == netId);
            if (net is null)
            {
                return;
            }

            if (leafComponentId is null || leafPin is null)
            {
                state.Nets.Remove(net);
                return;
            }

            net.Connections.RemoveAll(connection =>
                connection.ComponentId == leafComponentId && connection.Pin == leafPin);
        });
    }

    public void SetUsageHours(string target, double hours)
    {
        if (!double.IsFinite(hours) || hours is < 0 or > 24)
        {
            ParameterValidation.ThrowOutOfRange(Strings.HoursInvalid());
        }

        Edit(EditKind.Usage, true, $"usage {target}", state =>
        {
            var profile = state.UsageProfiles.FirstOrDefault(candidate => candidate.Target == target);
            if (profile is null)
            {
                state.UsageProfiles.Add(new UsageProfileDocument { Target = target, HoursPerDay = hours });
            }
            else
            {
                profile.HoursPerDay = hours;
            }
        });
    }

    public bool Undo()
    {
        var command = _undo.TryUndo();
        if (command is null)
        {
            return false;
        }

        command.Revert(State);
        Changed?.Invoke(command.AffectsCircuit);
        return true;
    }

    public bool Redo()
    {
        var command = _undo.TryRedo();
        if (command is null)
        {
            return false;
        }

        command.Apply(State);
        Changed?.Invoke(command.AffectsCircuit);
        return true;
    }

    private void Edit(EditKind kind, bool affectsCircuit, string description, Action<SchematicState> change)
    {
        var before = State.Capture();
        change(State);
        SchematicNormalizer.Normalize(State);
        var after = State.Capture();
        if (before.SameAs(after))
        {
            return;
        }

        State.History.Add(new ProjectHistoryEntry
        {
            At = _clock.GetUtcNow(),
            Source = "user",
            Change = description,
        });
        after = State.Capture();
        _undo.Push(new SnapshotCommand(kind, description, affectsCircuit, before, after));
        Changed?.Invoke(affectsCircuit);
    }

    private static void CopyInto(ProjectDocument document, SchematicState state)
    {
        foreach (var component in document.Modules.Circuits.Components)
        {
            var copy = new SchematicComponent
            {
                Id = component.Id,
                Type = component.Type,
                X = component.Layout.X,
                Y = component.Layout.Y,
                Rotation = SymbolTransform.Normalize(component.Layout.Rotation),
            };
            copy.Pins.AddRange(component.Pins);
            foreach (var (key, value) in component.Params)
            {
                copy.Params[key] = value;
            }

            state.Components.Add(copy);
        }

        foreach (var net in document.Modules.Circuits.Nets)
        {
            var copy = new SchematicNet { Id = net.Id };
            copy.Connections.AddRange(net.Connections.Select(connection => new PinRef(connection.ComponentId, connection.Pin)));
            state.Nets.Add(copy);
        }

        state.UsageProfiles.AddRange(document.Modules.Circuits.UsageProfiles.Select(profile => new UsageProfileDocument
        {
            Target = profile.Target,
            HoursPerDay = profile.HoursPerDay,
        }));
        state.History.AddRange(document.History.Select(entry => new ProjectHistoryEntry
        {
            At = entry.At,
            Source = entry.Source,
            Change = entry.Change,
        }));
    }

    private static SchematicComponent Require(SchematicState state, string id) =>
        state.Find(id) ?? throw new ArgumentException(Strings.UnknownComponent("?", id), nameof(id));

    private static void RequirePin(SchematicState state, string id, string pin)
    {
        var component = Require(state, id);
        if (!component.Pins.Contains(pin, StringComparer.Ordinal))
        {
            throw new ArgumentException(Strings.UnknownPin(id, pin));
        }
    }

    private static SchematicNet? FindNet(SchematicState state, PinRef pin) =>
        state.Nets.FirstOrDefault(net => net.Connections.Contains(pin));

    private static string NextNetId(SchematicState state)
    {
        var used = state.Nets.Select(net => net.Id).ToHashSet(StringComparer.Ordinal);
        for (var number = 1; number <= 9999; number++)
        {
            var id = "N" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!used.Contains(id))
            {
                return id;
            }
        }

        throw new InvalidOperationException(Strings.IdExhausted("N"));
    }
}

internal static class SchematicNormalizer
{
    public static void Normalize(SchematicState state)
    {
        var ids = state.Components.Select(component => component.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var net in state.Nets)
        {
            var distinct = new List<PinRef>();
            foreach (var connection in net.Connections)
            {
                if (!ids.Contains(connection.ComponentId) || distinct.Contains(connection))
                {
                    continue;
                }

                var component = state.Find(connection.ComponentId);
                if (component is null || !component.Pins.Contains(connection.Pin, StringComparer.Ordinal))
                {
                    continue;
                }

                distinct.Add(connection);
            }

            net.Connections.Clear();
            net.Connections.AddRange(distinct);
        }

        var grounds = state.Components
            .Where(component => component.Type == "ground")
            .Select(component => new PinRef(component.Id, "g"))
            .ToArray();
        if (grounds.Length > 0)
        {
            var groundNet = state.Nets.FirstOrDefault(net => net.Id == "0") ?? new SchematicNet { Id = "0" };
            if (!state.Nets.Contains(groundNet))
            {
                state.Nets.Add(groundNet);
            }

            foreach (var pin in grounds)
            {
                var other = state.Nets.FirstOrDefault(net => !ReferenceEquals(net, groundNet) && net.Connections.Contains(pin));
                if (other is not null)
                {
                    Merge(state, groundNet, other);
                }

                if (!groundNet.Connections.Contains(pin))
                {
                    groundNet.Connections.Add(pin);
                }
            }
        }

        var zeros = state.Nets.Where(net => net.Id == "0").ToArray();
        if (zeros.Length > 1)
        {
            var primary = zeros[0];
            foreach (var extra in zeros.Skip(1))
            {
                Merge(state, primary, extra);
            }
        }

        state.Nets.RemoveAll(net => net.Connections.Count == 0);
    }

    public static void Merge(SchematicState state, SchematicNet target, SchematicNet source)
    {
        if (ReferenceEquals(target, source))
        {
            return;
        }

        var keep = target;
        var drop = source;
        if (source.Id == "0" && target.Id != "0")
        {
            keep = source;
            drop = target;
        }

        foreach (var connection in drop.Connections)
        {
            if (!keep.Connections.Contains(connection))
            {
                keep.Connections.Add(connection);
            }
        }

        if (keep.Connections.Any(connection => IsGround(state, connection)))
        {
            keep.Id = "0";
        }

        state.Nets.Remove(drop);
    }

    private static bool IsGround(SchematicState state, PinRef pin) =>
        state.Find(pin.ComponentId)?.Type == "ground";
}
