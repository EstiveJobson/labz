using System.Globalization;
using Labz.Core.Engine;
using Labz.Core.Units;

namespace Labz.Modules.Circuits;

public static class ResultText
{
    public static string Format(EngineResult result, int significantDigits)
    {
        if (!TryUnit(result.Unit, out var unit))
        {
            var bare = result.Value.ToString("G" + Math.Max(1, significantDigits), CultureInfo.InvariantCulture).Replace('.', ',');
            return result.Unit.Length == 0 ? bare : bare + " " + result.Unit;
        }

        return QuantityFormatter.Format(Quantity.Of(result.Value, unit), unit, significantDigits);
    }

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
            new EnergyCard { Title = Strings.CardWhPerDay, Text = Strings.MissingValue },
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
        var rows = new List<ComponentResultRow>();
        foreach (var component in state.Components)
        {
            var definition = ComponentLibrary.TryGet(component.Type, out var found) ? found : null;
            ratings.TryGetValue(component.Id, out var rating);
            rows.Add(new ComponentResultRow
            {
                Id = component.Id,
                TypeName = definition?.DisplayName ?? component.Type,
                Voltage = Text(solution, component.Id + ".V", significantDigits),
                Current = Text(solution, component.Id + ".I", significantDigits),
                Power = Text(solution, component.Id + ".P", significantDigits),
                VoltageTrace = Trace(solution, component.Id + ".V"),
                CurrentTrace = Trace(solution, component.Id + ".I"),
                PowerTrace = Trace(solution, component.Id + ".P"),
                RatingStatus = rating?.Status,
                RatingText = rating?.Message,
            });
        }

        return new ResultProjection
        {
            Rows = rows,
            Cards =
            [
                Card(Strings.CardWhPerDay, solution, significantDigits, "energy.Wh_day", "energy.charge.Wh_day"),
                Autonomy(solution, significantDigits),
                Card(Strings.CardChargeCurrent, solution, significantDigits, "energy.I_charge"),
                Card(Strings.CardRecharge, solution, significantDigits, "energy.recharge.h"),
            ],
            BalanceText = solution.PowerBalanceOk ? Strings.BalanceOk : Strings.BalanceBad,
            PowerBalanceOk = solution.PowerBalanceOk,
            SignConvention = Strings.SignConvention,
            ChargeEfficiency = Strings.ChargeEfficiency,
        };
    }

    private static EnergyCard Autonomy(DcSolution solution, int digits)
    {
        var parts = new List<string>();
        string? trace = null;
        if (solution.Results.TryGet("energy.runtime.h", out var runtime) && runtime is not null)
        {
            parts.Add(ResultText.Format(runtime, digits));
            trace = ResultText.Trace(runtime);
        }

        if (solution.Results.TryGet("energy.autonomy.d", out var days) && days is not null)
        {
            parts.Add(ResultText.Format(days, digits));
            trace = trace is null ? ResultText.Trace(days) : trace + " · " + ResultText.Trace(days);
        }

        return new EnergyCard
        {
            Title = Strings.CardAutonomy,
            Text = parts.Count == 0 ? Strings.MissingValue : string.Join(" · ", parts),
            Trace = trace,
        };
    }

    private static EnergyCard Card(string title, DcSolution solution, int digits, params string[] resultIds)
    {
        foreach (var resultId in resultIds)
        {
            if (solution.Results.TryGet(resultId, out var result) && result is not null)
            {
                return new EnergyCard
                {
                    Title = title,
                    Text = ResultText.Format(result, digits),
                    Trace = ResultText.Trace(result),
                };
            }
        }

        return new EnergyCard { Title = title, Text = Strings.MissingValue, Trace = null };
    }

    private static string Text(DcSolution solution, string resultId, int digits) =>
        solution.Results.TryGet(resultId, out var result) && result is not null
            ? ResultText.Format(result, digits)
            : Strings.MissingValue;

    private static string Trace(DcSolution solution, string resultId) =>
        solution.Results.TryGet(resultId, out var result) && result is not null
            ? ResultText.Trace(result)
            : string.Empty;
}
