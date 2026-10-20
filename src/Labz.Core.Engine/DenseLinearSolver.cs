namespace Labz.Core.Engine;

/// <summary>
/// Dense Gaussian elimination with partial pivoting.
/// The matrix is factored once per solve; a residual correction reuses that factorization.
/// The elimination loop observes <see cref="CancellationToken"/> so a long ladder can stop mid-factor.
/// </summary>
public static class DenseLinearSolver
{
    private static int _factorizationCount;

    private static int _refinementCount;

    internal static int FactorizationCount => Interlocked.CompareExchange(ref _factorizationCount, 0, 0);

    internal static int RefinementCount => Interlocked.CompareExchange(ref _refinementCount, 0, 0);

    public static double[] Solve(
        double[,] matrix,
        double[] rhs,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null,
        double progressStart = 0,
        double progressEnd = 1)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(rhs);
        var n = rhs.Length;
        if (matrix.GetLength(0) != n || matrix.GetLength(1) != n)
        {
            throw new ArgumentException("The matrix must be square and match the right-hand side.", nameof(matrix));
        }

        var factors = Factor(matrix, cancellationToken, progress, progressStart, progressEnd);
        var solution = factors.Solve(rhs);
        var scale = Math.Max(1, MaxAbs(rhs));
        // Stop when the residual is already at the tolerance. The pass cap only bounds a
        // matrix whose residual stays large; ordinary circuit solves return on the first check.
        for (var pass = 0; pass < 4; pass++)
        {
            var residual = MultiplyResidual(matrix, solution, rhs);
            if (MaxAbs(residual) <= 1e-12 * scale)
            {
                break;
            }

            Interlocked.Increment(ref _refinementCount);
            var correction = factors.Solve(residual);
            for (var i = 0; i < n; i++)
            {
                solution[i] += correction[i];
            }
        }

        progress?.Report(progressEnd);
        return solution;
    }

    private static LuFactorization Factor(
        double[,] matrix,
        CancellationToken cancellationToken,
        IProgress<double>? progress,
        double progressStart,
        double progressEnd)
    {
        Interlocked.Increment(ref _factorizationCount);
        var n = matrix.GetLength(0);
        var a = (double[,])matrix.Clone();
        // Equilibrate before pivoting. A 1e-16 row beside a 1e16 row is otherwise lost,
        // and iterative refinement does not restore it.
        var rowScale = new double[n];
        for (var row = 0; row < n; row++)
        {
            var max = 0d;
            for (var column = 0; column < n; column++)
            {
                max = Math.Max(max, Math.Abs(a[row, column]));
            }

            rowScale[row] = max > 0 ? max : 1;
            for (var column = 0; column < n; column++)
            {
                a[row, column] /= rowScale[row];
            }
        }

        var permutation = new int[n];
        for (var row = 0; row < n; row++)
        {
            permutation[row] = row;
        }

        var threshold = 1e-15;
        var span = progressEnd - progressStart;

        for (var k = 0; k < n; k++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (progress is not null && (k % 8 == 0 || k == n - 1))
            {
                progress.Report(progressStart + (span * (k + 1) / n));
            }

            var pivotRow = k;
            var pivotMagnitude = Math.Abs(a[k, k]);
            for (var i = k + 1; i < n; i++)
            {
                var magnitude = Math.Abs(a[i, k]);
                if (magnitude > pivotMagnitude)
                {
                    pivotMagnitude = magnitude;
                    pivotRow = i;
                }
            }

            if (pivotMagnitude <= threshold)
            {
                throw new SingularLinearSystemException();
            }

            if (pivotRow != k)
            {
                SwapRows(a, k, pivotRow, n);
                (permutation[k], permutation[pivotRow]) = (permutation[pivotRow], permutation[k]);
            }

            var diagonal = a[k, k];
            for (var i = k + 1; i < n; i++)
            {
                var factor = a[i, k] / diagonal;
                a[i, k] = factor;
                for (var j = k + 1; j < n; j++)
                {
                    a[i, j] -= factor * a[k, j];
                }
            }
        }

        return new LuFactorization(a, permutation, rowScale);
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

    private static void SwapRows(double[,] a, int left, int right, int n)
    {
        for (var column = 0; column < n; column++)
        {
            (a[left, column], a[right, column]) = (a[right, column], a[left, column]);
        }
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

    private sealed class LuFactorization(double[,] factors, int[] permutation, double[] rowScale)
    {
        public double[] Solve(double[] rhs)
        {
            var n = rhs.Length;
            var b = new double[n];
            for (var i = 0; i < n; i++)
            {
                var original = permutation[i];
                b[i] = rhs[original] / rowScale[original];
            }

            for (var k = 0; k < n; k++)
            {
                for (var i = k + 1; i < n; i++)
                {
                    b[i] -= factors[i, k] * b[k];
                }
            }

            var x = new double[n];
            for (var i = n - 1; i >= 0; i--)
            {
                var sum = b[i];
                for (var j = i + 1; j < n; j++)
                {
                    sum -= factors[i, j] * x[j];
                }

                x[i] = sum / factors[i, i];
            }

            return x;
        }
    }
}
