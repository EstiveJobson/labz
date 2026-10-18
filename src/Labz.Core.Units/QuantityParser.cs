using System.Globalization;
using System.Text;

namespace Labz.Core.Units;

/// <summary>
/// Parses pt-BR and plain quantity strings ("4,7 kΩ", "4.7k", "100 nF", "4K7") into an SI <see cref="Quantity"/>.
/// Prefixes are f, p, n, µ (also "u"), m, k, M, G and T. Tera is accepted because sane ranges go up to 1 TΩ.
/// </summary>
/// <remarks>
/// A single separator followed by exactly three digits is rejected ("1.000", "1,000").
/// That spelling is ambiguous: the group can be a thousands separator or a decimal fraction.
/// One or two digits ("4,7", "1.00"), four or more ("1.0000"), a bare integer ("1000"),
/// or both separators ("1.000,5") are accepted. RKM codes use the letter as the decimal point ("4K7" = 4.7 kΩ).
/// The Unicode minus U+2212 is accepted wherever an ASCII hyphen-minus is.
/// </remarks>
public static class QuantityParser
{
    private const int MaxAbsoluteExponent = 308;

    public static Quantity Parse(string text, Dimension expected)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new QuantityFormatException(Strings.FormatInvalid(text ?? string.Empty));
        }

        var source = text.Trim();
        if (TryParseRkm(source, expected, out var rkm))
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
        var value = number * prefix * unit.SiPerUnit;
        RejectUnlessFinite(text, value);
        return new Quantity(value, unit.Dimension);
    }

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

        if (index >= source.Length || source[index] is not ('R' or 'r' or 'K' or 'k' or 'M'))
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

        // "4k" with nothing after it is ordinary prefix notation, not an RKM code.
        if (fractionDigits == 0 && multiplierChar is 'k' or 'M')
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

        var multiplier = multiplierChar switch
        {
            'R' or 'r' => 1d,
            'K' or 'k' => 1e3,
            _ => 1e6,
        };
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

        while (index < source.Length && char.IsDigit(source[index]))
        {
            index++;
        }

        var separatorCount = 0;
        var digitsAfterSeparator = 0;
        var lastSeparatorDigits = 0;
        if (index < source.Length && source[index] is ',' or '.')
        {
            while (index < source.Length && source[index] is ',' or '.')
            {
                separatorCount++;
                index++;
                var digitStart = index;
                while (index < source.Length && char.IsDigit(source[index]))
                {
                    index++;
                }

                lastSeparatorDigits = index - digitStart;
                digitsAfterSeparator += lastSeparatorDigits;
                if (index >= source.Length || source[index] is not (',' or '.'))
                {
                    break;
                }
            }
        }

        int? exponent = null;
        if (index < source.Length && source[index] is 'e' or 'E')
        {
            var exponentMark = index;
            index++;
            var exponentSign = 1;
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
                exponent = exponentSign * int.Parse(source[exponentStart..index], CultureInfo.InvariantCulture);
            }
        }

        if (index == start)
        {
            return false;
        }

        if (separatorCount == 1 && lastSeparatorDigits == 3)
        {
            throw new QuantityFormatException(Strings.AmbiguousSeparator(source[start..index]));
        }

        if (exponent is int power && Math.Abs(power) > MaxAbsoluteExponent)
        {
            throw new QuantityFormatException(Strings.NonFinite(source[start..index]));
        }

        var token = source[start..index];
        var mantissa = exponent is null ? token : token[..token.IndexOfAny(['e', 'E'])];
        if (!TryParseMantissa(mantissa, out var parsed))
        {
            return false;
        }

        if (exponent is int applied)
        {
            parsed *= Math.Pow(10, applied);
        }

        number = parsed;
        return true;
    }

    private static bool TryParseMantissa(string mantissa, out double number)
    {
        var separators = 0;
        foreach (var character in mantissa)
        {
            if (character is ',' or '.')
            {
                separators++;
            }
        }

        if (separators <= 1)
        {
            return double.TryParse(mantissa.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        var groups = new List<string>();
        var current = new StringBuilder();
        char? separator = null;
        var mixed = false;
        foreach (var character in mantissa)
        {
            if (character is ',' or '.')
            {
                if (separator is null)
                {
                    separator = character;
                }
                else if (separator != character)
                {
                    mixed = true;
                }

                groups.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        groups.Add(current.ToString());
        if (groups.Any(group => group.Length == 0))
        {
            number = 0;
            return false;
        }

        if (!mixed && groups.Skip(1).All(group => group.Length == 3))
        {
            return double.TryParse(string.Concat(groups), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        var fraction = groups[^1];
        var whole = string.Concat(groups.Take(groups.Count - 1));
        return double.TryParse(whole + "." + fraction, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

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
