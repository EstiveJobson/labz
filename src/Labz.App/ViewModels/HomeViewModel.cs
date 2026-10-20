using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Labz.App.Resources;
using Labz.Core.Project;

namespace Labz.App.ViewModels;

public interface IProjectActions
{
    void NewProject();

    void OpenRef1();

    void OpenProject();

    void OpenRecent(string folder);

    void Save();

    void SaveAs();

    IReadOnlyList<RecentProject> LoadRecent();
}

public sealed class HomeViewModel : ObservableObject
{
    private readonly IProjectActions _actions;

    public HomeViewModel(IProjectActions actions)
    {
        _actions = actions;
        Recent = [];
        NewCommand = new RelayCommand(actions.NewProject);
        OpenCommand = new RelayCommand(actions.OpenProject);
        OpenRef1Command = new RelayCommand(actions.OpenRef1);
        SaveCommand = new RelayCommand(actions.Save);
        SaveAsCommand = new RelayCommand(actions.SaveAs);
        OpenRecentCommand = new RelayCommand<string>(folder =>
        {
            if (!string.IsNullOrWhiteSpace(folder))
            {
                actions.OpenRecent(folder);
            }
        });
        Reload();
    }

    public ObservableCollection<RecentProject> Recent { get; }

    public RelayCommand NewCommand { get; }

    public RelayCommand OpenCommand { get; }

    public RelayCommand OpenRef1Command { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand SaveAsCommand { get; }

    public RelayCommand<string> OpenRecentCommand { get; }

    public string NewProjectLabel => Strings.NewProject;

    public string OpenLabel => Strings.OpenProject;

    public string OpenRef1Label => Strings.OpenRef1;

    public string SaveLabel => Strings.Save;

    public string SaveAsLabel => Strings.SaveAs;

    public string NewFromIdeaLabel => Strings.NewFromIdea;

    public string IdeaHint => Strings.IdeaHint;

    public string RecentHeader => Strings.RecentProjects;

    public string EmptyRecent => Strings.NoRecentProjects;

    public bool IdeaEnabled => false;

    public bool HasRecent => Recent.Count > 0;

    public void Reload()
    {
        Recent.Clear();
        foreach (var project in _actions.LoadRecent())
        {
            Recent.Add(project);
        }

        OnPropertyChanged(nameof(HasRecent));
    }
}
