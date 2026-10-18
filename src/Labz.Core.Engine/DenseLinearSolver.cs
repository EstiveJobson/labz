namespace Labz.Core.Engine;

/// <summary>
/// Dense Gaussian elimination with partial pivoting and two iterative-refinement passes.
/// Enough for the v0 MNA systems; no extra numeric package.
/// </summary>
public static class DenseLinearSolver
{
    public static double[] Solve(double[,] matrix, double[] rhs)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(rhs);
        var n = rhs.Length;
        if (matrix.GetLength(0) != n || matrix.GetLength(1) != n)
        {
            throw new ArgumentException("The matrix must be square and match the right-hand side.", nameof(matrix));
        }

        var solution = FactorAndSolve(matrix, rhs);
        for (var pass = 0; pass < 2; pass++)
        {
            var residual = MultiplyResidual(matrix, solution, rhs);
            if (MaxAbs(residual) == 0)
            {
                break;
            }

            var correction = FactorAndSolve(matrix, residual);
            for (var i = 0; i < n; i++)
            {
                solution[i] += correction[i];
            }
        }

        return solution;
    }

    private static double[] FactorAndSolve(double[,] matrix, double[] rhs)
    {
        var n = rhs.Length;
        var a = (double[,])matrix.Clone();
        var b = (double[])rhs.Clone();
        var threshold = Math.Max(MaxAbs(a), 1) * 1e-12;

        for (var k = 0; k < n; k++)
        {
            var pivot = k;
            var pivotMagnitude = Math.Abs(a[k, k]);
            for (var i = k + 1; i < n; i++)
            {
                var magnitude = Math.Abs(a[i, k]);
                if (magnitude > pivotMagnitude)
                {
                    pivotMagnitude = magnitude;
                    pivot = i;
                }
            }

            if (pivotMagnitude <= threshold)
            {
                throw new SingularLinearSystemException();
            }

            SwapRows(a, b, k, pivot, n);
            var diagonal = a[k, k];
            for (var i = k + 1; i < n; i++)
            {
                var factor = a[i, k] / diagonal;
                a[i, k] = 0;
                for (var j = k + 1; j < n; j++)
                {
                    a[i, j] -= factor * a[k, j];
                }

                b[i] -= factor * b[k];
            }
        }

        var x = new double[n];
        for (var i = n - 1; i >= 0; i--)
        {
            var sum = b[i];
            for (var j = i + 1; j < n; j++)
            {
                sum -= a[i, j] * x[j];
            }

            x[i] = sum / a[i, i];
        }

        return x;
    }

    private static double[] MultiplyResidual(double[,] matrix, double[] solution, double[] rhs)
    {
        var n = rhs.Length;
        var residual = new double[n];
        for (var i = 0; i < n; i++)
        {
            var sum = 0d;
            for (var j = 0; j < n; j++)
            {
                sum += matrix[i, j] * solution[j];
            }

            residual[i] = rhs[i] - sum;
        }

        return residual;
    }

    private static void SwapRows(double[,] a, double[] b, int left, int right, int n)
    {
        if (left == right)
        {
            return;
        }

        for (var column = 0; column < n; column++)
        {
            (a[left, column], a[right, column]) = (a[right, column], a[left, column]);
        }

        (b[left], b[right]) = (b[right], b[left]);
    }

    private static double MaxAbs(double[,] matrix)
    {
        var max = 0d;
        var rows = matrix.GetLength(0);
        var columns = matrix.GetLength(1);
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < columns; j++)
            {
                max = Math.Max(max, Math.Abs(matrix[i, j]));
            }
        }

        return max;
    }

    private static double MaxAbs(double[] values)
    {
        var max = 0d;
        foreach (var value in values)
        {
            max = Math.Max(max, Math.Abs(value));
        }

        return max;
    }
}
