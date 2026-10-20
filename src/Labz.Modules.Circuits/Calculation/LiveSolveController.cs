using Labz.Core.Units;

namespace Labz.Modules.Circuits;

public interface IMainThread
{
    void Invoke(Action action);
}

public sealed class DirectMainThread : IMainThread
{
    public void Invoke(Action action) => action();
}

public sealed class LiveSolveController : IDisposable
{
    private readonly IMainThread _main;
    private readonly TimeSpan _debounce;
    private readonly TimeSpan _progressAfter;
    private readonly Func<CircuitModel, EnergySettings?, CancellationToken, IProgress<double>, DcSolution> _solve;
    private readonly object _gate = new();
    private int _generation;
    private CancellationTokenSource? _run;

    public LiveSolveController(
        IMainThread? mainThread = null,
        TimeSpan? debounce = null,
        TimeSpan? progressAfter = null,
        Func<CircuitModel, EnergySettings?, CancellationToken, IProgress<double>, DcSolution>? solve = null)
    {
        _main = mainThread ?? new DirectMainThread();
        _debounce = debounce ?? TimeSpan.FromMilliseconds(250);
        _progressAfter = progressAfter ?? TimeSpan.FromMilliseconds(200);
        _solve = solve ?? ((circuit, energy, token, progress) => new DcSolver().Solve(circuit, energy, token, progress));
    }

    public bool IsRunning { get; private set; }

    public bool ShowProgress { get; private set; }

    public double Progress { get; private set; }

    public DcSolution? Solution { get; private set; }

    public IReadOnlyList<CircuitProblem> Problems { get; private set; } = [];

    public string? BlockedMessage { get; private set; }

    public int SolverThreadId { get; private set; }

    public event Action? Changed;

    public void Schedule(SchematicState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var snapshot = state.Capture();
        var generation = Interlocked.Increment(ref _generation);
        _run?.Cancel();
        var cts = new CancellationTokenSource();
        _run = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounce, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (generation != Volatile.Read(ref _generation))
            {
                return;
            }

            await SolveAsync(snapshot, generation, cts).ConfigureAwait(false);
        });
    }

    public void Cancel() => _run?.Cancel();

    public void Dispose() => _run?.Cancel();

    private Task SolveAsync(SchematicMemento snapshot, int generation, CancellationTokenSource cts)
    {
        Post(() =>
        {
            IsRunning = true;
            ShowProgress = false;
            Progress = 0;
        });

        var progressDelay = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_progressAfter, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (generation == Volatile.Read(ref _generation))
            {
                Post(() => ShowProgress = IsRunning);
            }
        });

        try
        {
            var state = new SchematicState();
            snapshot.Restore(state);
            CircuitModel circuit;
            try
            {
                circuit = CircuitMapper.ToCircuit(state);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or QuantityFormatException)
            {
                PublishBlocked(generation, [new CircuitProblem(CircuitProblemKind.UnconnectedPin, exception.Message, [])]);
                return Task.CompletedTask;
            }

            var problems = CircuitDiagnostics.Check(circuit);
            if (problems.Count > 0)
            {
                PublishBlocked(generation, problems);
                return Task.CompletedTask;
            }

            var progress = new Progress<double>(value =>
            {
                if (generation == Volatile.Read(ref _generation))
                {
                    Post(() => Progress = value);
                }
            });
            var solverThreadId = Environment.CurrentManagedThreadId;
            var solution = _solve(circuit, CircuitMapper.ToEnergy(state), cts.Token, progress);
            if (generation != Volatile.Read(ref _generation))
            {
                return Task.CompletedTask;
            }

            Post(() =>
            {
                SolverThreadId = solverThreadId;
                Solution = solution;
                Problems = [];
                BlockedMessage = null;
                IsRunning = false;
                ShowProgress = false;
                Progress = 1;
            });
        }
        catch (OperationCanceledException)
        {
            if (generation == Volatile.Read(ref _generation))
            {
                Post(() =>
                {
                    IsRunning = false;
                    ShowProgress = false;
                });
            }
        }
        catch (CircuitSolverException exception)
        {
            PublishBlocked(generation, [new CircuitProblem(CircuitProblemKind.VoltageSourceShort, exception.Message, exception.ComponentIds)]);
        }
        finally
        {
            progressDelay.ContinueWith(_ => { }, TaskScheduler.Default);
        }

        return Task.CompletedTask;
    }

    private void PublishBlocked(int generation, IReadOnlyList<CircuitProblem> problems)
    {
        if (generation != Volatile.Read(ref _generation))
        {
            return;
        }

        Post(() =>
        {
            Problems = problems;
            Solution = null;
            BlockedMessage = Strings.FixSchematic;
            IsRunning = false;
            ShowProgress = false;
        });
    }

    private void Post(Action action)
    {
        _main.Invoke(() =>
        {
            lock (_gate)
            {
                action();
            }

            Changed?.Invoke();
        });
    }
}
