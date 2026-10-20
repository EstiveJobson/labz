using System.Globalization;

namespace Labz.Core.Units;

/// <summary>
/// Parses pt-BR and plain quantity strings ("4,7 kΩ", "4.7k", "100 nF", "4K7", "4n7") into an SI <see cref="Quantity"/>.
/// Prefixes are f, p, n, µ (also "u"), m, k, M, G and T. Tera is accepted because sane ranges go up to 1 TΩ.
/// </summary>
/// <remarks>
/// A comma is always the decimal separator (pt-BR): "0,125" is 0.125, "1,250" is 1.250, and "1,000 Ω" is 1 Ω.
/// A dot is also a decimal separator, except one ambiguous pattern: a non-zero integer part of
/// 1–3 digits, a dot, exactly three digits, and no exponent ("1.000", "12.500"). That pattern is
/// rejected. Use a comma for the decimal and do not write a thousands separator.
/// More than one separator, or both separators in the same number, is rejected ("1,000,000", "1,5,2", "1,000.5").
/// "0.125" and "1.000e3" stay decimal because the integer part is zero or an exponent is present.
/// RKM codes ("4K7", "2R5") are accepted only for resistance, capacitance and inductance.
/// Capacitance also accepts nano and micro RKM codes ("4n7" is 4.7 nF, "4u7" is 4.7 µF).
/// The Unicode minus U+2212 is accepted wherever an ASCII hyphen-minus is.
/// Hour, minute and day do not take a prefix.
/// </remarks>
public static class QuantityParser
{
    private const int MaxPositiveExponent = 308;

    private const int MinExponent = -324;

    public static Quantity Parse(string text, Dimension expected)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new QuantityFormatException(Strings.FormatInvalid(text ?? string.Empty));
        }

        var source = text.Trim();
        if (IsRkmDimension(expected) && TryParseRkm(source, expected, out var rkm))
        {
            return rkm;
        }

        var index = 0;
        var sign = ReadSign(source, ref index);

        if (!TryReadNumber(source, ref index, out var number))
        {
            throw new QuantityFormatException(Strings.FormatInvalid(text));
        }

        number *= sign;
        RejectUnlessFinite(text, number);
        SkipSpace(source, ref index);

        var prefix = 1d;
        if (index < source.Length && TryPrefix(source[index], out var parsedPrefix))
        {
            var rest = source[(index + 1)..].TrimStart();
            if (rest.Length == 0 || LooksLikeUnit(rest, expected))
            {
                prefix = parsedPrefix;
                index++;
                SkipSpace(source, ref index);
            }
        }

        var unit = ResolveUnit(source, index, expected, text);
        if (prefix != 1 && UnprefixedUnitName(unit.Symbol) is string blocked)
        {
            throw new QuantityFormatException(Strings.PrefixedUnit(blocked));
        }

        var value = number * prefix * unit.SiPerUnit;
        RejectUnlessFinite(text, value);
        return new Quantity(value, unit.Dimension);
    }

    private static bool IsRkmDimension(Dimension dimension) =>
        dimension == Dimension.Resistance
        || dimension == Dimension.Capacitance
        || dimension == Dimension.Inductance;

    private static bool TryParseRkm(string source, Dimension expected, out Quantity quantity)
    {
        quantity = default;
        var index = 0;
        var sign = 1d;
        if (index < source.Length && source[index] is '+' or '-' or '\u2212')
        {
            sign = source[index] == '+' ? 1 : -1;
            index++;
        }

        var intStart = index;
        while (index < source.Length && char.IsDigit(source[index]))
        {
            index++;
        }

        if (index >= source.Length || !TryRkmMultiplier(source[index], expected, out var multiplier))
        {
            return false;
        }

        var multiplierChar = source[index];
        var integerDigits = index - intStart;
        index++;
        var fractionStart = index;
        while (index < source.Length && char.IsDigit(source[index]))
        {
            index++;
        }

        var fractionDigits = index - fractionStart;
        if (integerDigits == 0 && fractionDigits == 0)
        {
            return false;
        }

        // "4k" or "4n" with nothing after it is ordinary prefix notation, not an RKM code.
        // "4K" stays an RKM code: K is not a prefix character.
        if (fractionDigits == 0 && multiplierChar is 'k' or 'M' or 'n' or 'u' or 'U' or '\u00B5' or '\u03BC')
        {
            return false;
        }

        var rest = source[index..].Trim();
        if (rest.Length > 0 && !LooksLikeUnit(rest, expected))
        {
            return false;
        }

        var intText = source.Substring(intStart, integerDigits);
        var integer = intText.Length == 0 ? 0d : double.Parse(intText, CultureInfo.InvariantCulture);
        var fraction = 0d;
        if (fractionDigits > 0)
        {
            fraction = double.Parse(source.Substring(fractionStart, fractionDigits), CultureInfo.InvariantCulture)
                / Math.Pow(10, fractionDigits);
        }

        var unit = rest.Length == 0 ? KnownUnits.Canonical(expected) : ResolveToken(rest, expected, source);
        var value = sign * (integer + fraction) * multiplier * unit.SiPerUnit;
        RejectUnlessFinite(source, value);
        quantity = new Quantity(value, unit.Dimension);
        return true;
    }

    private static Unit ResolveUnit(string source, int index, Dimension expected, string original)
    {
        if (index >= source.Length)
        {
            return KnownUnits.Canonical(expected);
        }

        return ResolveToken(source[index..].Trim(), expected, original);
    }

    private static Unit ResolveToken(string token, Dimension expected, string original)
    {
        if ((token == "h") && expected == Dimension.Time)
        {
            return KnownUnits.Hour;
        }

        if (!KnownUnits.TryParseSymbol(token, out var unit))
        {
            throw new QuantityFormatException(Strings.FormatInvalid(original));
        }

        if (unit.Dimension != expected)
        {
            throw new UnitMismatchException(token, expected);
        }

        return unit;
    }

    private static bool LooksLikeUnit(string token, Dimension expected)
    {
        if (token == "h" && expected == Dimension.Time)
        {
            return true;
        }

        return KnownUnits.TryParseSymbol(token, out _);
    }

    private static bool TryReadNumber(string source, ref int index, out double number)
    {
        number = 0;
        var start = index;
        if (index >= source.Length || !char.IsDigit(source[index]))
        {
            return false;
        }

        var integerStart = index;
        while (index < source.Length && char.IsDigit(source[index]))
        {
            index++;
        }

        var integerPart = source[integerStart..index];
        char? separator = null;
        var sawComma = false;
        var sawDot = false;
        var separatorCount = 0;
        var fraction = string.Empty;
        if (index < source.Length && source[index] is ',' or '.')
        {
            separator = source[index];
            sawComma = separator == ',';
            sawDot = separator == '.';
            separatorCount = 1;
            index++;
            var fractionStart = index;
            while (index < source.Length && char.IsDigit(source[index]))
            {
                index++;
            }

            fraction = source[fractionStart..index];
            while (index < source.Length && source[index] is ',' or '.')
            {
                separatorCount++;
                if (source[index] == ',')
                {
                    sawComma = true;
                }
                else
                {
                    sawDot = true;
                }

                index++;
                while (index < source.Length && char.IsDigit(source[index]))
                {
                    index++;
                }
            }
        }

        long? exponent = null;
        var hadExponent = false;
        if (index < source.Length && source[index] is 'e' or 'E')
        {
            var exponentMark = index;
            index++;
            var exponentSign = 1L;
            if (index < source.Length && source[index] is '+' or '-' or '\u2212')
            {
                exponentSign = source[index] == '+' ? 1 : -1;
                index++;
            }

            var exponentStart = index;
            while (index < source.Length && char.IsDigit(source[index]))
            {
                index++;
            }

            if (index == exponentStart)
            {
                index = exponentMark;
            }
            else
            {
                hadExponent = true;
                var exponentText = source[exponentStart..index];
                if (!long.TryParse(exponentText, NumberStyles.None, CultureInfo.InvariantCulture, out var magnitude))
                {
                    throw new QuantityFormatException(Strings.NonFinite(source[start..index]));
                }

                exponent = exponentSign * magnitude;
            }
        }

        if (separatorCount > 1 || (sawComma && sawDot))
        {
            throw new QuantityFormatException(Strings.AmbiguousSeparator(source[start..index]));
        }

        if (separator == '.' && !hadExponent && fraction.Length == 3 && IsAmbiguousInteger(integerPart))
        {
            throw new QuantityFormatException(Strings.AmbiguousSeparator(source[start..index]));
        }

        if (exponent is long power && (power > MaxPositiveExponent || power < MinExponent))
        {
            throw new QuantityFormatException(Strings.NonFinite(source[start..index]));
        }

        var mantissa = fraction.Length == 0 ? integerPart : integerPart + "." + fraction;
        var literal = exponent is long applied ? mantissa + "E" + applied.ToString(CultureInfo.InvariantCulture) : mantissa;
        if (!double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return false;
        }

        if (!double.IsFinite(number) && exponent is long overflowExponent && overflowExponent <= MaxPositiveExponent)
        {
            var reduced = mantissa + "E" + (overflowExponent - MaxPositiveExponent).ToString(CultureInfo.InvariantCulture);
            // 1.7977e308 overflows double.Parse but is only a few ulps above MaxValue, so it is clamped.
            // 1.8e308 is outside that band and must be rejected as non-finite.
            if (double.TryParse(reduced, NumberStyles.Float, CultureInfo.InvariantCulture, out var scaled)
                && Math.Abs(scaled) < 1.8)
            {
                number = Math.CopySign(double.MaxValue, scaled == 0 ? 1 : scaled);
            }
        }

        return true;
    }

    /// <summary>True for "1", "12" and "123". "0", "01" and "1234" are ordinary decimals when followed by a dot.</summary>
    private static bool IsAmbiguousInteger(string integerPart) =>
        integerPart.Length is >= 1 and <= 3
        && integerPart[0] != '0'
        && integerPart.All(char.IsDigit);

    private static double ReadSign(string source, ref int index)
    {
        if (source[index] == '+')
        {
            index++;
            return 1;
        }

        if (source[index] is '-' or '\u2212')
        {
            index++;
            return -1;
        }

        return 1;
    }

    private static void RejectUnlessFinite(string text, double value)
    {
        if (!double.IsFinite(value))
        {
            throw new QuantityFormatException(Strings.NonFinite(text));
        }
    }

    private static void SkipSpace(string source, ref int index)
    {
        while (index < source.Length && char.IsWhiteSpace(source[index]))
        {
            index++;
        }
    }

    private static string? UnprefixedUnitName(string symbol) => symbol switch
    {
        "h" => "hora",
        "min" => "minuto",
        "d" => "dia",
        _ => null,
    };

    private static bool TryRkmMultiplier(char symbol, Dimension dimension, out double multiplier)
    {
        switch (symbol)
        {
            case 'R' or 'r':
                multiplier = 1d;
                return true;
            case 'K' or 'k':
                multiplier = 1e3;
                return true;
            case 'M':
                multiplier = 1e6;
                return true;
            case 'n' when dimension == Dimension.Capacitance:
                multiplier = 1e-9;
                return true;
            case 'u' or 'U' or '\u00B5' or '\u03BC' when dimension == Dimension.Capacitance:
                multiplier = 1e-6;
                return true;
            default:
                multiplier = 0;
                return false;
        }
    }

    private static bool TryPrefix(char symbol, out double factor)
    {
        switch (symbol)
        {
            case 'f':
                factor = 1e-15;
                return true;
            case 'p':
                factor = 1e-12;
                return true;
            case 'n':
                factor = 1e-9;
                return true;
            case 'u':
            case 'U':
            case '\u00B5':
            case '\u03BC':
                factor = 1e-6;
                return true;
            case 'm':
                factor = 1e-3;
                return true;
            case 'k':
                factor = 1e3;
                return true;
            case 'M':
                factor = 1e6;
                return true;
            case 'G':
                factor = 1e9;
                return true;
            case 'T':
                factor = 1e12;
                return true;
            default:
                factor = 1;
                return false;
        }
    }
}
