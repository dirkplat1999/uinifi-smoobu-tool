using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using UnifiSmoobuTool.App.Logging;
using UnifiSmoobuTool.App.Services;
using UnifiSmoobuTool.App.ViewModels;
using UnifiSmoobuTool.App.Views;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Services;
using UnifiSmoobuTool.Infrastructure;
using UnifiSmoobuTool.Infrastructure.Backup;
using UnifiSmoobuTool.Infrastructure.Notifications;
using UnifiSmoobuTool.Infrastructure.Persistence;
using UnifiSmoobuTool.Infrastructure.Security;
using UnifiSmoobuTool.Infrastructure.Smoobu;
using UnifiSmoobuTool.Infrastructure.UnifiAccess;
using UnifiSmoobuTool.Infrastructure.Updates;

namespace UnifiSmoobuTool.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnifiSmoobuTool");
        Directory.CreateDirectory(appDataDir);
        var dbPath = Path.Combine(appDataDir, "app.db");
        var logSink = new InMemoryLogSink();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.File(Path.Combine(appDataDir, "logs", "log-.txt"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .WriteTo.Sink(logSink)
            .CreateLogger();

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);

        builder.Services.AddSingleton(logSink);
        builder.Services.AddSingleton(new SqliteConnectionFactory(dbPath));
        builder.Services.AddSingleton<SecretProtector>();
        builder.Services.AddSingleton<IClock, SystemClock>();

        builder.Services.AddSingleton<IAppSettingsStore, SqliteAppSettingsStore>();
        builder.Services.AddSingleton<IReservationStateStore, SqliteReservationStateStore>();
        builder.Services.AddSingleton<IMessageTemplateStore, SqliteMessageTemplateStore>();
        builder.Services.AddSingleton<IApartmentMappingStore, SqliteApartmentMappingStore>();
        builder.Services.AddSingleton<IWebhookConfigStore, SqliteWebhookConfigStore>();
        builder.Services.AddSingleton<ITestModeRuleStore, SqliteTestModeRuleStore>();

        builder.Services.AddHttpClient<ISmoobuClient, SmoobuApiClient>();
        builder.Services.AddSingleton<IUnifiAccessClient, UnifiAccessApiClient>();
        builder.Services.AddHttpClient<IWebhookSender, HttpWebhookSender>();

        builder.Services.AddSingleton<WebhookDispatcher>();
        builder.Services.AddSingleton<SmtpAlerter>();
        builder.Services.AddSingleton<IErrorNotifier, CompositeErrorNotifier>();
        builder.Services.AddSingleton<BookingSyncOrchestrator>();
        builder.Services.AddSingleton<BackupService>();
        builder.Services.AddSingleton<UpdateChecker>();

        builder.Services.AddHostedService<SyncBackgroundService>();

        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<ApartmentsViewModel>();
        builder.Services.AddSingleton<TemplatesViewModel>();
        builder.Services.AddSingleton<WebhooksViewModel>();
        builder.Services.AddSingleton<TestModeViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<BackupViewModel>();
        builder.Services.AddSingleton<LogViewerViewModel>();

        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();

        try
        {
            var connectionFactory = _host.Services.GetRequiredService<SqliteConnectionFactory>();
            connectionFactory.EnsureSchema();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to initialize the local database.");
            System.Windows.MessageBox.Show(
                $"Couldn't set up the local database:\n{ex.Message}", "UniFi Smoobu Tool",
                MessageBoxButton.OK, MessageBoxImage.Error);
            System.Windows.Application.Current.Shutdown(-1);
            return;
        }

        _host.Start();

        if (NeedsLicenseAgreement(appDataDir))
        {
            var about = new AboutWindow(requireAgreement: true);
            about.ShowDialog();

            if (!about.UserAgreed)
            {
                Log.Information("User declined the license agreement on first launch; exiting.");
                _host.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
                _host.Dispose();
                Log.CloseAndFlush();
                System.Windows.Application.Current.Shutdown();
                return;
            }

            RecordLicenseAgreed(appDataDir);
        }

        SetupTrayIcon();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static bool NeedsLicenseAgreement(string appDataDir)
    {
        var marker = Path.Combine(appDataDir, ".license-agreed");
        return !File.Exists(marker);
    }

    private static void RecordLicenseAgreed(string appDataDir)
    {
        var marker = Path.Combine(appDataDir, ".license-agreed");
        File.WriteAllText(marker, DateTimeOffset.UtcNow.ToString("O"));
    }

    private void SetupTrayIcon()
    {
        var appIcon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location)
            ?? System.Drawing.SystemIcons.Application;

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = appIcon,
            Visible = true,
            Text = "UniFi Smoobu Tool",
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowMainWindow());
        menu.Items.Add("-");
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (MainWindow is null)
        {
            return;
        }

        MainWindow.Show();
        MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();

        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
