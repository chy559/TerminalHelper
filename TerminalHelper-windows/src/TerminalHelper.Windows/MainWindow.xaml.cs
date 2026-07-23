using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using TerminalHelper.Core.Launching;
using TerminalHelper.Windows.Input;
using TerminalHelper.Windows.Presentation;
using WinRT.Interop;

namespace TerminalHelper.Windows;

public sealed partial class MainWindow : Window
{
    private readonly LatestInputGate latestDropInput = new();
    private bool isClosed;
    private XamlRoot? xamlRoot;

    public MainWindow(MainWindowViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        Title = "Terminal Helper";
        TrySetWindowIcon();

        DropRoot.Loaded += DropRoot_Loaded;
        ViewModel.StateChanged += ViewModel_StateChanged;
        Closed += MainWindow_Closed;
        Render();
    }

    public MainWindowViewModel ViewModel { get; }

    private void TrySetWindowIcon()
    {
        try
        {
            var windowHandle = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var iconPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "Assets", "TerminalHelper.ico"));
            appWindow.SetIcon(iconPath);
        }
        catch (Exception)
        {
            // The icon is cosmetic; a missing or unsupported icon must not prevent startup.
        }
    }

    private void DropRoot_Loaded(object sender, RoutedEventArgs args)
    {
        DropRoot.Loaded -= DropRoot_Loaded;
        if (isClosed || DropRoot.XamlRoot is not XamlRoot root)
        {
            return;
        }

        xamlRoot = root;
        xamlRoot.Changed += XamlRoot_Changed;
        ResizeWindow(xamlRoot.RasterizationScale);
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (!isClosed)
        {
            ResizeWindow(sender.RasterizationScale);
        }
    }

    private void ResizeWindow(double rasterizationScale)
    {
        var size = WindowSizeCalculator.ForRasterizationScale(rasterizationScale);
        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        AppWindow.GetFromWindowId(windowId).Resize(new SizeInt32(size.Width, size.Height));
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        isClosed = true;
        DropRoot.Loaded -= DropRoot_Loaded;
        if (xamlRoot is not null)
        {
            xamlRoot.Changed -= XamlRoot_Changed;
            xamlRoot = null;
        }

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
        if (ViewModel.HasError)
        {
            normalText.Visibility = Visibility.Collapsed;
            errorText.Visibility = Visibility.Visible;
            errorText.Text = ViewModel.StatusText;
        }
        else
        {
            errorText.Visibility = Visibility.Collapsed;
            normalText.Visibility = Visibility.Visible;
            normalText.Text = ViewModel.StatusText;
        }
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

        try
        {
            if (!args.DataView.Contains(StandardDataFormats.StorageItems))
            {
                return;
            }

            var inputGeneration = latestDropInput.BeginInput();
            var items = await args.DataView.GetStorageItemsAsync();
            if (isClosed)
            {
                return;
            }

            var storagePaths = items
                .Select(item => item switch
                {
                    StorageFolder folder => folder.Path,
                    StorageFile file => file.Path,
                    _ => null,
                })
                .ToArray();

            if (!isClosed)
            {
                latestDropInput.TryApply(
                    inputGeneration,
                    storagePaths,
                    paths => ViewModel.Receive(paths));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }
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
