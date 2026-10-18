namespace Labz.Modules.Circuits;

public abstract class CircuitSolverException : Exception
{
    protected CircuitSolverException(string message, IReadOnlyList<string> componentIds)
        : base(message)
    {
        ComponentIds = componentIds;
    }

    public IReadOnlyList<string> ComponentIds { get; }
}

public sealed class MissingGroundException : CircuitSolverException
{
    public MissingGroundException(string message, IReadOnlyList<string> componentIds)
        : base(message, componentIds)
    {
    }
}

public sealed class FloatingNodeException : CircuitSolverException
{
    public FloatingNodeException(string message, IReadOnlyList<string> nodeIds, IReadOnlyList<string> componentIds)
        : base(message, componentIds)
    {
        NodeIds = nodeIds;
    }

    public IReadOnlyList<string> NodeIds { get; }
}

public sealed class VoltageSourceLoopException : CircuitSolverException
{
    public VoltageSourceLoopException(string message, IReadOnlyList<string> componentIds)
        : base(message, componentIds)
    {
    }
}

public sealed class SingularCircuitException : CircuitSolverException
{
    public SingularCircuitException(string message, IReadOnlyList<string> componentIds)
        : base(message, componentIds)
    {
    }
}

public sealed class DcSolverConvergenceException : CircuitSolverException
{
    public DcSolverConvergenceException(string message, IReadOnlyList<string> componentIds)
        : base(message, componentIds)
    {
    }
}
