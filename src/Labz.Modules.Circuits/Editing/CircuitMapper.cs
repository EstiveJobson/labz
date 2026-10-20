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
            Required(component, "resistance", KnownUnits.Ohm),
            Required(component, "powerRating", KnownUnits.Watt)),
        "capacitor" => new CapacitorComponent(
            component.Id,
            Required(component, "capacitance", KnownUnits.Farad),
            Optional(component, "initialVoltage", KnownUnits.Volt)),
        "inductor" => new InductorComponent(
            component.Id,
            Required(component, "inductance", KnownUnits.Henry),
            Optional(component, "initialCurrent", KnownUnits.Ampere)),
        "diode" => new DiodeComponent(component.Id, new DiodeModel(
            Required(component, "IS", KnownUnits.Ampere),
            Required(component, "N", KnownUnits.Dimensionless),
            Required(component, "RS", KnownUnits.Ohm))),
        "led" => new LedComponent(
            component.Id,
            Required(component, "forwardVoltage", KnownUnits.Volt),
            Optional(component, "forwardCurrent", KnownUnits.Ampere),
            component.Params.TryGetValue("color", out var color) ? color : null),
        "dc_voltage_source" => new DcVoltageSourceComponent(
            component.Id,
            Required(component, "voltage", KnownUnits.Volt)),
        "solar_panel" => new SolarPanelComponent(
            component.Id,
            Required(component, "openCircuitVoltage", KnownUnits.Volt),
            Required(component, "seriesResistance", KnownUnits.Ohm),
            Optional(component, "ratedPower", KnownUnits.Watt)),
        "battery" => new BatteryComponent(
            component.Id,
            Required(component, "nominalVoltage", KnownUnits.Volt),
            Required(component, "internalResistance", KnownUnits.Ohm),
            Required(component, "capacity", KnownUnits.AmpereHour),
            Required(component, "usableDepthOfDischarge", KnownUnits.Dimensionless)),
        "switch" => new SwitchComponent(
            component.Id,
            component.Params.TryGetValue("state", out var state) && state == "closed" ? SwitchState.Closed : SwitchState.Open,
            Optional(component, "Ron", KnownUnits.Ohm) ?? SwitchComponent.DefaultRonOhms,
            Optional(component, "Roff", KnownUnits.Ohm) ?? SwitchComponent.DefaultRoffOhms),
        "ground" => new GroundComponent(component.Id),
        _ => throw new ArgumentException(Strings.UnknownType(component.Type)),
    };

    private static double Required(SchematicComponent component, string name, Unit unit)
    {
        if (!component.Params.TryGetValue(name, out var text) || string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(Strings.RequiredParam(name));
        }

        return QuantityParser.Parse(text, unit.Dimension).In(unit);
    }

    private static double? Optional(SchematicComponent component, string name, Unit unit)
    {
        if (!component.Params.TryGetValue(name, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return QuantityParser.Parse(text, unit.Dimension).In(unit);
    }
}
