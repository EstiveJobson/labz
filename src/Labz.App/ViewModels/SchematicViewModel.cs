using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Labz.App.Resources;
using Labz.Core.Drawing;
using Labz.Modules.Circuits;

namespace Labz.App.ViewModels;

public sealed class SchematicViewModel : ObservableObject
{
    private readonly HashSet<string> _highlighted = new(StringComparer.Ordinal);
    private string? _selectedId;
    private string? _armedType;
    private string? _wireComponent;
    private string? _wirePin;
    private string? _selectedNetId;
    private string? _selectedLeafComponent;
    private string? _selectedLeafPin;
    private string _status = string.Empty;
    private bool _pressing;
    private bool _dragged;
    private string? _pressId;
    private double _pressX;
    private double _pressY;
    private double _startX;
    private double _startY;
    private double _zoom = 1;
    private double _panX = 40;
    private double _panY = 40;
    private bool _showSchottky;

    public SchematicViewModel(SchematicSession session, ResultsViewModel? results = null)
    {
        Session = session;
        Results = results;
        Library = new ObservableCollection<LibraryItemViewModel>(
            ComponentLibrary.All.Select(definition => new LibraryItemViewModel(definition.Type, definition.DisplayName)));
        Fields = [];
        ArmCommand = new RelayCommand<string>(Arm);
        UndoCommand = new RelayCommand(Undo, () => Session.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => Session.CanRedo);
        RotateCommand = new RelayCommand(RotateSelected, () => _selectedId is not null);
        DeleteCommand = new RelayCommand(DeleteSelected, () => _selectedId is not null || _selectedNetId is not null);
        SchottkyCommand = new RelayCommand(() => ApplyPreset(true), () => _showSchottky);
        StandardDiodeCommand = new RelayCommand(() => ApplyPreset(false), () => _showSchottky);
        Session.Changed += _ => RefreshCommands();
        SelectProblemCommand = new RelayCommand<CircuitProblem>(SelectProblem);
        if (Results is not null)
        {
            Results.Updated += () => Highlight(Results.Problems.SelectMany(problem => problem.ComponentIds));
        }

        RebuildScene();
    }

    public ResultsViewModel? Results { get; }

    public RelayCommand<CircuitProblem> SelectProblemCommand { get; }

    public SchematicSession Session { get; }

    public ObservableCollection<LibraryItemViewModel> Library { get; }

    public ObservableCollection<ParamFieldViewModel> Fields { get; }

    public CanvasScene Scene { get; private set; } = CanvasScene.Empty;

    public RelayCommand<string> ArmCommand { get; }

    public RelayCommand UndoCommand { get; }

    public RelayCommand RedoCommand { get; }

    public RelayCommand RotateCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public RelayCommand SchottkyCommand { get; }

    public RelayCommand StandardDiodeCommand { get; }

    public string LibraryHeader => Strings.LibraryHeader;

    public string PropertiesHeader => Strings.PropertiesHeader;

    public string UndoLabel => Strings.Undo;

    public string RedoLabel => Strings.Redo;

    public string RotateLabel => Strings.Rotate;

    public string DeleteLabel => Strings.Delete;

    public string SchottkyLabel => Strings.SchottkyPreset;

    public string StandardDiodeLabel => Strings.StandardDiodePreset;

    public string NoSelection => Strings.NoSelection;

    public string? SelectedId => _selectedId;

    public string SelectionTitle =>
        _selectedId is null ? NoSelection : _selectedId + " · " + TypeName(_selectedId);

    public bool HasSelection => _selectedId is not null;

    public bool ShowDiodePresets => _showSchottky;

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public double Zoom
    {
        get => _zoom;
        private set => SetProperty(ref _zoom, value);
    }

    public double PanX
    {
        get => _panX;
        private set => SetProperty(ref _panX, value);
    }

    public double PanY
    {
        get => _panY;
        private set => SetProperty(ref _panY, value);
    }

    public void Arm(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return;
        }

        _armedType = type;
        _wireComponent = null;
        _wirePin = null;
        var name = ComponentLibrary.Get(type).DisplayName;
        Status = string.Format(System.Globalization.CultureInfo.CurrentUICulture, Strings.PlaceHint, name);
        OnPropertyChanged(nameof(Status));
    }

    public void CancelTool()
    {
        _armedType = null;
        _wireComponent = null;
        _wirePin = null;
        Status = string.Empty;
    }

    public void Undo()
    {
        if (Session.Undo())
        {
            EnsureSelection();
            RebuildFields();
            RebuildScene();
            RefreshCommands();
        }
    }

    public void Redo()
    {
        if (Session.Redo())
        {
            EnsureSelection();
            RebuildFields();
            RebuildScene();
            RefreshCommands();
        }
    }

    public void RotateSelected()
    {
        if (_selectedId is null)
        {
            return;
        }

        Session.Rotate(_selectedId);
        RebuildScene();
        RefreshCommands();
    }

    public void DeleteSelected()
    {
        if (_selectedId is not null)
        {
            Session.Delete(_selectedId);
            _selectedId = null;
        }
        else if (_selectedNetId is not null)
        {
            Session.DeleteWire(_selectedNetId, _selectedLeafComponent, _selectedLeafPin);
            _selectedNetId = null;
        }
        else
        {
            return;
        }

        RebuildFields();
        RebuildScene();
        RefreshCommands();
    }

    public void PointerDown(double x, double y)
    {
        var hit = HitTester.Hit(Scene, x, y);
        if (_armedType is not null && hit.Kind is HitKind.None or HitKind.Body)
        {
            var id = Session.Place(_armedType, x, y);
            Select(id);
            return;
        }

        if (hit.Kind == HitKind.Pin && hit.ComponentId is not null && hit.Pin is not null)
        {
            PinClicked(hit.ComponentId, hit.Pin);
            return;
        }

        if (hit.Kind == HitKind.Wire && hit.NetId is not null)
        {
            if (_wireComponent is not null && _wirePin is not null)
            {
                Session.ConnectPinToNet(_wireComponent, _wirePin, hit.NetId);
                _wireComponent = null;
                _wirePin = null;
                Status = string.Empty;
                RebuildScene();
                RefreshCommands();
                return;
            }

            _selectedId = null;
            _selectedNetId = hit.NetId;
            _selectedLeafComponent = hit.LeafComponentId;
            _selectedLeafPin = hit.LeafPin;
            RebuildFields();
            RefreshCommands();
            return;
        }

        if (hit.Kind == HitKind.Body && hit.ComponentId is not null)
        {
            _wireComponent = null;
            _selectedNetId = null;
            Select(hit.ComponentId);
            var component = Session.State.Find(hit.ComponentId);
            if (component is not null)
            {
                _pressing = true;
                _dragged = false;
                _pressId = hit.ComponentId;
                _pressX = x;
                _pressY = y;
                _startX = component.X;
                _startY = component.Y;
                Session.BeginDrag(hit.ComponentId);
            }

            return;
        }

        _selectedId = null;
        _selectedNetId = null;
        _wireComponent = null;
        _wirePin = null;
        Status = string.Empty;
        RebuildFields();
        RebuildScene();
        RefreshCommands();
    }

    public void PointerMove(double x, double y)
    {
        if (!_pressing || _pressId is null)
        {
            return;
        }

        if (Math.Abs(x - _pressX) < 3 && Math.Abs(y - _pressY) < 3)
        {
            return;
        }

        var component = Session.State.Find(_pressId);
        if (component is null)
        {
            return;
        }

        _dragged = true;
        Session.PreviewDrag(_pressId, _startX + (x - _pressX), _startY + (y - _pressY));
        RebuildScene();
    }

    public void PointerUp(double x, double y)
    {
        if (!_pressing || _pressId is null)
        {
            _pressing = false;
            return;
        }

        var id = _pressId;
        _pressing = false;
        _pressId = null;
        if (!_dragged)
        {
            return;
        }

        Session.CommitDrag(id);
        _dragged = false;
        RebuildScene();
        RefreshCommands();
    }

    public void ZoomAt(double screenX, double screenY, double factor)
    {
        var next = Math.Clamp(Zoom * factor, 0.2, 4);
        var worldX = (screenX - PanX) / Zoom;
        var worldY = (screenY - PanY) / Zoom;
        Zoom = next;
        PanX = screenX - (worldX * next);
        PanY = screenY - (worldY * next);
    }

    public void PanBy(double screenDx, double screenDy)
    {
        PanX += screenDx;
        PanY += screenDy;
    }

    public void Highlight(IEnumerable<string> componentIds)
    {
        _highlighted.Clear();
        foreach (var id in componentIds)
        {
            _highlighted.Add(id);
        }

        RebuildScene();
    }

    public void SelectProblem(CircuitProblem? problem)
    {
        var id = problem?.ComponentIds.FirstOrDefault();
        if (!string.IsNullOrEmpty(id))
        {
            Select(id);
        }
    }

    public void Select(string id)
    {
        _selectedId = id;
        _selectedNetId = null;
        _armedType = null;
        Status = string.Empty;
        RebuildFields();
        RebuildScene();
        RefreshCommands();
    }

    private void PinClicked(string componentId, string pin)
    {
        if (_wireComponent is null || _wirePin is null)
        {
            _wireComponent = componentId;
            _wirePin = pin;
            Status = Strings.WireHint;
            Select(componentId);
            return;
        }

        if (_wireComponent == componentId && _wirePin == pin)
        {
            CancelTool();
            return;
        }

        Session.ConnectPins(_wireComponent, _wirePin, componentId, pin);
        _wireComponent = null;
        _wirePin = null;
        Status = string.Empty;
        RebuildScene();
        RefreshCommands();
    }

    private void ApplyPreset(bool schottky)
    {
        if (_selectedId is null)
        {
            return;
        }

        Session.ApplyDiodePreset(_selectedId, schottky);
        RebuildFields();
        RefreshCommands();
    }

    private void RebuildFields()
    {
        Fields.Clear();
        var component = _selectedId is null ? null : Session.State.Find(_selectedId);
        _showSchottky = component?.Type == "diode";
        if (component is null || !ComponentLibrary.TryGet(component.Type, out var definition) || definition is null)
        {
            OnPropertyChanged(nameof(SelectionTitle));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(ShowDiodePresets));
            return;
        }

        foreach (var parameter in definition.Params)
        {
            component.Params.TryGetValue(parameter.Name, out var text);
            Fields.Add(new ParamFieldViewModel(this, component.Id, parameter, text ?? string.Empty));
        }

        OnPropertyChanged(nameof(SelectionTitle));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowDiodePresets));
    }

    internal bool TryCommit(string componentId, string name, string text, out string? error)
    {
        var ok = Session.TrySetParam(componentId, name, text, out error);
        if (ok)
        {
            RefreshCommands();
        }

        return ok;
    }

    internal void SetSwitch(string componentId, bool closed)
    {
        Session.SetSwitchClosed(componentId, closed);
        RefreshCommands();
    }

    private void RebuildScene()
    {
        Scene = SceneBuilder.Build(Session.State, _selectedId, _highlighted);
        OnPropertyChanged(nameof(Scene));
    }

    private void EnsureSelection()
    {
        if (_selectedId is not null && Session.State.Find(_selectedId) is null)
        {
            _selectedId = null;
        }
    }

    private void RefreshCommands()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        RotateCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        SchottkyCommand.NotifyCanExecuteChanged();
        StandardDiodeCommand.NotifyCanExecuteChanged();
    }

    private string TypeName(string id)
    {
        var type = Session.State.Find(id)?.Type;
        return type is not null && ComponentLibrary.TryGet(type, out var definition) && definition is not null
            ? definition.DisplayName
            : string.Empty;
    }
}

public sealed class LibraryItemViewModel
{
    public LibraryItemViewModel(string type, string name)
    {
        Type = type;
        Name = name;
    }

    public string Type { get; }

    public string Name { get; }
}

public sealed class ParamFieldViewModel : ObservableObject
{
    private readonly SchematicViewModel _owner;
    private readonly string _componentId;
    private string _text;
    private string? _error;
    private bool _closed;
    private bool _suspend;

    public ParamFieldViewModel(SchematicViewModel owner, string componentId, ParamDefinition definition, string text)
    {
        _owner = owner;
        _componentId = componentId;
        Name = definition.Name;
        Label = definition.Label;
        IsSwitch = definition.Kind == ParamKind.State;
        _text = IsSwitch ? string.Empty : text;
        _closed = text == "closed";
        SwitchLabel = _closed ? Strings.SwitchClosed : Strings.SwitchOpen;
    }

    public string Name { get; }

    public string Label { get; }

    public bool IsSwitch { get; }

    public bool IsText => !IsSwitch;

    public string SwitchLabel { get; private set; }

    public string Text
    {
        get => _text;
        set
        {
            if (_suspend || _text == value)
            {
                return;
            }

            var previous = _text;
            _text = value;
            OnPropertyChanged();
            if (_owner.TryCommit(_componentId, Name, value, out var error))
            {
                Error = null;
                return;
            }

            Error = error;
            _suspend = true;
            _text = previous;
            _suspend = false;
            OnPropertyChanged();
        }
    }

    public bool IsClosed
    {
        get => _closed;
        set
        {
            if (_suspend || _closed == value)
            {
                return;
            }

            _closed = value;
            SwitchLabel = value ? Strings.SwitchClosed : Strings.SwitchOpen;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SwitchLabel));
            _owner.SetSwitch(_componentId, value);
        }
    }

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }
}
