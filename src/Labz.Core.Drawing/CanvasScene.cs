namespace Labz.Core.Drawing;

public sealed record SymbolPin(string Name, CanvasPoint Local);

public sealed record SymbolStroke(IReadOnlyList<CanvasPoint> Points);

public sealed class SymbolGeometry
{
    public SymbolGeometry(IReadOnlyList<SymbolStroke> strokes, IReadOnlyList<SymbolPin> pins)
    {
        Strokes = strokes ?? throw new ArgumentNullException(nameof(strokes));
        Pins = pins ?? throw new ArgumentNullException(nameof(pins));
    }

    public IReadOnlyList<SymbolStroke> Strokes { get; }

    public IReadOnlyList<SymbolPin> Pins { get; }
}

public sealed class ScenePin
{
    public required string ComponentId { get; init; }

    public required string Name { get; init; }

    public required CanvasPoint Position { get; init; }
}

public sealed class SceneSymbol
{
    public required string Id { get; init; }

    public required CanvasPoint Center { get; init; }

    public required IReadOnlyList<SymbolStroke> Strokes { get; init; }

    public required IReadOnlyList<ScenePin> Pins { get; init; }

    public required bool Selected { get; init; }

    public required bool Highlighted { get; init; }
}

public sealed class SceneLabel
{
    public required CanvasPoint Position { get; init; }

    public required string Text { get; init; }
}

public sealed class SceneWire
{
    public required string NetId { get; init; }

    public required IReadOnlyList<CanvasPoint> Points { get; init; }

    /// <summary>When set, this segment is the stub of one pin and can be deleted on its own.</summary>
    public string? LeafComponentId { get; init; }

    public string? LeafPin { get; init; }
}

public sealed class CanvasScene
{
    public static CanvasScene Empty { get; } = new([], [], []);

    public CanvasScene(
        IReadOnlyList<SceneSymbol> symbols,
        IReadOnlyList<SceneWire> wires,
        IReadOnlyList<SceneLabel> labels)
    {
        Symbols = symbols;
        Wires = wires;
        Labels = labels;
    }

    public IReadOnlyList<SceneSymbol> Symbols { get; }

    public IReadOnlyList<SceneWire> Wires { get; }

    public IReadOnlyList<SceneLabel> Labels { get; }
}
