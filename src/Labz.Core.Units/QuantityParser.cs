using System.Globalization;

namespace Labz.Core.Units;

/// <summary>
/// Parses pt-BR and plain quantity strings ("4,7 kΩ", "4.7k", "100 nF") into an SI <see cref="Quantity"/>.
/// Prefixes are p, n, µ (also "u"), m, k, M, G and T. Tera is accepted because sane ranges go up to 1 TΩ.
/// </summary>
public static class QuantityParser
{
    public static Quantity Parse(string text, Dimension expected)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new QuantityFormatException(Strings.FormatInvalid(text ?? string.Empty));
        }

        var source = text.Trim();
        var index = 0;
        var sign = 1d;
        if (source[index] == '+')
        {
            index++;
        }
        else if (source[index] == '-')
        {
            sign = -1;
            index++;
        }

        if (!TryReadNumber(source, ref index, out var number))
        {
            throw new QuantityFormatException(Strings.FormatInvalid(text));
        }

        number *= sign;
        SkipSpace(source, ref index);

        var prefix = 1d;
        if (index < source.Length && TryPrefix(source[index], out var parsedPrefix))
        {
            var rest = source[(index + 1)..].TrimStart();
            if (rest.Length == 0 || KnownUnits.TryParseSymbol(rest, out _))
            {
                prefix = parsedPrefix;
                index++;
                SkipSpace(source, ref index);
            }
        }

        Unit unit;
        if (index >= source.Length)
        {
            unit = KnownUnits.Canonical(expected);
        }
        else
        {
            var token = source[index..].Trim();
            if (!KnownUnits.TryParseSymbol(token, out unit!))
            {
                throw new QuantityFormatException(Strings.FormatInvalid(text));
            }

            if (unit.Dimension != expected)
            {
                throw new UnitMismatchException(token, expected);
            }
        }

        return new Quantity(number * prefix * unit.SiPerUnit, unit.Dimension);
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

        if (index < source.Length && source[index] is ',' or '.')
        {
            index++;
            while (index < source.Length && char.IsDigit(source[index]))
            {
                index++;
            }
        }

        if (index < source.Length && source[index] is 'e' or 'E')
        {
            var exponentMark = index;
            index++;
            if (index < source.Length && source[index] is '+' or '-')
            {
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
        }

        if (index == start)
        {
            return false;
        }

        var token = source[start..index].Replace(',', '.');
        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
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
