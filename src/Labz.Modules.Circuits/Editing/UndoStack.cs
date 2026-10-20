namespace Labz.Modules.Circuits;

public enum EditKind
{
    Place,
    Move,
    Rotate,
    Wire,
    Delete,
    Value,
    Usage,
}

public interface ISchematicCommand
{
    EditKind Kind { get; }

    string Description { get; }

    bool AffectsCircuit { get; }

    void Apply(SchematicState state);

    void Revert(SchematicState state);
}

public sealed class SnapshotCommand : ISchematicCommand
{
    private readonly SchematicMemento _before;
    private readonly SchematicMemento _after;

    public SnapshotCommand(EditKind kind, string description, bool affectsCircuit, SchematicMemento before, SchematicMemento after)
    {
        Kind = kind;
        Description = description;
        AffectsCircuit = affectsCircuit;
        _before = before;
        _after = after;
    }

    public EditKind Kind { get; }

    public string Description { get; }

    public bool AffectsCircuit { get; }

    public void Apply(SchematicState state) => _after.Restore(state);

    public void Revert(SchematicState state) => _before.Restore(state);
}

public sealed class UndoStack
{
    public const int Capacity = 100;

    private readonly List<ISchematicCommand> _commands = [];
    private int _applied;

    public int Count => _applied;

    public bool CanUndo => _applied > 0;

    public bool CanRedo => _applied < _commands.Count;

    public void Push(ISchematicCommand command)
    {
        if (_applied < _commands.Count)
        {
            _commands.RemoveRange(_applied, _commands.Count - _applied);
        }

        _commands.Add(command);
        _applied = _commands.Count;
        while (_commands.Count > Capacity)
        {
            _commands.RemoveAt(0);
            _applied--;
        }
    }

    public ISchematicCommand? TryUndo()
    {
        if (!CanUndo)
        {
            return null;
        }

        _applied--;
        return _commands[_applied];
    }

    public ISchematicCommand? TryRedo()
    {
        if (!CanRedo)
        {
            return null;
        }

        var command = _commands[_applied];
        _applied++;
        return command;
    }
}
