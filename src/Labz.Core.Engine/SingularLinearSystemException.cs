namespace Labz.Core.Engine;

/// <summary>Gaussian elimination found no usable pivot.</summary>
public sealed class SingularLinearSystemException : Exception
{
    public SingularLinearSystemException()
        : base("The linear system is singular.")
    {
    }
}
