using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Labz.App.ViewModels;

namespace Labz.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox || DataContext is not MainViewModel main || main.CurrentPage is not SchematicViewModel schematic)
        {
            return;
        }

        if (e.Key == Key.Delete)
        {
            schematic.DeleteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.None)
        {
            schematic.RotateSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            schematic.CancelTool();
            e.Handled = true;
        }
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            schematic.Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
        {
            schematic.Redo();
            e.Handled = true;
        }
    }
}
