using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Labz.App.ViewModels;

namespace Labz.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timer.Tick += (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.PulseAutosave();
            }
        };
        timer.Start();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.CommitFocusedEdits = CommitFocusedBindings;
            }
        };
        Closing += (_, args) =>
        {
            if (DataContext is MainViewModel viewModel && !viewModel.ConfirmClose())
            {
                args.Cancel = true;
            }
        };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is MainViewModel main && e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            main.Save();
            e.Handled = true;
            return;
        }

        if (e.OriginalSource is TextBox || DataContext is not MainViewModel host || host.CurrentPage is not SchematicViewModel schematic)
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

    private static void CommitFocusedBindings()
    {
        if (Keyboard.FocusedElement is TextBox textBox)
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
    }
}
