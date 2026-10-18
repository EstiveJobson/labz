using System.Globalization;

namespace Labz.Core.Units;

/// <summary>
/// Formats a quantity with an engineering prefix and a pt-BR decimal comma. Default is four significant digits.
/// </summary>
public static class QuantityFormatter
{
    public const int DefaultSignificantDigits = 4;

    public static string Format(Quantity quantity, int significantDigits = DefaultSignificantDigits)
    {
        if (significantDigits < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(significantDigits), "Significant digits must be at least 1.");
        }

        if (!KnownUnits.TryGetCanonical(quantity.Dimension, out var unit))
        {
            return quantity.Value.ToString("G" + significantDigits, CultureInfo.InvariantCulture).Replace('.', ',');
        }

        return FormatScaled(unit.FromSi(quantity.Value), unit.Symbol, significantDigits);
    }

    private static string FormatScaled(double valueInUnit, string symbol, int significantDigits)
    {
        if (valueInUnit == 0 || double.IsNaN(valueInUnit))
        {
            return Join(valueInUnit < 0 ? "-0" : "0", string.Empty, symbol);
        }

        var sign = valueInUnit < 0 ? "-" : string.Empty;
        var absolute = Math.Abs(valueInUnit);
        var exponent = ExponentOf(absolute);
        var engineering = (int)Math.Floor(exponent / 3.0) * 3;
        if (engineering > 12)
        {
            engineering = 12;
        }
        else if (engineering < -12)
        {
            engineering = -12;
        }

        double rounded = 0;
        for (var guard = 0; guard < 4; guard++)
        {
            var scaled = absolute / Math.Pow(10, engineering);
            rounded = RoundSignificant(scaled, significantDigits);
            if (rounded >= 1000 && engineering < 12)
            {
                engineering += 3;
                continue;
            }

            if (rounded is > 0 and < 1 && engineering > -12)
            {
                engineering -= 3;
                continue;
            }

            break;
        }

        var decimals = DecimalPlaces(rounded, significantDigits);
        var digits = rounded.ToString("F" + decimals, CultureInfo.InvariantCulture).Replace('.', ',');
        return Join(sign + digits, Prefix(engineering), symbol);
    }

    private static string Join(string number, string prefix, string symbol)
    {
        if (prefix.Length == 0 && symbol.Length == 0)
        {
            return number;
        }

        return symbol.Length == 0 ? $"{number} {prefix}" : $"{number} {prefix}{symbol}";
    }

    private static int DecimalPlaces(double rounded, int significantDigits)
    {
        if (rounded <= 0)
        {
            return 0;
        }

        var places = significantDigits - 1 - ExponentOf(rounded);
        return places < 0 ? 0 : places;
    }

    private static double RoundSignificant(double value, int significantDigits)
    {
        if (value == 0)
        {
            return 0;
        }

        var scale = Math.Pow(10, significantDigits - 1 - ExponentOf(value));
        return Math.Round(value * scale, MidpointRounding.AwayFromZero) / scale;
    }

    private static int ExponentOf(double value)
    {
        var exponent = (int)Math.Floor(Math.Log10(value));
        var boundary = Math.Pow(10, exponent);
        if (value < boundary)
        {
            exponent--;
        }
        else if (value >= boundary * 10)
        {
            exponent++;
        }

        return exponent;
    }

    private static string Prefix(int engineeringExponent) => engineeringExponent switch
    {
        -12 => "p",
        -9 => "n",
        -6 => "\u00B5",
        -3 => "m",
        0 => string.Empty,
        3 => "k",
        6 => "M",
        9 => "G",
        12 => "T",
        _ => throw new InvalidOperationException($"Unsupported engineering exponent {engineeringExponent}."),
    };
}
