using System.Windows;
using Labz.App.Resources;
using Microsoft.Win32;

namespace Labz.App;

public sealed class WpfUserDialogs : IUserDialogs
{
    public bool Confirm(string message)
    {
        return MessageBox.Show(message, Strings.AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public string? ChooseFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public void Alert(string message)
    {
        MessageBox.Show(message, Strings.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
