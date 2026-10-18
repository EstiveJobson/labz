using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Labz.App.Resources;

namespace Labz.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private object? _currentPage;

    public MainViewModel()
    {
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

    public string WindowTitle => Strings.AppTitle;

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public RelayCommand<NavigationItem> SelectSectionCommand { get; }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    private void SelectSection(NavigationItem? item)
    {
        if (item is null)
        {
            return;
        }

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsSelected = ReferenceEquals(navigationItem, item);
        }

        CurrentPage = new PlaceholderViewModel(item.Title, Strings.UnderConstruction);
    }
}
