using Labz.Core.Units;

namespace Labz.Modules.Circuits;

/// <summary>In-memory circuit shaped like the §6.1 component and net lists. Serialization is M2.</summary>
public sealed class CircuitModel
{
    public CircuitModel(IReadOnlyList<CircuitComponent> components, IReadOnlyList<CircuitNet> nets)
    {
        Components = components ?? throw new ArgumentNullException(nameof(components));
        Nets = nets ?? throw new ArgumentNullException(nameof(nets));
    }

    public IReadOnlyList<CircuitComponent> Components { get; }

    public IReadOnlyList<CircuitNet> Nets { get; }
}

public sealed record PinConnection(string ComponentId, string Pin);

public sealed record CircuitNet(string Id, IReadOnlyList<PinConnection> Connections);

public abstract class CircuitComponent
{
    protected CircuitComponent(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(Strings.ComponentIdRequired());
        }

        Id = id;
    }

    public string Id { get; }

    public abstract string TypeName { get; }

    public abstract IReadOnlyList<string> Pins { get; }

    public abstract IReadOnlyDictionary<string, Quantity> Parameters { get; }
}

public sealed class ResistorComponent : CircuitComponent
{
    public ResistorComponent(string id, double resistanceOhms, double powerRatingWatts = 0.25)
        : base(id)
    {
        ParameterValidation.RequirePositive(resistanceOhms, Strings.PositiveFinite(Strings.Resistance()));
        ParameterValidation.RequirePositive(powerRatingWatts, Strings.PositiveFinite(Strings.PowerRating()));
        ResistanceOhms = resistanceOhms;
        PowerRatingWatts = powerRatingWatts;
        Parameters = new Dictionary<string, Quantity>
        {
            ["resistance"] = Quantity.Of(resistanceOhms, KnownUnits.Ohm),
            ["powerRating"] = Quantity.Of(powerRatingWatts, KnownUnits.Watt),
        };
    }

    public double ResistanceOhms { get; }

    public double PowerRatingWatts { get; }

    public override string TypeName => "resistor";

    public override IReadOnlyList<string> Pins { get; } = ["1", "2"];

    public override IReadOnlyDictionary<string, Quantity> Parameters { get; }
}

public sealed class CapacitorComponent : CircuitComponent
{
    public CapacitorComponent(string id, double capacitanceFarads, double? initialVoltage = null)
        : base(id)
    {
        if (!double.IsFinite(capacitanceFarads) || capacitanceFarads <= 0)
        {
            ParameterValidation.ThrowOutOfRange(Strings.PositiveFinite(Strings.Capacitance()));
        }

        CapacitanceFarads = capacitanceFarads;
        InitialVoltage = initialVoltage;
        var parameters = new Dictionary<string, Quantity>
        {
            ["capacitance"] = Quantity.Of(capacitanceFarads, KnownUnits.Farad),
        };
        if (initialVoltage is not null)
        {
            parameters["initialVoltage"] = Quantity.Of(initialVoltage.Value, KnownUnits.Volt);
        }

        Parameters = parameters;
    }

    public double CapacitanceFarads { get; }

    public double? InitialVoltage { get; }

    public override string TypeName => "capacitor";

    public override IReadOnlyList<string> Pins { get; } = ["1", "2"];

    public override IReadOnlyDictionary<string, Quantity> Parameters { get; }
}

public sealed class InductorComponent : CircuitComponent
{
    public InductorComponent(string id, double inductanceHenries, double? initialCurrent = null)
        : base(id)
    {
        if (!double.IsFinite(inductanceHenries) || inductanceHenries <= 0)
        {
            ParameterValidation.ThrowOutOfRange(Strings.PositiveFinite(Strings.Inductance()));
        }

        InductanceHenries = inductanceHenries;
        InitialCurrent = initialCurrent;
        var parameters = new Dictionary<string, Quantity>
        {
            ["inductance"] = Quantity.Of(inductanceHenries, KnownUnits.Henry),
        };
        if (initialCurrent is not null)
        {
            parameters["initialCurrent"] = Quantity.Of(initialCurrent.Value, KnownUnits.Ampere);
        }

        Parameters = parameters;
    }

    public double InductanceHenries { get; }

    public double? InitialCurrent { get; }

    public override string TypeName => "inductor";

    public override IReadOnlyList<string> Pins { get; } = ["1", "2"];

    public override IReadOnlyDictionary<string, Quantity> Parameters { get; }
}

public sealed class DiodeComponent : CircuitComponent
{
    public DiodeComponent(string id, DiodeModel? model = null)
        : base(id)
    {
        Model = model ?? DiodePresets.In4148;
        Model.EnsureValid();
        Parameters = new Dictionary<string, Quantity>
        {
            ["IS"] = new(Model.SaturationCurrent, Dimension.Current),
            ["N"] = Quantity.Of(Model.EmissionCoefficient, KnownUnits.Dimensionless),
            ["RS"] = Quantity.Of(Model.SeriesResistance, KnownUnits.Ohm),
        };
    }

    public DiodeModel Model { get; }

    public override string TypeName => "diode";

    public override IReadOnlyList<string> Pins { get; } = ["A", "K"];

    public override IReadOnlyDictionary<string, Quantity> Parameters { get; }
}

public sealed class LedComponent : CircuitComponent
{
    public LedComponent(string id, double forwardVoltage, double? forwardCurrent = null, string? color = null)
        : base(id)
    {
        if (!double.IsFinite(forwardVoltage) || forwardVoltage <= 0)
        {
            ParameterValidation.ThrowOutOfRange(Strings.PositiveFinite(Strings.ForwardVoltage()));
        }

        ForwardVoltage = forwardVoltage;
        ForwardCurrent = forwardCurrent;
        Color = color;
        var parameters = new Dictionary<string, Quantity>
        {
            ["forwardVoltage"] = Quantity.Of(forwardVoltage, KnownUnits.Volt),
        };
        if (forwardCurrent is not null)
        {
            parameters["forwardCurrent"] = Quantity.Of(forwardCurrent.Value, KnownUnits.Ampere);
        }

        Parameters = parameters;
    }

    public double ForwardVoltage { get; }

    public double? ForwardCurrent { get; }

    public string? Color { get; }

    public override string TypeName => "led";

    public override IReadOnlyList<string> Pins { get; } = ["A", "K"];

    public override IReadOnlyDictionary<string, Quantity> Parameters { get; }
}

public sealed class DcVoltageSourceComponent : CircuitComponent
{
    public DcVoltageSourceComponent(string id, double voltage)
        : base(id)
    {
        if (!double.IsFinite(voltage))
        {
            ParameterValidation.ThrowOutOfRange(Strings.FiniteValue(Strings.Voltage()));
        }

        Voltage = voltage;
        Parameters = new Dictionary<string, Quantity>
        {
            ["voltage"] = Quantity.Of(voltage, KnownUnits.Volt),
        };
    }

    public double Voltage { get; }

    public override string TypeName => "dc_voltage_source";

    public override IReadOnlyList<string> Pins { get; } = ["+", "-"];

    public override IReadOnlyDictionary<string, Quantity> Parameters { get; }
}

public sealed class SolarPanelComponent : CircuitComponent
{
    public SolarPanelComponent(
        string id,
        double openCircuitVoltage,
        double seriesResistance,
        double? ratedPowerWatts = null)
        : base(id)
    {
        if (!double.IsFinite(openCircuitVoltage))
        {
            ParameterValidation.ThrowOutOfRange(Strings.FiniteValue(Strings.OpenCircuitVoltage()));
        }

        if (!double.IsFinite(seriesResistance) || seriesResistance < 0)
        {
            ParameterValidation.ThrowOutOfRange(Strings.NonNegativeFinite(Strings.SeriesResistance()));
        }

        OpenCircuitVoltage = openCircuitVoltage;
        SeriesResistance = seriesResistance;
        RatedPowerWatts = ratedPowerWatts;
        var parameters = new Dictionary<string, Quantity>
        {
            ["openCircuitVoltage"] = Quantity.Of(openCircuitVoltage, KnownUnits.Volt),
            ["seriesResistance"] = Quantity.Of(seriesResistance, KnownUnits.Ohm),
        };
        if (ratedPowerWatts is not null)
        {
            parameters["ratedPower"] = Quantity.Of(ratedPowerWatts.Value, KnownUnits.Watt);
        }

        Parameters = parameters;
    }

    public double OpenCircuitVoltage { get; }

    public double SeriesResistance { get; }

    public double? RatedPowerWatts { get; }

    public override string TypeName => "solar_panel";

    public override IReadOnlyList<string> Pins { get; } = ["+", "-"];

    public override IReadOnlyDictionary<string, Quantity> Parameters { get; }
}

public sealed class BatteryComponent : CircuitComponent
{
    public BatteryComponent(
        string id,
        double nominalVoltage,
        double internalResistance,
        double capacityAmpereHours,
        double usableDepthOfDischarge)
        : base(id)
    {
        if (!double.IsFinite(nominalVoltage))
        {
            ParameterValidation.ThrowOutOfRange(Strings.FiniteValue(Strings.NominalVoltage()));
        }

        if (!double.IsFinite(internalResistance) || internalResistance < 0)
        {
            ParameterValidation.ThrowOutOfRange(Strings.NonNegativeFinite(Strings.InternalResistance()));
        }

        if (!double.IsFinite(capacityAmpereHours) || capacityAmpereHours < 0)
        {
            ParameterValidation.ThrowOutOfRange(Strings.NonNegativeFinite(Strings.Capacity()));
        }

        if (!double.IsFinite(usableDepthOfDischarge) || usableDepthOfDischarge is < 0 or > 1)
        {
            ParameterValidation.ThrowOutOfRange(Strings.DepthOfDischarge());
        }

        NominalVoltage = nominalVoltage;
        InternalResistance = internalResistance;
        CapacityAmpereHours = capacityAmpereHours;
        UsableDepthOfDischarge = usableDepthOfDischarge;
        Parameters = new Dictionary<string, Quantity>
        {
            ["nominalVoltage"] = Quantity.Of(nominalVoltage, KnownUnits.Volt),
            ["internalResistance"] = Quantity.Of(internalResistance, KnownUnits.Ohm),
            ["capacity"] = Quantity.Of(capacityAmpereHours, KnownUnits.AmpereHour),
            ["usableDepthOfDischarge"] = Quantity.Of(usableDepthOfDischarge, KnownUnits.Dimensionless),
        };
    }

    public double NominalVoltage { get; }

    public double InternalResistance { get; }

    public double CapacityAmpereHours { get; }

    public double UsableDepthOfDischarge { get; }

    public override string TypeName => "battery";

    public override IReadOnlyList<string> Pins { get; } = ["+", "-"];

    public override IReadOnlyDictionary<string, Quantity> Parameters { get; }
}

public enum SwitchState
{
    Open,
    Closed,
}

public sealed class SwitchComponent : CircuitComponent
{
    public const double DefaultRonOhms = 0.001;

    public const double DefaultRoffOhms = 1e9;

    public SwitchComponent(string id, SwitchState state, double ronOhms = DefaultRonOhms, double roffOhms = DefaultRoffOhms)
        : base(id)
    {
        if (!double.IsFinite(ronOhms) || ronOhms < 0)
        {
            ParameterValidation.ThrowOutOfRange(Strings.NonNegativeFinite(Strings.Ron()));
        }

        if (!double.IsFinite(roffOhms) || roffOhms < 0)
        {
            ParameterValidation.ThrowOutOfRange(Strings.NonNegativeFinite(Strings.Roff()));
        }

        State = state;
        RonOhms = ronOhms;
        RoffOhms = roffOhms;
        Parameters = new Dictionary<string, Quantity>
        {
            ["Ron"] = Quantity.Of(ronOhms, KnownUnits.Ohm),
            ["Roff"] = Quantity.Of(roffOhms, KnownUnits.Ohm),
        };
    }

    public SwitchState State { get; }

    /// <summary>Stored for the SPICE path (M3). The DC engine treats the switch as ideal and does not stamp this value.</summary>
    public double RonOhms { get; }

    /// <summary>Stored for the SPICE path (M3). The DC engine treats an open switch as removed.</summary>
    public double RoffOhms { get; }

    public override string TypeName => "switch";

    public override IReadOnlyList<string> Pins { get; } = ["1", "2"];

    public override IReadOnlyDictionary<string, Quantity> Parameters { get; }
}

public sealed class GroundComponent : CircuitComponent
{
    public GroundComponent(string id)
        : base(id)
    {
        Parameters = new Dictionary<string, Quantity>();
    }

    public override string TypeName => "ground";

    public override IReadOnlyList<string> Pins { get; } = ["g"];

    public override IReadOnlyDictionary<string, Quantity> Parameters { get; }
}

public sealed class CircuitBuilder
{
    private readonly List<CircuitComponent> _components = [];
    private readonly Dictionary<string, List<PinConnection>> _nets = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);

    public CircuitBuilder Add(CircuitComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!_ids.Add(component.Id))
        {
            throw new ArgumentException(Strings.DuplicateComponent(component.Id));
        }

        _components.Add(component);
        return this;
    }

    public CircuitBuilder Connect(string netId, params (string ComponentId, string Pin)[] pins)
    {
        if (string.IsNullOrWhiteSpace(netId))
        {
            throw new ArgumentException(Strings.NetIdRequired());
        }

        if (!_nets.TryGetValue(netId, out var connections))
        {
            connections = [];
            _nets[netId] = connections;
        }

        foreach (var (componentId, pin) in pins)
        {
            connections.Add(new PinConnection(componentId, pin));
        }

        return this;
    }

    public CircuitModel Build()
    {
        var nets = _nets
            .Select(pair => new CircuitNet(pair.Key, pair.Value.ToArray()))
            .ToArray();
        var model = new CircuitModel(_components.ToArray(), nets);
        CircuitValidator.ValidateStructure(model);
        return model;
    }
}

internal static class CircuitValidator
{
    public static void ValidateStructure(CircuitModel circuit)
    {
        var components = circuit.Components.ToDictionary(component => component.Id, StringComparer.Ordinal);
        var seenPins = new HashSet<string>(StringComparer.Ordinal);
        foreach (var net in circuit.Nets)
        {
            if (components.ContainsKey(net.Id))
            {
                throw new CircuitNameCollisionException(Strings.NameCollision(net.Id), [net.Id]);
            }

            foreach (var connection in net.Connections)
            {
                if (!components.TryGetValue(connection.ComponentId, out var component))
                {
                    throw new ArgumentException(Strings.UnknownComponent(net.Id, connection.ComponentId));
                }

                if (!component.Pins.Contains(connection.Pin, StringComparer.Ordinal))
                {
                    throw new ArgumentException(Strings.UnknownPin(component.Id, connection.Pin));
                }

                if (!seenPins.Add(component.Id + "." + connection.Pin))
                {
                    throw new ArgumentException(Strings.PinConnectedTwice(component.Id, connection.Pin));
                }
            }
        }
    }
}
