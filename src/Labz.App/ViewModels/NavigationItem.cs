using CommunityToolkit.Mvvm.ComponentModel;

namespace Labz.App.ViewModels;

public sealed class NavigationItem : ObservableObject
{
    private bool _isSelected;

    public NavigationItem(string title)
    {
        Title = title;
    }

    public string Title { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
