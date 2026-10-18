namespace Labz.Core.Engine;

/// <summary>Results of one calculation run, looked up by result id.</summary>
public sealed class ResultStore
{
    private readonly Dictionary<string, EngineResult> _results = new(StringComparer.Ordinal);

    public IReadOnlyList<EngineResult> All => _results.Values.ToArray();

    public void Add(EngineResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!_results.TryAdd(result.ResultId, result))
        {
            throw new InvalidOperationException($"Duplicate result id '{result.ResultId}'.");
        }
    }

    public EngineResult Get(string resultId)
    {
        if (TryGet(resultId, out var result))
        {
            return result!;
        }

        throw new KeyNotFoundException($"No engine result '{resultId}'.");
    }

    public bool TryGet(string resultId, out EngineResult? result)
    {
        if (resultId is null)
        {
            result = null;
            return false;
        }

        return _results.TryGetValue(resultId, out result);
    }
}
