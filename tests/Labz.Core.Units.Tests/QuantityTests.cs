using FluentAssertions;
using Labz.Core.Units;

namespace Labz.Core.Units.Tests;

public class QuantityTests
{
    [Theory]
    [InlineData("4,7 kΩ", 4700)]
    [InlineData("4.7k", 4700)]
    [InlineData("1 MΩ", 1e6)]
    [InlineData("1 TΩ", 1e12)]
    [InlineData("1 mΩ", 0.001)]
    [InlineData("10 Ohm", 10)]
    [InlineData("10 ohm", 10)]
    [InlineData("2,2 kOhm", 2200)]
    public void Core1_parses_resistance(string text, double ohms)
    {
        var quantity = QuantityParser.Parse(text, Dimension.Resistance);
        quantity.Dimension.Should().Be(Dimension.Resistance);
        quantity.Value.Should().BeApproximately(ohms, 1e-9);
    }

    [Fact]
    public void Core1_parses_capacitance_current_examples()
    {
        QuantityParser.Parse("100 nF", Dimension.Capacitance).Value.Should().BeApproximately(1e-7, 1e-18);
        QuantityParser.Parse("20 mA", Dimension.Current).Value.Should().BeApproximately(0.02, 1e-12);
        QuantityParser.Parse("1 uF", Dimension.Capacitance).Value.Should().BeApproximately(1e-6, 1e-15);
        QuantityParser.Parse("1 µF", Dimension.Capacitance).Value.Should().BeApproximately(1e-6, 1e-15);
        QuantityParser.Parse("1 \u03BCF", Dimension.Capacitance).Value.Should().BeApproximately(1e-6, 1e-15);
    }

    [Fact]
    public void Core2_addition_of_different_dimensions_throws_in_portuguese()
    {
        var volts = Quantity.Of(12, KnownUnits.Volt);
        var amperes = Quantity.Of(1, KnownUnits.Ampere);

        var add = () => volts + amperes;
        var subtract = () => volts - amperes;

        add.Should().Throw<DimensionMismatchException>().WithMessage("*dimensões*");
        subtract.Should().Throw<DimensionMismatchException>().WithMessage("*dimensões*");
    }

    [Fact]
    public void Core2_division_and_multiplication_combine_dimensions()
    {
        var volts = Quantity.Of(10, KnownUnits.Volt);
        var ohms = Quantity.Of(5, KnownUnits.Ohm);
        var current = volts / ohms;
        current.Dimension.Should().Be(Dimension.Current);
        current.Value.Should().BeApproximately(2, 1e-12);

        var power = volts * Quantity.Of(0.5, KnownUnits.Ampere);
        power.Dimension.Should().Be(Dimension.Power);
        power.Value.Should().BeApproximately(5, 1e-12);
    }

    [Fact]
    public void Core3_formats_current_with_prefix_and_decimal_comma()
    {
        var text = QuantityFormatter.Format(new Quantity(0.0198675, Dimension.Current));
        text.Should().Be("19,87 mA");
    }

    [Fact]
    public void Formatter_significant_digits_are_configurable()
    {
        var current = Quantity.Of(0.02, KnownUnits.Ampere);
        QuantityFormatter.Format(current, 2).Should().Be("20 mA");
        QuantityFormatter.Format(current, 4).Should().Be("20,00 mA");
    }

    [Theory]
    [InlineData(4700, "Ω")]
    [InlineData(0.02, "A")]
    [InlineData(1e-7, "F")]
    [InlineData(1e6, "Ω")]
    [InlineData(1e12, "Ω")]
    [InlineData(3.3e-12, "F")]
    [InlineData(0.0198675, "A")]
    [InlineData(18, "V")]
    [InlineData(1, "Wh")]
    [InlineData(3.5, "Ah")]
    public void Round_trip_parse_of_format_is_within_four_significant_digits(double valueInUnit, string symbol)
    {
        KnownUnits.TryParseSymbol(symbol, out var unit).Should().BeTrue();
        var quantity = Quantity.Of(valueInUnit, unit!);
        var text = QuantityFormatter.Format(quantity);
        var parsed = QuantityParser.Parse(text, unit!.Dimension);
        var scale = Math.Max(Math.Abs(quantity.Value), 1e-15);
        Math.Abs(parsed.Value - quantity.Value).Should().BeLessThanOrEqualTo(scale * 5e-4);
    }

    [Fact]
    public void Parser_rejects_a_unit_from_another_dimension()
    {
        var act = () => QuantityParser.Parse("4,7 kΩ", Dimension.Current);
        act.Should().Throw<UnitMismatchException>().WithMessage("*não corresponde*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("10 volts")]
    [InlineData("kΩ")]
    public void Parser_rejects_invalid_text(string text)
    {
        var act = () => QuantityParser.Parse(text, Dimension.Resistance);
        act.Should().Throw<QuantityFormatException>().WithMessage("*Não foi possível*");
    }

    [Fact]
    public void Same_dimension_addition_uses_si_values()
    {
        var sum = QuantityParser.Parse("1 kΩ", Dimension.Resistance) + QuantityParser.Parse("2,2 kΩ", Dimension.Resistance);
        sum.In(KnownUnits.Ohm).Should().BeApproximately(3200, 1e-9);
    }
}
