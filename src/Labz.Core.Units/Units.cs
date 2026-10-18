namespace Labz.Core.Units;

/// <summary>
/// Named units for the v0 set: V, A, Ω, W, F, H, s, Hz, Wh, Ah, and dimensionless.
/// Hour and day exist so energy formulas can take a time span; they are not display units.
/// </summary>
public static class KnownUnits
{
    public static Unit Volt { get; } = new("V", Dimension.Voltage, 1);

    public static Unit Ampere { get; } = new("A", Dimension.Current, 1);

    public static Unit Ohm { get; } = new("Ω", Dimension.Resistance, 1);

    public static Unit Watt { get; } = new("W", Dimension.Power, 1);

    public static Unit Farad { get; } = new("F", Dimension.Capacitance, 1);

    public static Unit Henry { get; } = new("H", Dimension.Inductance, 1);

    public static Unit Second { get; } = new("s", Dimension.Time, 1);

    public static Unit Hertz { get; } = new("Hz", Dimension.Frequency, 1);

    /// <summary>1 Wh = 3600 J.</summary>
    public static Unit WattHour { get; } = new("Wh", Dimension.Energy, 3600);

    /// <summary>1 Ah = 3600 C.</summary>
    public static Unit AmpereHour { get; } = new("Ah", Dimension.Charge, 3600);

    public static Unit Hour { get; } = new("h", Dimension.Time, 3600);

    public static Unit Day { get; } = new("d", Dimension.Time, 86400);

    public static Unit Dimensionless { get; } = new("", Dimension.None, 1);

    private static readonly Unit[] CanonicalUnits =
    [
        Volt,
        Ampere,
        Ohm,
        Watt,
        Farad,
        Henry,
        Second,
        Hertz,
        WattHour,
        AmpereHour,
        Dimensionless,
    ];

    private static readonly Dictionary<string, Unit> Symbols = BuildSymbols();

    public static bool TryGetCanonical(Dimension dimension, out Unit unit)
    {
        foreach (var candidate in CanonicalUnits)
        {
            if (candidate.Dimension == dimension)
            {
                unit = candidate;
                return true;
            }
        }

        unit = Dimensionless;
        return false;
    }

    public static Unit Canonical(Dimension dimension)
    {
        if (TryGetCanonical(dimension, out var unit))
        {
            return unit;
        }

        throw new ArgumentException($"No named v0 unit exists for dimension {dimension}.", nameof(dimension));
    }

    public static bool TryParseSymbol(string token, out Unit unit)
    {
        return Symbols.TryGetValue(token, out unit!);
    }

    public static string Describe(Dimension dimension)
    {
        return TryGetCanonical(dimension, out var unit) && unit.Symbol.Length > 0
            ? unit.Symbol
            : dimension.IsDimensionless ? "adimensional" : dimension.ToString();
    }

    private static Dictionary<string, Unit> BuildSymbols()
    {
        var symbols = new Dictionary<string, Unit>(StringComparer.Ordinal);
        void Add(string symbol, Unit unit) => symbols[symbol] = unit;

        Add("V", Volt);
        Add("v", Volt);
        Add("A", Ampere);
        Add("a", Ampere);
        Add("Ω", Ohm);
        Add("ω", Ohm);
        Add("Ohm", Ohm);
        Add("ohm", Ohm);
        Add("OHM", Ohm);
        Add("W", Watt);
        Add("w", Watt);
        Add("F", Farad);
        Add("f", Farad);
        Add("H", Henry);
        Add("h", Henry);
        Add("s", Second);
        Add("S", Second);
        Add("Hz", Hertz);
        Add("HZ", Hertz);
        Add("hz", Hertz);
        Add("Wh", WattHour);
        Add("WH", WattHour);
        Add("wh", WattHour);
        Add("Ah", AmpereHour);
        Add("AH", AmpereHour);
        Add("ah", AmpereHour);
        return symbols;
    }
}
