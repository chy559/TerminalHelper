using Microsoft.UI.Xaml;
using TerminalHelper.Core.Folders;
using TerminalHelper.Core.Launching;
using TerminalHelper.Windows.Input;
using TerminalHelper.Windows.Presentation;
using TerminalHelper.WindowsPlatform.Discovery;
using TerminalHelper.WindowsPlatform.Launching;

namespace TerminalHelper.Windows;

public partial class App : Application
{
    private Window? window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var resolver = new WindowsExecutableResolver(
            new SystemWindowsEnvironment(),
            new SystemWindowsFileSystem(),
            new WindowsRegistryReader());
        var requestFactory = new WindowsLaunchRequestFactory();
        var launcher = new WindowsWorkspaceLauncher(
            resolver,
            requestFactory,
            new SystemProcessRunner());
        var planner = new FolderBatchPlanner(new SystemFolderPathService());
        var coordinator = new WorkspaceOpenCoordinator(planner, launcher);
        var viewModel = new MainWindowViewModel(coordinator);

        window = new MainWindow(viewModel);
        window.Activate();

        viewModel.Receive(StartupPathReader.Read(Environment.GetCommandLineArgs()));
    }
}
