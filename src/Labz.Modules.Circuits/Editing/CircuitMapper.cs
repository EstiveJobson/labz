using Labz.Core.Units;

namespace Labz.Modules.Circuits;

public static class CircuitMapper
{
    public static CircuitModel ToCircuit(SchematicState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var builder = new CircuitBuilder();
        foreach (var component in state.Components)
        {
            builder.Add(Create(component));
        }

        foreach (var net in state.Nets.Where(candidate => candidate.Connections.Count > 0))
        {
            var pins = net.Connections.Select(connection => (connection.ComponentId, connection.Pin)).ToArray();
            builder.Connect(net.Id, pins);
        }

        return builder.Build();
    }

    public static EnergySettings ToEnergy(SchematicState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        double? load = state.UsageProfiles
            .FirstOrDefault(profile => profile.Target == ComponentLibrary.LoadTarget)
            ?.HoursPerDay;
        var panelId = state.Components.FirstOrDefault(component => component.Type == "solar_panel")?.Id;
        double? peak = panelId is null
            ? null
            : state.UsageProfiles.FirstOrDefault(profile => profile.Target == panelId)?.HoursPerDay;
        return new EnergySettings(load, peak, ComponentLibrary.ChargeEfficiency);
    }

    private static CircuitComponent Create(SchematicComponent component) => component.Type switch
    {
        "resistor" => new ResistorComponent(
            component.Id,
            Required(component, "resistance", Dimension.Resistance),
            Required(component, "powerRating", Dimension.Power)),
        "capacitor" => new CapacitorComponent(
            component.Id,
            Required(component, "capacitance", Dimension.Capacitance),
            Optional(component, "initialVoltage", Dimension.Voltage)),
        "inductor" => new InductorComponent(
            component.Id,
            Required(component, "inductance", Dimension.Inductance),
            Optional(component, "initialCurrent", Dimension.Current)),
        "diode" => new DiodeComponent(component.Id, new DiodeModel(
            Required(component, "IS", Dimension.Current),
            Required(component, "N", Dimension.None),
            Required(component, "RS", Dimension.Resistance),
            Required(component, "BV", Dimension.Voltage))),
        "led" => new LedComponent(
            component.Id,
            Required(component, "forwardVoltage", Dimension.Voltage),
            Optional(component, "forwardCurrent", Dimension.Current),
            component.Params.TryGetValue("color", out var color) ? color : null),
        "dc_voltage_source" => new DcVoltageSourceComponent(
            component.Id,
            Required(component, "voltage", Dimension.Voltage)),
        "solar_panel" => new SolarPanelComponent(
            component.Id,
            Required(component, "openCircuitVoltage", Dimension.Voltage),
            Required(component, "seriesResistance", Dimension.Resistance),
            Optional(component, "ratedPower", Dimension.Power)),
        "battery" => new BatteryComponent(
            component.Id,
            Required(component, "nominalVoltage", Dimension.Voltage),
            Required(component, "internalResistance", Dimension.Resistance),
            Required(component, "capacity", Dimension.Charge),
            Required(component, "usableDepthOfDischarge", Dimension.None)),
        "switch" => new SwitchComponent(
            component.Id,
            component.Params.TryGetValue("state", out var state) && state == "closed" ? SwitchState.Closed : SwitchState.Open,
            Optional(component, "Ron", Dimension.Resistance) ?? SwitchComponent.DefaultRonOhms,
            Optional(component, "Roff", Dimension.Resistance) ?? SwitchComponent.DefaultRoffOhms),
        "ground" => new GroundComponent(component.Id),
        _ => throw new ArgumentException(Strings.UnknownType(component.Type)),
    };

    private static double Required(SchematicComponent component, string name, Dimension dimension)
    {
        if (!component.Params.TryGetValue(name, out var text) || string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(Strings.RequiredParam(name));
        }

        return QuantityParser.Parse(text, dimension).Value;
    }

    private static double? Optional(SchematicComponent component, string name, Dimension dimension)
    {
        if (!component.Params.TryGetValue(name, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return QuantityParser.Parse(text, dimension).Value;
    }
}
