using Labz.Core.Engine;

namespace Labz.Modules.Circuits;

public enum PowerRatingStatus
{
    Ok,
    Warning,
    Error,
}

public sealed record PowerRatingFinding(
    string ComponentId,
    PowerRatingStatus Status,
    string Message,
    double PowerWatts,
    double RatingWatts,
    double Ratio);

/// <summary>
/// Hours are caller inputs. Charge efficiency defaults to 1. Peak-sun hours have no engine default;
/// where the project file stores them is decided in M2.
/// </summary>
public sealed record EnergySettings(double? LoadHoursPerDay = null, double? PeakSunHours = null, double ChargeEfficiency = 1)
{
    public void EnsureValid()
    {
        RequireNonNegative(LoadHoursPerDay, nameof(LoadHoursPerDay));
        RequireNonNegative(PeakSunHours, nameof(PeakSunHours));
        if (!double.IsFinite(ChargeEfficiency) || ChargeEfficiency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ChargeEfficiency));
        }
    }

    private static void RequireNonNegative(double? value, string name)
    {
        if (value is null)
        {
            return;
        }

        if (!double.IsFinite(value.Value) || value.Value < 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

public sealed class DcSolution
{
    public DcSolution(ResultStore results, bool powerBalanceOk, IReadOnlyList<PowerRatingFinding> powerRatings)
    {
        Results = results;
        PowerBalanceOk = powerBalanceOk;
        PowerRatings = powerRatings;
    }

    public ResultStore Results { get; }

    public bool PowerBalanceOk { get; }

    public IReadOnlyList<PowerRatingFinding> PowerRatings { get; }

    public EngineResult Get(string resultId) => Results.Get(resultId);
}
