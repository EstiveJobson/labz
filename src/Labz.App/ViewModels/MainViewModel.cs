using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Labz.App.Resources;
using Labz.Core.Project;
using Labz.Modules.Circuits;

namespace Labz.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IProjectActions
{
    private readonly IMainThread _thread;
    private readonly IUserDialogs _dialogs;
    private readonly string _recentPath;
    private readonly TimeProvider _clock;
    private readonly AutosavePolicy _autosave;
    private LiveSolveController _calculator;
    private SchematicViewModel _schematic;
    private readonly PlaceholderViewModel _simulation;
    private object? _currentPage;
    private bool _dirty;

    public MainViewModel()
        : this(
            SchematicSession.CreateBlank(Strings.NewProjectName, DateTimeOffset.Now),
            new WpfMainThread(),
            new WpfUserDialogs(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Strings.AppTitle, "recent.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Strings.AppTitle, "settings.json"),
            TimeProvider.System)
    {
    }

    public MainViewModel(
        SchematicSession session,
        IMainThread thread,
        IUserDialogs dialogs,
        string recentPath,
        string settingsPath,
        TimeProvider clock)
    {
        _thread = thread;
        _dialogs = dialogs;
        _recentPath = recentPath;
        _clock = clock;
        SettingsPath = settingsPath;
        Settings = UserSettingsStore.Load(settingsPath);
        _autosave = new AutosavePolicy(clock, () => Settings.AutosaveIntervalMinutes);
        _simulation = new PlaceholderViewModel(Strings.NavSimulation, Strings.UnderConstruction);
        _calculator = new LiveSolveController(thread);
        Results = new ResultsViewModel(session, _calculator, () => Settings.SignificantDigits);
        _schematic = new SchematicViewModel(session, Results);
        Home = new HomeViewModel(this);
        Preferences = new SettingsViewModel(Settings, settingsPath, () => Results.SignificantDigits = Settings.SignificantDigits);
        Session = session;
        session.Changed += OnSessionChanged;
        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new(Strings.NavHome),
            new(Strings.NavSchematic),
            new(Strings.NavSimulation),
            new(Strings.NavResults),
            new(Strings.NavSettings),
        };
        SelectSectionCommand = new RelayCommand<NavigationItem>(SelectSection);
        SelectSection(NavigationItems[0]);
    }

    public UserSettings Settings { get; }

    public string SettingsPath { get; }

    public SchematicSession Session { get; private set; }

    public string? Folder { get; private set; }

    public HomeViewModel Home { get; }

    public SettingsViewModel Preferences { get; }

    public SchematicViewModel Schematic => _schematic;

    public ResultsViewModel Results { get; private set; }

    public bool IsDirty
    {
        get => _dirty;
        private set
        {
            if (SetProperty(ref _dirty, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public string WindowTitle => Strings.AppTitle + " — " + Session.Name + (IsDirty ? "*" : string.Empty);

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public RelayCommand<NavigationItem> SelectSectionCommand { get; }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public void SelectSection(NavigationItem? item)
    {
        if (item is null)
        {
            return;
        }

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsSelected = ReferenceEquals(navigationItem, item);
        }

        CurrentPage = item.Title switch
        {
            _ when ReferenceEquals(item, NavigationItems[0]) => Home,
            _ when ReferenceEquals(item, NavigationItems[1]) => _schematic,
            _ when ReferenceEquals(item, NavigationItems[2]) => _simulation,
            _ when ReferenceEquals(item, NavigationItems[3]) => Results,
            _ => Preferences,
        };
    }

    public void NewProject()
    {
        if (!ConfirmDiscard())
        {
            return;
        }

        UseSession(SchematicSession.CreateBlank(Strings.NewProjectName, _clock.GetUtcNow()), null, false);
    }

    public void OpenRef1()
    {
        if (!ConfirmDiscard())
        {
            return;
        }

        var document = Ref1Sample.Load();
        document.Id = Guid.NewGuid().ToString("D");
        document.CreatedAt = _clock.GetUtcNow();
        UseSession(SchematicSession.FromDocument(document, _clock), null, true);
        SelectSection(NavigationItems[1]);
    }

    public void OpenProject()
    {
        var folder = _dialogs.ChooseFolder(Strings.ChooseFolder);
        if (folder is not null)
        {
            OpenRecent(folder);
        }
    }

    public void OpenRecent(string folder)
    {
        if (!ConfirmDiscard())
        {
            return;
        }

        var opened = ProjectStore.Open(folder);
        if (opened.Document is null)
        {
            _dialogs.Alert(opened.Error ?? Strings.FixSchematicMessage);
            return;
        }

        UseSession(SchematicSession.FromDocument(opened.Document, _clock), folder, false);
        Remember();
        SelectSection(NavigationItems[1]);
    }

    public void Save()
    {
        if (Folder is null)
        {
            SaveAs();
            return;
        }

        Write(Folder);
    }

    public void SaveAs()
    {
        var folder = _dialogs.ChooseFolder(Strings.ChooseFolder);
        if (folder is null)
        {
            return;
        }

        Write(folder);
    }

    public bool ConfirmClose() => ConfirmDiscard();

    public bool PulseAutosave()
    {
        if (Folder is null || !IsDirty || !_autosave.IsDue())
        {
            return false;
        }

        SnapshotStore.Write(Folder, Session.ToDocument(), _clock.GetUtcNow());
        _autosave.MarkSaved();
        return true;
    }

    public IReadOnlyList<RecentProject> LoadRecent() => RecentProjectStore.Load(_recentPath);

    public void SelectSection(int index) => SelectSection(NavigationItems[index]);

    private void Write(string folder)
    {
        ProjectStore.Save(folder, Session.ToDocument());
        Folder = folder;
        IsDirty = false;
        _autosave.MarkSaved();
        Remember();
        Home.Reload();
        OnPropertyChanged(nameof(WindowTitle));
    }

    private void Remember()
    {
        if (Folder is null)
        {
            return;
        }

        RecentProjectStore.Remember(_recentPath, Folder, Session.Name, _clock.GetUtcNow());
        Home.Reload();
    }

    private bool ConfirmDiscard()
    {
        if (!IsDirty)
        {
            return true;
        }

        return _dialogs.Confirm(Strings.DiscardChanges);
    }

    private void UseSession(SchematicSession session, string? folder, bool dirty)
    {
        _calculator.Dispose();
        _calculator = new LiveSolveController(_thread);
        Session = session;
        Folder = folder;
        Results = new ResultsViewModel(session, _calculator, () => Settings.SignificantDigits);
        _schematic = new SchematicViewModel(session, Results);
        session.Changed += OnSessionChanged;
        IsDirty = dirty;
        OnPropertyChanged(nameof(Schematic));
        OnPropertyChanged(nameof(Results));
        OnPropertyChanged(nameof(WindowTitle));
        if (ReferenceEquals(CurrentPage, NavigationItems[1]) || CurrentPage is SchematicViewModel)
        {
            CurrentPage = _schematic;
        }
        else if (CurrentPage is ResultsViewModel)
        {
            CurrentPage = Results;
        }
    }

    private void OnSessionChanged(bool _)
    {
        IsDirty = true;
    }
}
