namespace Labz.Modules.Circuits;

/// <summary>
/// Range checks whose message is the whole exception text. The Portuguese sentence
/// starts with a capital, and the English "(Parameter ...)" suffix is left off.
/// </summary>
internal static class ParameterValidation
{
    public static double RequirePositive(double value, string message)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            ThrowOutOfRange(message);
        }

        return value;
    }

    public static double RequireNonNegative(double value, string message)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            ThrowOutOfRange(message);
        }

        return value;
    }

    public static void RequireFinite(double value, string message)
    {
        if (!double.IsFinite(value))
        {
            ThrowOutOfRange(message);
        }
    }

    public static void ThrowOutOfRange(string message) =>
        throw new ArgumentOutOfRangeException(paramName: null, message);
}
