using Labz.Core.Units;

namespace Labz.Modules.Circuits;

public static class ParamEditor
{
    public static bool TryAccept(string type, string name, string text, out string? error)
    {
        if (!ComponentLibrary.TryGet(type, out var definition) || definition is null)
        {
            error = Strings.UnknownType(type);
            return false;
        }

        var parameter = definition.Params.FirstOrDefault(candidate => candidate.Name == name);
        if (parameter is null)
        {
            error = Strings.UnknownParam(name);
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            if (parameter.Optional)
            {
                error = null;
                return true;
            }

            error = Strings.RequiredParam(parameter.Label);
            return false;
        }

        if (parameter.Kind == ParamKind.Text)
        {
            error = null;
            return true;
        }

        if (parameter.Kind == ParamKind.State)
        {
            if (text is "open" or "closed")
            {
                error = null;
                return true;
            }

            error = Strings.StateValue();
            return false;
        }

        try
        {
            var quantity = QuantityParser.Parse(text, parameter.Dimension);
            if (!IsAccepted(type, name, quantity.Value))
            {
                error = ConstraintMessage(type, name, parameter.Label);
                return false;
            }

            error = null;
            return true;
        }
        catch (QuantityFormatException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool IsAccepted(string type, string name, double value)
    {
        if (!double.IsFinite(value))
        {
            return false;
        }

        if (type == "battery" && name == "usableDepthOfDischarge")
        {
            return value is >= 0 and <= 1;
        }

        if (name is "resistance" or "powerRating" or "capacitance" or "inductance" or "IS" or "N" or "forwardVoltage")
        {
            return value > 0;
        }

        if (name is "RS" or "Ron" or "Roff" or "seriesResistance" or "internalResistance" or "capacity" or "ratedPower" or "forwardCurrent")
        {
            return value >= 0;
        }

        return true;
    }

    private static string ConstraintMessage(string type, string name, string label)
    {
        if (type == "battery" && name == "usableDepthOfDischarge")
        {
            return Strings.DepthOfDischarge();
        }

        if (name is "RS" or "Ron" or "Roff" or "seriesResistance" or "internalResistance" or "capacity" or "ratedPower" or "forwardCurrent")
        {
            return Strings.NonNegativeFinite(label);
        }

        if (name is "voltage" or "openCircuitVoltage" or "nominalVoltage" or "initialVoltage" or "initialCurrent")
        {
            return Strings.FiniteValue(label);
        }

        return Strings.PositiveFinite(label);
    }
}
