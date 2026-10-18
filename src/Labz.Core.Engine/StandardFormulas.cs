using Labz.Core.Units;

namespace Labz.Core.Engine;

/// <summary>Ohm's law, power and the REF-1 energy formulas.</summary>
public static class StandardFormulas
{
    public static FormulaRegistry Create()
    {
        var registry = new FormulaRegistry();
        RegisterOhm(registry);
        RegisterEnergy(registry);
        registry.Register(new Formula(
            "power.rating",
            [Port("P", Dimension.Power, KnownUnits.Watt), Port("rating", Dimension.Power, KnownUnits.Watt)],
            Port("ratio", Dimension.None, KnownUnits.Dimensionless),
            args => args[0] / args[1]));
        return registry;
    }

    private static void RegisterOhm(FormulaRegistry registry)
    {
        registry.Register(new Formula(
            "ohm.V=IR",
            [Port("I", Dimension.Current, KnownUnits.Ampere), Port("R", Dimension.Resistance, KnownUnits.Ohm)],
            Port("V", Dimension.Voltage, KnownUnits.Volt),
            args => args[0] * args[1]));

        registry.Register(new Formula(
            "ohm.I=V/R",
            [Port("V", Dimension.Voltage, KnownUnits.Volt), Port("R", Dimension.Resistance, KnownUnits.Ohm)],
            Port("I", Dimension.Current, KnownUnits.Ampere),
            args => args[0] / args[1]));

        registry.Register(new Formula(
            "ohm.R=V/I",
            [Port("V", Dimension.Voltage, KnownUnits.Volt), Port("I", Dimension.Current, KnownUnits.Ampere)],
            Port("R", Dimension.Resistance, KnownUnits.Ohm),
            args => args[0] / args[1]));

        registry.Register(new Formula(
            "ohm.P=I2R",
            [Port("I", Dimension.Current, KnownUnits.Ampere), Port("R", Dimension.Resistance, KnownUnits.Ohm)],
            Port("P", Dimension.Power, KnownUnits.Watt),
            args => args[0] * args[0] * args[1]));

        registry.Register(new Formula(
            "ohm.P=VI",
            [Port("V", Dimension.Voltage, KnownUnits.Volt), Port("I", Dimension.Current, KnownUnits.Ampere)],
            Port("P", Dimension.Power, KnownUnits.Watt),
            args => args[0] * args[1]));

        registry.Register(new Formula(
            "ohm.P=V2/R",
            [Port("V", Dimension.Voltage, KnownUnits.Volt), Port("R", Dimension.Resistance, KnownUnits.Ohm)],
            Port("P", Dimension.Power, KnownUnits.Watt),
            args => args[0] * args[0] / args[1]));
    }

    private static void RegisterEnergy(FormulaRegistry registry)
    {
        registry.Register(new Formula(
            "energy.Wh_day",
            [Port("P_source", Dimension.Power, KnownUnits.Watt), Port("hours_per_day", Dimension.Time, KnownUnits.Hour)],
            Port("Wh_per_day", Dimension.EnergyPerDay, KnownUnits.WattHourPerDay),
            args => args[0] * args[1]));

        registry.Register(new Formula(
            "energy.usable_Ah",
            [Port("capacity", Dimension.Charge, KnownUnits.AmpereHour), Port("DoD", Dimension.None, KnownUnits.Dimensionless)],
            Port("usable_Ah", Dimension.Charge, KnownUnits.AmpereHour),
            args => args[0] * args[1]));

        registry.Register(new Formula(
            "energy.usable_Wh",
            [
                Port("capacity", Dimension.Charge, KnownUnits.AmpereHour),
                Port("DoD", Dimension.None, KnownUnits.Dimensionless),
                Port("V", Dimension.Voltage, KnownUnits.Volt),
            ],
            Port("usable_Wh", Dimension.Energy, KnownUnits.WattHour),
            args => args[0] * args[1] * args[2]));

        registry.Register(new Formula(
            "energy.runtime_h",
            [
                Port("capacity", Dimension.Charge, KnownUnits.AmpereHour),
                Port("DoD", Dimension.None, KnownUnits.Dimensionless),
                Port("I_discharge", Dimension.Current, KnownUnits.Ampere),
            ],
            Port("runtime_h", Dimension.Time, KnownUnits.Hour),
            args => args[0] * args[1] / args[2]));

        registry.Register(new Formula(
            "energy.autonomy_d",
            [Port("usable_Wh", Dimension.Energy, KnownUnits.WattHour), Port("Wh_per_day", Dimension.EnergyPerDay, KnownUnits.WattHourPerDay)],
            Port("autonomy_d", Dimension.Time, KnownUnits.Day),
            args => args[0] / args[1]));

        registry.Register(new Formula(
            "energy.charge_Wh_day",
            [Port("P_stored", Dimension.Power, KnownUnits.Watt), Port("peak_sun_hours", Dimension.Time, KnownUnits.Hour)],
            Port("charge_Wh_per_day", Dimension.EnergyPerDay, KnownUnits.WattHourPerDay),
            args => args[0] * args[1]));

        registry.Register(new Formula(
            "energy.recharge_h",
            [
                Port("capacity", Dimension.Charge, KnownUnits.AmpereHour),
                Port("DoD", Dimension.None, KnownUnits.Dimensionless),
                Port("I_charge", Dimension.Current, KnownUnits.Ampere),
                Port("charge_efficiency", Dimension.None, KnownUnits.Dimensionless),
            ],
            Port("recharge_h", Dimension.Time, KnownUnits.Hour),
            args => args[0] * args[1] / args[2] / args[3]));
    }

    private static FormulaPort Port(string name, Dimension dimension, Unit unit) => new(name, dimension, unit);
}
