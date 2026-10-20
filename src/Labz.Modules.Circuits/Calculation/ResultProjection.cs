using System.Globalization;
using Labz.Core.Engine;
using Labz.Core.Units;

namespace Labz.Modules.Circuits;

public static class ResultText
{
    public static string Format(EngineResult result, int significantDigits, double scale = 0)
    {
        if (!TryUnit(result.Unit, out var unit))
        {
            var bare = result.Value.ToString("G" + Math.Max(1, significantDigits), CultureInfo.InvariantCulture).Replace('.', ',');
            return result.Unit.Length == 0 ? bare : bare + " " + result.Unit;
        }

        var magnitude = Math.Abs(result.Value);
        // 1e-14 relative to the largest result of this unit is solver noise.
        var value = scale > 0 && magnitude <= scale * 1e-14 ? 0 : result.Value;
        return Display(QuantityFormatter.Format(Quantity.Of(value, unit), unit, significantDigits), result.Unit);
    }

    private static string Display(string formatted, string unit) => unit switch
    {
        "Wh/day" => formatted.Replace("Wh/day", "Wh/dia", StringComparison.Ordinal),
        "d" => formatted.EndsWith(" d", StringComparison.Ordinal) ? formatted[..^2] + " dias" : formatted,
        _ => formatted,
    };

    public static string Trace(EngineResult result)
    {
        var formula = result.FormulaId switch
        {
            "ohm.P=I2R" => "P=I²R",
            "ohm.P=VI" => "P=V×I",
            "energy.Wh_day" or "energy.charge_Wh_day" => "Wh/dia = P×h",
            "energy.runtime_h" => "t = C×DoD/I",
            "energy.autonomy_d" => "dias = Wh/Wh_dia",
            "energy.recharge_h" => "t = C×DoD/(I×η)",
            "power.rating" => "P/Pnom",
            "mna.dc" => "MNA",
            _ => result.FormulaId,
        };
        return formula == result.FormulaId
            ? $"{result.ResultId} · {result.FormulaId}"
            : $"{result.ResultId} · {formula} · {result.FormulaId}";
    }

    private static bool TryUnit(string symbol, out Unit unit)
    {
        unit = symbol switch
        {
            "V" => KnownUnits.Volt,
            "A" => KnownUnits.Ampere,
            "W" => KnownUnits.Watt,
            "Wh/day" => KnownUnits.WattHourPerDay,
            "h" => KnownUnits.Hour,
            "d" => KnownUnits.Day,
            "Ah" => KnownUnits.AmpereHour,
            "Wh" => KnownUnits.WattHour,
            "1" or "" => KnownUnits.Dimensionless,
            _ => KnownUnits.Dimensionless,
        };
        return symbol is "V" or "A" or "W" or "Wh/day" or "h" or "d" or "Ah" or "Wh" or "1" or "";
    }
}

public sealed class ComponentResultRow
{
    public required string Id { get; init; }

    public required string TypeName { get; init; }

    public required string Voltage { get; init; }

    public required string Current { get; init; }

    public required string Power { get; init; }

    public required string VoltageTrace { get; init; }

    public required string CurrentTrace { get; init; }

    public required string PowerTrace { get; init; }

    public PowerRatingStatus? RatingStatus { get; init; }

    public string? RatingText { get; init; }

    public string RatingTrace { get; init; } = string.Empty;

    public string RatingKind => RatingStatus switch
    {
        PowerRatingStatus.Ok => "ok",
        PowerRatingStatus.Warning => "warning",
        PowerRatingStatus.Error => "error",
        _ => string.Empty,
    };
}

public sealed class EnergyCard
{
    public required string Title { get; init; }

    public required string Text { get; init; }

    public string? Trace { get; init; }
}

public sealed class ResultProjection
{
    public required IReadOnlyList<ComponentResultRow> Rows { get; init; }

    public required IReadOnlyList<EnergyCard> Cards { get; init; }

    public required string BalanceText { get; init; }

    public required bool PowerBalanceOk { get; init; }

    public required string SignConvention { get; init; }

    public required string ChargeEfficiency { get; init; }

    public static ResultProjection Empty() => new()
    {
        Rows = [],
        Cards =
        [
            new EnergyCard { Title = Strings.CardConsumption, Text = Strings.MissingValue },
            new EnergyCard { Title = Strings.CardChargeEnergy, Text = Strings.MissingValue },
            new EnergyCard { Title = Strings.CardAutonomy, Text = Strings.MissingValue },
            new EnergyCard { Title = Strings.CardChargeCurrent, Text = Strings.MissingValue },
            new EnergyCard { Title = Strings.CardRecharge, Text = Strings.MissingValue },
        ],
        BalanceText = string.Empty,
        PowerBalanceOk = false,
        SignConvention = Strings.SignConvention,
        ChargeEfficiency = Strings.ChargeEfficiency,
    };

    public static ResultProjection From(SchematicState state, DcSolution solution, int significantDigits)
    {
        var ratings = solution.PowerRatings.ToDictionary(rating => rating.ComponentId, StringComparer.Ordinal);
        var scales = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var result in solution.Results.All)
        {
            var magnitude = Math.Abs(result.Value);
            if (!scales.TryGetValue(result.Unit, out var current) || magnitude > current)
            {
                scales[result.Unit] = magnitude;
            }
        }

        var rows = new List<ComponentResultRow>();
        foreach (var component in state.Components)
        {
            var definition = ComponentLibrary.TryGet(component.Type, out var found) ? found : null;
            ratings.TryGetValue(component.Id, out var rating);
            rows.Add(new ComponentResultRow
            {
                Id = component.Id,
                TypeName = definition?.DisplayName ?? component.Type,
                Voltage = Text(solution, component.Id + ".V", significantDigits, scales),
                Current = Text(solution, component.Id + ".I", significantDigits, scales),
                Power = Text(solution, component.Id + ".P", significantDigits, scales),
                VoltageTrace = Trace(solution, component.Id + ".V"),
                CurrentTrace = Trace(solution, component.Id + ".I"),
                PowerTrace = Trace(solution, component.Id + ".P"),
                RatingStatus = rating?.Status,
                RatingText = rating?.Message,
                RatingTrace = Trace(solution, component.Id + ".rating"),
            });
        }

        return new ResultProjection
        {
            Rows = rows,
            Cards =
            [
                Card(Strings.CardConsumption, solution, significantDigits, scales, "energy.Wh_day", "energy.load.Wh_day"),
                Card(Strings.CardChargeEnergy, solution, significantDigits, scales, "energy.charge.Wh_day"),
                Autonomy(solution, significantDigits, scales),
                Card(Strings.CardChargeCurrent, solution, significantDigits, scales, "energy.I_charge"),
                Card(Strings.CardRecharge, solution, significantDigits, scales, "energy.recharge.h"),
            ],
            BalanceText = solution.PowerBalanceOk ? Strings.BalanceOk : Strings.BalanceBad,
            PowerBalanceOk = solution.PowerBalanceOk,
            SignConvention = Strings.SignConvention,
            ChargeEfficiency = Strings.ChargeEfficiency,
        };
    }

    private static EnergyCard Autonomy(DcSolution solution, int digits, IReadOnlyDictionary<string, double> scales)
    {
        if (solution.Results.TryGet("energy.I_charge", out var charge) && charge is not null && charge.Value > 0)
        {
            return new EnergyCard
            {
                Title = Strings.CardAutonomy,
                Text = Strings.NotApplicable,
                Trace = ResultText.Trace(charge),
            };
        }

        var parts = new List<string>();
        string? trace = null;
        if (solution.Results.TryGet("energy.runtime.h", out var runtime) && runtime is not null)
        {
            parts.Add(Format(runtime, digits, scales));
            trace = ResultText.Trace(runtime);
        }

        var autonomyId = solution.Results.TryGet("energy.autonomy.d", out var dischargeDays) && dischargeDays is not null
            ? "energy.autonomy.d"
            : "energy.load.autonomy.d";
        if (solution.Results.TryGet(autonomyId, out var days) && days is not null)
        {
            parts.Add(Format(days, digits, scales));
            trace = trace is null ? ResultText.Trace(days) : trace + " · " + ResultText.Trace(days);
        }

        return new EnergyCard
        {
            Title = Strings.CardAutonomy,
            Text = parts.Count == 0 ? Strings.MissingValue : string.Join(" · ", parts),
            Trace = trace,
        };
    }

    private static EnergyCard Card(
        string title,
        DcSolution solution,
        int digits,
        IReadOnlyDictionary<string, double> scales,
        params string[] resultIds)
    {
        foreach (var resultId in resultIds)
        {
            if (solution.Results.TryGet(resultId, out var result) && result is not null)
            {
                return new EnergyCard
                {
                    Title = title,
                    Text = Format(result, digits, scales),
                    Trace = ResultText.Trace(result),
                };
            }
        }

        return new EnergyCard { Title = title, Text = Strings.MissingValue, Trace = null };
    }

    private static string Format(EngineResult result, int digits, IReadOnlyDictionary<string, double> scales) =>
        ResultText.Format(result, digits, scales.TryGetValue(result.Unit, out var scale) ? scale : 0);

    private static string Text(DcSolution solution, string resultId, int digits, IReadOnlyDictionary<string, double> scales) =>
        solution.Results.TryGet(resultId, out var result) && result is not null
            ? Format(result, digits, scales)
            : Strings.MissingValue;

    private static string Trace(DcSolution solution, string resultId) =>
        solution.Results.TryGet(resultId, out var result) && result is not null
            ? ResultText.Trace(result)
            : string.Empty;
}
