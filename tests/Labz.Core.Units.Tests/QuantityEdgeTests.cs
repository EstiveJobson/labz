using FluentAssertions;
using Labz.Core.Units;

namespace Labz.Core.Units.Tests;

public class QuantityEdgeTests
{
    [Theory]
    [InlineData("1e400 Ω")]
    [InlineData("1e309 Ω")]
    [InlineData("1e-400 Ω")]
    public void Parser_rejects_non_finite_values(string text)
    {
        var act = () => QuantityParser.Parse(text, Dimension.Resistance);
        act.Should().Throw<QuantityFormatException>().WithMessage("*finito*");
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
        tiny.Should().NotBe("0 Ω");
    }

    [Theory]
    [InlineData("1.000 Ω")]
    [InlineData("1,000 Ω")]
    [InlineData("12.000")]
    public void Parser_rejects_an_ambiguous_group_of_three_digits(string text)
    {
        var act = () => QuantityParser.Parse(text, Dimension.Resistance);
        act.Should().Throw<QuantityFormatException>().WithMessage("*ambíguo*");
    }

    [Theory]
    [InlineData("1.00 Ω", 1)]
    [InlineData("1,00 Ω", 1)]
    [InlineData("1.0000 Ω", 1)]
    [InlineData("1000 Ω", 1000)]
    [InlineData("1.000,5 Ω", 1000.5)]
    public void Parser_accepts_unambiguous_separators(string text, double ohms)
    {
        QuantityParser.Parse(text, Dimension.Resistance).Value.Should().BeApproximately(ohms, 1e-9);
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
    }

    [Fact]
    public void Dimensionless_description_comes_from_resources()
    {
        KnownUnits.Describe(Dimension.None).Should().Be("adimensional");
    }
}
