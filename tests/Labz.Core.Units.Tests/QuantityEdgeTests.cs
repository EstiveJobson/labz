using FluentAssertions;
using Labz.Core.Units;

namespace Labz.Core.Units.Tests;

public class QuantityEdgeTests
{
    [Theory]
    [InlineData("1e400 Ω")]
    [InlineData("1e309 Ω")]
    [InlineData("1e-400 Ω")]
    [InlineData("999e308 Ω")]
    [InlineData("1e99999999999 Ω")]
    public void Parser_rejects_non_finite_values(string text)
    {
        var act = () => QuantityParser.Parse(text, Dimension.Resistance);
        act.Should().Throw<QuantityFormatException>().WithMessage("*finito*");
    }

    [Fact]
    public void Subnormal_exponent_is_accepted()
    {
        var quantity = QuantityParser.Parse("1e-320 Ω", Dimension.Resistance);
        quantity.Value.Should().BeGreaterThan(0);
        double.IsFinite(quantity.Value).Should().BeTrue();
        (Math.Abs(quantity.Value - 1e-320) / 1e-320).Should().BeLessThan(1e-6);
    }

    [Fact]
    public void Formatter_does_not_print_nan_for_edge_values()
    {
        QuantityFormatter.Format(new Quantity(double.PositiveInfinity, Dimension.Resistance))
            .Should().NotContain("NaN").And.Contain("infinito");
        QuantityFormatter.Format(new Quantity(double.NaN, Dimension.Resistance))
            .Should().NotContain("NaN").And.NotBe("0 Ω").And.Contain("indefinido");
        var tiny = QuantityFormatter.Format(new Quantity(5e-324, Dimension.Resistance));
        tiny.Should().NotContain("NaN");
        tiny.Should().Contain("E");
        var parsedTiny = QuantityParser.Parse(tiny, Dimension.Resistance);
        (Math.Abs(parsedTiny.Value - 5e-324) / 5e-324).Should().BeLessThan(5e-4);

        var huge = QuantityFormatter.Format(new Quantity(double.MaxValue, Dimension.Resistance));
        huge.Should().NotContain("NaN");
        var parsedHuge = QuantityParser.Parse(huge, Dimension.Resistance);
        parsedHuge.Value.Should().BeApproximately(double.MaxValue, double.MaxValue * 1e-4);
    }

    [Theory]
    [InlineData("4,9407E-324 Ω")]
    [InlineData("1,7977E+308 Ω")]
    public void Scientific_extremes_parse(string text)
    {
        var quantity = QuantityParser.Parse(text, Dimension.Resistance);
        double.IsFinite(quantity.Value).Should().BeTrue();
        quantity.Value.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("0,125 A", 0.125)]
    [InlineData("0.125 A", 0.125)]
    [InlineData("1,250 V", 1.250)]
    [InlineData("0,047 µF", 0.047e-6)]
    [InlineData("4,700 µF", 4.7e-6)]
    [InlineData("2,200 kΩ", 2200)]
    [InlineData("12,345 V", 12.345)]
    [InlineData("0,001", 0.001)]
    [InlineData("1.000e3", 1000)]
    public void Comma_is_decimal_and_ordinary_dots_are_decimal(string text, double expected)
    {
        var dimension = text.Contains('A') ? Dimension.Current
            : text.Contains('V') ? Dimension.Voltage
            : text.Contains('F') ? Dimension.Capacitance
            : Dimension.Resistance;
        QuantityParser.Parse(text, dimension).Value.Should().BeApproximately(expected, Math.Abs(expected) * 1e-9 + 1e-15);
    }

    [Theory]
    [InlineData("1.000 Ω")]
    [InlineData("12.500")]
    [InlineData("1.234.567")]
    [InlineData("1.000,5 Ω")]
    public void Ambiguous_dot_groups_and_repeated_separators_are_rejected(string text)
    {
        var act = () => QuantityParser.Parse(text, Dimension.Resistance);
        act.Should().Throw<QuantityFormatException>()
            .WithMessage("*vírgula*separador de milhar*");
    }

    [Theory]
    [InlineData(1500, "1,5 kΩ")]
    [InlineData(4700, "4,7 kΩ")]
    public void Formatter_uses_a_comma_and_strips_trailing_zeros(double ohms, string text)
    {
        QuantityFormatter.Format(Quantity.Of(ohms, KnownUnits.Ohm)).Should().Be(text);
    }

    [Theory]
    [InlineData(1500)]
    [InlineData(4700)]
    [InlineData(0.0198675)]
    [InlineData(1e-7)]
    [InlineData(12.345)]
    [InlineData(0.125)]
    public void Format_then_parse_round_trips(double value)
    {
        var quantity = new Quantity(value, Dimension.Resistance);
        var parsed = QuantityParser.Parse(QuantityFormatter.Format(quantity), Dimension.Resistance);
        var scale = Math.Max(Math.Abs(quantity.Value), 1e-300);
        (Math.Abs(parsed.Value - quantity.Value) / scale).Should().BeLessThan(5e-4);
    }

    [Fact]
    public void S_is_siemens_and_h_is_an_hour_in_a_time_field()
    {
        var siemens = QuantityParser.Parse("1 S", Dimension.Conductance);
        siemens.Dimension.Should().Be(Dimension.Conductance);
        siemens.Value.Should().BeApproximately(1, 1e-12);
        var asTime = () => QuantityParser.Parse("1 S", Dimension.Time);
        asTime.Should().Throw<UnitMismatchException>();

        var hours = QuantityParser.Parse("2 h", Dimension.Time);
        hours.In(KnownUnits.Hour).Should().BeApproximately(2, 1e-12);
        QuantityParser.Parse("2 h", Dimension.Inductance).In(KnownUnits.Henry).Should().BeApproximately(2, 1e-12);
    }

    [Fact]
    public void Femto_prefix_rkm_notation_and_unicode_minus_parse()
    {
        QuantityParser.Parse("10 fF", Dimension.Capacitance).Value.Should().BeApproximately(10e-15, 1e-24);
        QuantityParser.Parse("4K7", Dimension.Resistance).Value.Should().BeApproximately(4700, 1e-9);
        QuantityParser.Parse("4R7", Dimension.Resistance).Value.Should().BeApproximately(4.7, 1e-12);
        QuantityParser.Parse("\u22125 V", Dimension.Voltage).Value.Should().BeApproximately(-5, 1e-12);
        QuantityParser.Parse("4k7", Dimension.Resistance).Value.Should().BeApproximately(4700, 1e-6);
        QuantityParser.Parse("2R5", Dimension.Resistance).Value.Should().BeApproximately(2.5, 1e-12);
        QuantityParser.Parse("4k7", Dimension.Capacitance).Value.Should().BeApproximately(4700, 1e-6);
        QuantityParser.Parse("2R5", Dimension.Inductance).Value.Should().BeApproximately(2.5, 1e-12);
        var notVoltage = () => QuantityParser.Parse("4k7 V", Dimension.Voltage);
        notVoltage.Should().Throw<QuantityFormatException>();
        var notCurrent = () => QuantityParser.Parse("2R5 A", Dimension.Current);
        notCurrent.Should().Throw<QuantityFormatException>();
    }

    [Fact]
    public void Time_units_and_daily_energy_parse_without_prefixed_hours()
    {
        QuantityParser.Parse("2 min", Dimension.Time).In(KnownUnits.Minute).Should().BeApproximately(2, 1e-12);
        QuantityParser.Parse("3 d", Dimension.Time).In(KnownUnits.Day).Should().BeApproximately(3, 1e-12);
        QuantityParser.Parse("14 Wh/day", Dimension.EnergyPerDay).In(KnownUnits.WattHourPerDay).Should().BeApproximately(14, 1e-9);
        var milliHour = () => QuantityParser.Parse("2 mh", Dimension.Time);
        milliHour.Should().Throw<QuantityFormatException>().WithMessage("*hora*");
        var kiloHour = () => QuantityParser.Parse("2 kh", Dimension.Time);
        kiloHour.Should().Throw<QuantityFormatException>().WithMessage("*hora*");
    }

    [Fact]
    public void Programmer_messages_come_from_resources()
    {
        var divide = () => Quantity.Of(1, KnownUnits.Volt) / 0;
        divide.Should().Throw<DivideByZeroException>().WithMessage("*dividir*zero*");
        var digits = () => QuantityFormatter.Format(Quantity.Of(1, KnownUnits.Ohm), 0);
        digits.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*algarismos*");
        var missing = () => KnownUnits.Canonical(new Dimension(1, 0, 0, 0, 0, 0, 0));
        missing.Should().Throw<ArgumentException>().WithMessage("*unidade nomeada*");
    }

    [Fact]
    public void Dimensionless_description_comes_from_resources()
    {
        KnownUnits.Describe(Dimension.None).Should().Be("adimensional");
    }
}
