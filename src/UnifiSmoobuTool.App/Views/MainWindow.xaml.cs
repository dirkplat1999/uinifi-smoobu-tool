using System.ComponentModel;
using System.Windows;
using UnifiSmoobuTool.App.ViewModels;
using UnifiSmoobuTool.Infrastructure.Updates;

namespace UnifiSmoobuTool.App.Views;

public partial class MainWindow : Window
{
    private readonly UpdateChecker _updateChecker;
    private readonly SettingsViewModel _settingsViewModel;

    public MainWindow(
        DashboardViewModel dashboardViewModel,
        ApartmentsViewModel apartmentsViewModel,
        TemplatesViewModel templatesViewModel,
        WebhooksViewModel webhooksViewModel,
        TestModeViewModel testModeViewModel,
        SettingsViewModel settingsViewModel,
        BackupViewModel backupViewModel,
        LogViewerViewModel logViewerViewModel,
        UpdateChecker updateChecker)
    {
        InitializeComponent();
        _updateChecker = updateChecker;
        _settingsViewModel = settingsViewModel;

        DashboardViewControl.DataContext = dashboardViewModel;
        ApartmentsViewControl.DataContext = apartmentsViewModel;
        TemplatesViewControl.DataContext = templatesViewModel;
        WebhooksViewControl.DataContext = webhooksViewModel;
        TestModeViewControl.DataContext = testModeViewModel;
        SettingsViewControl.DataContext = settingsViewModel;
        BackupViewControl.DataContext = backupViewModel;
        LogViewerViewControl.DataContext = logViewerViewModel;

        Loaded += async (_, _) =>
        {
            await settingsViewModel.LoadCommand.ExecuteAsync(null);
            await dashboardViewModel.RefreshCommand.ExecuteAsync(null);
            await apartmentsViewModel.RefreshFromSmoobuCommand.ExecuteAsync(null);
            await templatesViewModel.LoadCommand.ExecuteAsync(null);
            await webhooksViewModel.LoadCommand.ExecuteAsync(null);
            await testModeViewModel.LoadCommand.ExecuteAsync(null);
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // By default, closing the window minimizes to the tray instead of exiting, so the
        // background sync loop keeps running. Use the tray icon's "Exit" item (or File > Exit) to
        // quit, or turn off "Keep running in the background" in Settings to make the X button
        // exit the app outright.
        if (_settingsViewModel.RunInBackgroundWhenClosed)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();

    private void UninstallMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var confirm = System.Windows.MessageBox.Show(
            "This removes UniFi Smoobu Tool's program files and shortcuts from this computer.\n\n" +
            "Your saved settings, message templates, and log files are kept on disk and are not " +
            "deleted automatically.\n\nContinue?",
            "Uninstall UniFi Smoobu Tool", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var result = _updateChecker.TriggerUninstall();
        if (!result.Started)
        {
            System.Windows.MessageBox.Show(
                result.Error ?? "Couldn't start the uninstaller.", "Uninstall", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        System.Windows.Application.Current.Shutdown();
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    private async void CheckForUpdatesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var result = await _updateChecker.CheckForUpdatesAsync();

        if (result.Status == UpdateCheckStatus.UpdateAvailable)
        {
            var notes = string.IsNullOrWhiteSpace(result.ReleaseNotes) ? "" : $"\n\nWhat's new:\n{result.ReleaseNotes}";
            var confirm = System.Windows.MessageBox.Show(
                $"A new version is available: {result.Detail}.{notes}\n\nDownload and install it now? The app will restart.",
                "Update available", MessageBoxButton.OKCancel, MessageBoxImage.Information);

            if (confirm == MessageBoxResult.OK)
            {
                await _updateChecker.DownloadAndApplyUpdateAsync();
            }

            return;
        }

        var message = result.Status switch
        {
            UpdateCheckStatus.UpToDate => "You're running the latest version.",
            UpdateCheckStatus.NotInstalled => "Auto-update is only available for installed (packaged) copies of the app.",
            _ => $"Couldn't check for updates: {result.Detail}",
        };
        System.Windows.MessageBox.Show(message, "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
