using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnifiSmoobuTool.Infrastructure.Backup;

namespace UnifiSmoobuTool.App.ViewModels;

/// <summary>Backs the Backup screen (Feature 11): export/import application settings and message
/// templates as a zip bundle, with credentials optionally AES-encrypted behind a passphrase.</summary>
public sealed partial class BackupViewModel : ObservableObject
{
    private readonly BackupService _backupService;

    [ObservableProperty]
    private bool _includeCredentials;

    [ObservableProperty]
    private string _exportPassphrase = "";

    [ObservableProperty]
    private string _importPassphrase = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private BackupPreview? _pendingImportPreview;

    [ObservableProperty]
    private string? _pendingImportFilePath;

    public BackupViewModel(BackupService backupService)
    {
        _backupService = backupService;
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export configuration backup",
            Filter = "UniFi Smoobu Tool backup (*.usbackup)|*.usbackup",
            FileName = $"unifi-smoobu-backup-{DateTime.Now:yyyy-MM-dd}.usbackup",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (IncludeCredentials && string.IsNullOrWhiteSpace(ExportPassphrase))
        {
            StatusMessage = "Enter a passphrase to include credentials in the backup.";
            return;
        }

        IsBusy = true;
        try
        {
            await _backupService.ExportAsync(dialog.FileName, IncludeCredentials, IncludeCredentials ? ExportPassphrase : null);
            StatusMessage = $"Backup saved to {dialog.FileName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ChooseImportFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose a backup to restore",
            Filter = "UniFi Smoobu Tool backup (*.usbackup)|*.usbackup|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsBusy = true;
        try
        {
            PendingImportPreview = await _backupService.PreviewImportAsync(dialog.FileName);
            PendingImportFilePath = dialog.FileName;
            StatusMessage = "Review the backup contents below, then click Import to apply.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't read backup: {ex.Message}";
            PendingImportPreview = null;
            PendingImportFilePath = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (PendingImportFilePath is null)
        {
            return;
        }

        if (PendingImportPreview?.HasEncryptedSecrets == true && string.IsNullOrWhiteSpace(ImportPassphrase))
        {
            StatusMessage = "This backup includes encrypted credentials; enter the passphrase first.";
            return;
        }

        IsBusy = true;
        try
        {
            var passphrase = PendingImportPreview?.HasEncryptedSecrets == true ? ImportPassphrase : null;
            await _backupService.ImportAsync(PendingImportFilePath, passphrase);
            StatusMessage = "Backup restored. Revisit the other tabs to see the restored configuration.";
            PendingImportPreview = null;
            PendingImportFilePath = null;
            ImportPassphrase = "";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
