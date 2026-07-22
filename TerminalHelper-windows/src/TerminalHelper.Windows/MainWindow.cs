using Microsoft.UI.Xaml;
using TerminalHelper.Windows.Presentation;

namespace TerminalHelper.Windows;

// Task 6 replaces this launchable placeholder with the complete XAML window.
public sealed class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Title = "Terminal Helper";
    }

    public MainWindowViewModel ViewModel { get; }
}
