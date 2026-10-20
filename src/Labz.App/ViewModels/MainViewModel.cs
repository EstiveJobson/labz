using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Labz.App.Resources;
using Labz.Modules.Circuits;

namespace Labz.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly SchematicViewModel _schematic;
    private readonly PlaceholderViewModel _home;
    private readonly PlaceholderViewModel _simulation;
    private readonly PlaceholderViewModel _settings;
    private object? _currentPage;

    public MainViewModel()
        : this(SchematicSession.CreateBlank(Strings.NewProjectName, DateTimeOffset.Now), new WpfMainThread())
    {
    }

    public MainViewModel(SchematicSession session, Labz.Modules.Circuits.IMainThread mainThread)
    {
        Results = new ResultsViewModel(session, new LiveSolveController(mainThread));
        _schematic = new SchematicViewModel(session, Results);
        _home = new PlaceholderViewModel(Strings.NavHome, Strings.UnderConstruction);
        _simulation = new PlaceholderViewModel(Strings.NavSimulation, Strings.UnderConstruction);
        _settings = new PlaceholderViewModel(Strings.NavSettings, Strings.UnderConstruction);
        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new(Strings.NavHome),
            new(Strings.NavSchematic),
            new(Strings.NavSimulation),
            new(Strings.NavResults),
            new(Strings.NavSettings),
        };
        SelectSectionCommand = new RelayCommand<NavigationItem>(SelectSection);
        SelectSection(NavigationItems[1]);
    }

    public string WindowTitle => Strings.AppTitle;

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public RelayCommand<NavigationItem> SelectSectionCommand { get; }

    public SchematicViewModel Schematic => _schematic;

    public ResultsViewModel Results { get; }

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
            _ when ReferenceEquals(item, NavigationItems[0]) => _home,
            _ when ReferenceEquals(item, NavigationItems[1]) => _schematic,
            _ when ReferenceEquals(item, NavigationItems[2]) => _simulation,
            _ when ReferenceEquals(item, NavigationItems[3]) => Results,
            _ => _settings,
        };
    }
}
