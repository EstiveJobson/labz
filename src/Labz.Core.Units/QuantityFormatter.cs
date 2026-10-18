using System.Globalization;

namespace Labz.Core.Units;

/// <summary>
/// Formats a quantity with an engineering prefix and a pt-BR decimal comma. Default is four significant digits.
/// The text never uses a single separator followed by exactly three digits, because that form is ambiguous to the parser.
/// Non-finite values and subnormals are written as words or scientific notation, never as "NaN".
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
            return FormatBare(quantity.Value, significantDigits);
        }

        return FormatScaled(unit.FromSi(quantity.Value), unit.Symbol, significantDigits);
    }

    private static string FormatBare(double value, int significantDigits)
    {
        if (double.IsNaN(value))
        {
            return Strings.NotANumber();
        }

        if (double.IsPositiveInfinity(value))
        {
            return Strings.Infinity();
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-" + Strings.Infinity();
        }

        return value.ToString("G" + significantDigits, CultureInfo.InvariantCulture).Replace('.', ',');
    }

    private static string FormatScaled(double valueInUnit, string symbol, int significantDigits)
    {
        if (double.IsNaN(valueInUnit))
        {
            return Join(Strings.NotANumber(), string.Empty, symbol);
        }

        if (double.IsInfinity(valueInUnit))
        {
            var word = valueInUnit < 0 ? "-" + Strings.Infinity() : Strings.Infinity();
            return Join(word, string.Empty, symbol);
        }

        if (valueInUnit == 0)
        {
            return Join("0", string.Empty, symbol);
        }

        var sign = valueInUnit < 0 ? "-" : string.Empty;
        var absolute = Math.Abs(valueInUnit);
        if (absolute < 1e-280 || absolute > 1e280 || !TryExponent(absolute, out _))
        {
            return Join(sign + Scientific(absolute, significantDigits), string.Empty, symbol);
        }

        var exponent = ExponentOf(absolute);
        var engineering = (int)Math.Floor(exponent / 3.0) * 3;
        if (engineering > 12)
        {
            engineering = 12;
        }
        else if (engineering < -15)
        {
            engineering = -15;
        }

        double rounded = 0;
        for (var guard = 0; guard < 6; guard++)
        {
            var scaled = absolute / Math.Pow(10, engineering);
            if (!double.IsFinite(scaled) || scaled == 0)
            {
                return Join(sign + Scientific(absolute, significantDigits), string.Empty, symbol);
            }

            rounded = RoundSignificant(scaled, significantDigits);
            if (!double.IsFinite(rounded))
            {
                return Join(sign + Scientific(absolute, significantDigits), string.Empty, symbol);
            }

            if (rounded >= 1000 && engineering < 12)
            {
                engineering += 3;
                continue;
            }

            if (rounded is > 0 and < 1 && engineering > -15)
            {
                engineering -= 3;
                continue;
            }

            var decimals = DecimalPlaces(rounded, significantDigits);
            if (decimals == 3)
            {
                // "1,000" is ambiguous (thousands group vs decimal). One extra trailing zero keeps it a decimal.
                decimals = 4;
            }

            var digits = rounded.ToString("F" + decimals, CultureInfo.InvariantCulture).Replace('.', ',');
            if (digits.Contains("NaN", StringComparison.OrdinalIgnoreCase))
            {
                return Join(sign + Scientific(absolute, significantDigits), string.Empty, symbol);
            }

            return Join(sign + digits, Prefix(engineering), symbol);
        }

        return Join(sign + Scientific(absolute, significantDigits), string.Empty, symbol);
    }

    private static string Scientific(double absolute, int significantDigits)
    {
        var text = absolute.ToString("E" + significantDigits, CultureInfo.InvariantCulture);
        return text.Replace('.', ',');
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
        if (rounded <= 0 || !double.IsFinite(rounded))
        {
            return 0;
        }

        var places = significantDigits - 1 - ExponentOf(rounded);
        return places < 0 ? 0 : places;
    }

    private static double RoundSignificant(double value, int significantDigits)
    {
        if (value == 0 || !TryExponent(value, out var exponent))
        {
            return value;
        }

        var scale = Math.Pow(10, significantDigits - 1 - exponent);
        if (!double.IsFinite(scale) || scale == 0)
        {
            return value;
        }

        var scaled = value * scale;
        if (!double.IsFinite(scaled))
        {
            return value;
        }

        return Math.Round(scaled, MidpointRounding.AwayFromZero) / scale;
    }

    private static int ExponentOf(double value)
    {
        if (!TryExponent(value, out var exponent))
        {
            return 0;
        }

        var boundary = Math.Pow(10, exponent);
        if (!double.IsFinite(boundary) || boundary == 0)
        {
            return exponent;
        }

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

    private static bool TryExponent(double value, out int exponent)
    {
        exponent = 0;
        if (!(value > 0) || double.IsInfinity(value))
        {
            return false;
        }

        var log = Math.Log10(value);
        if (!double.IsFinite(log))
        {
            return false;
        }

        exponent = (int)Math.Floor(log);
        return true;
    }

    private static string Prefix(int engineeringExponent) => engineeringExponent switch
    {
        -15 => "f",
        -12 => "p",
        -9 => "n",
        -6 => "\u00B5",
        -3 => "m",
        0 => string.Empty,
        3 => "k",
        6 => "M",
        9 => "G",
        12 => "T",
        _ => "e" + engineeringExponent.ToString(CultureInfo.InvariantCulture),
    };
}
