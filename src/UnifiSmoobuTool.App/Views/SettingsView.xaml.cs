using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using UnifiSmoobuTool.App.ViewModels;

namespace UnifiSmoobuTool.App.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    private bool _suppressPasswordSync;

    public SettingsView()
    {
        InitializeComponent();

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is SettingsViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is SettingsViewModel newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
                SyncPasswordBoxFromViewModel(newVm);
            }
        };
    }

    // PasswordBox.Password is intentionally not a bindable DependencyProperty (security), so it's
    // kept in sync with the view model manually in both directions.
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SmtpPassword) && sender is SettingsViewModel vm)
        {
            SyncPasswordBoxFromViewModel(vm);
        }
    }

    private void SyncPasswordBoxFromViewModel(SettingsViewModel vm)
    {
        if (SmtpPasswordBox.Password == vm.SmtpPassword)
        {
            return;
        }

        _suppressPasswordSync = true;
        SmtpPasswordBox.Password = vm.SmtpPassword;
        _suppressPasswordSync = false;
    }

    private void SmtpPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressPasswordSync)
        {
            return;
        }

        if (DataContext is SettingsViewModel vm)
        {
            vm.SmtpPassword = SmtpPasswordBox.Password;
        }
    }
}
