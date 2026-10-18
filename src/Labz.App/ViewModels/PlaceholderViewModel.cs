using CommunityToolkit.Mvvm.ComponentModel;

namespace Labz.App.ViewModels;

public sealed class PlaceholderViewModel : ObservableObject
{
    public PlaceholderViewModel(string title, string message)
    {
        Title = title;
        Message = message;
    }

    public string Title { get; }

    public string Message { get; }
}
