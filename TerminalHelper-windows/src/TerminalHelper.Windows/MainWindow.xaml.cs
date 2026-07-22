using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using TerminalHelper.Core.Launching;
using TerminalHelper.Windows.Presentation;
using WinRT.Interop;

namespace TerminalHelper.Windows;

public sealed partial class MainWindow : Window
{
    private bool isClosed;

    public MainWindow(MainWindowViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        Title = "Terminal Helper";
        ResizeWindow();

        ViewModel.StateChanged += ViewModel_StateChanged;
        Closed += MainWindow_Closed;
        Render();
    }

    public MainWindowViewModel ViewModel { get; }

    private void ResizeWindow()
    {
        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        AppWindow.GetFromWindowId(windowId).Resize(new SizeInt32(500, 440));
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        isClosed = true;
        ViewModel.StateChanged -= ViewModel_StateChanged;
        Closed -= MainWindow_Closed;
    }

    private void ViewModel_StateChanged(object? sender, EventArgs args)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (!isClosed)
            {
                Render();
            }
        });
    }

    private void Render()
    {
        var hasSelection = ViewModel.HasSelection;
        EmptyState.Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
        SelectedState.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        SelectionCountText.Text = $"已选择 {ViewModel.SelectedFolderCount} 个文件夹";

        RenderStatus(EmptyStatusText, EmptyErrorStatusText);
        RenderStatus(SelectedStatusText, SelectedErrorStatusText);

        RenderTarget(
            ViewModel.TargetOptions[0],
            TerminalButton,
            TerminalProgress,
            TerminalUnavailable,
            TerminalChevron);
        RenderTarget(
            ViewModel.TargetOptions[1],
            VisualStudioCodeButton,
            VisualStudioCodeProgress,
            VisualStudioCodeUnavailable,
            VisualStudioCodeChevron);
        RenderTarget(
            ViewModel.TargetOptions[2],
            IntelliJIdeaButton,
            IntelliJIdeaProgress,
            IntelliJIdeaUnavailable,
            IntelliJIdeaChevron);

        ResetButton.IsEnabled = !ViewModel.IsLaunching;
    }

    private void RenderStatus(TextBlock normalText, TextBlock errorText)
    {
        normalText.Text = ViewModel.StatusText;
        errorText.Text = ViewModel.StatusText;
        normalText.Visibility = ViewModel.HasError ? Visibility.Collapsed : Visibility.Visible;
        errorText.Visibility = ViewModel.HasError ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderTarget(
        TargetOptionViewModel option,
        Button button,
        ProgressRing progress,
        TextBlock unavailable,
        FontIcon chevron)
    {
        button.IsEnabled = option.CanLaunch;
        AutomationProperties.SetName(
            button,
            option.IsAvailable ? option.DisplayName : $"{option.DisplayName}，未安装");

        progress.IsActive = option.IsLaunching;
        progress.Visibility = option.IsLaunching ? Visibility.Visible : Visibility.Collapsed;
        unavailable.Visibility = option.IsAvailable ? Visibility.Collapsed : Visibility.Visible;
        chevron.Visibility = option.IsAvailable && !option.IsLaunching
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void DropRoot_DragOver(object sender, DragEventArgs args)
    {
        var hasStorageItems = args.DataView.Contains(StandardDataFormats.StorageItems);
        args.AcceptedOperation = hasStorageItems
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
        SetDropTargeted(hasStorageItems);
        args.Handled = true;
    }

    private void DropRoot_DragLeave(object sender, DragEventArgs args)
    {
        SetDropTargeted(false);
        args.Handled = true;
    }

    private async void DropRoot_Drop(object sender, DragEventArgs args)
    {
        SetDropTargeted(false);
        args.Handled = true;

        if (!args.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await args.DataView.GetStorageItemsAsync();
        if (isClosed)
        {
            return;
        }

        ViewModel.Receive(items
            .OfType<StorageFolder>()
            .Select(folder => folder.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path)));
    }

    private void SetDropTargeted(bool isTargeted)
    {
        DropTargetHighlight.Visibility = isTargeted ? Visibility.Visible : Visibility.Collapsed;
        DropTargetOutline.Visibility = isTargeted ? Visibility.Visible : Visibility.Collapsed;
        DropOutline.Visibility = isTargeted ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void TerminalButton_Click(object sender, RoutedEventArgs args)
    {
        await ViewModel.LaunchAsync(WorkspaceTarget.Terminal);
    }

    private async void VisualStudioCodeButton_Click(object sender, RoutedEventArgs args)
    {
        await ViewModel.LaunchAsync(WorkspaceTarget.VisualStudioCode);
    }

    private async void IntelliJIdeaButton_Click(object sender, RoutedEventArgs args)
    {
        await ViewModel.LaunchAsync(WorkspaceTarget.IntelliJIdea);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs args)
    {
        ViewModel.Reset();
    }
}
