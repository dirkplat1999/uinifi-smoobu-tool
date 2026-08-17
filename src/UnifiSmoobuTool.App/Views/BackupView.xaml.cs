using System.Windows;
using System.Windows.Controls;
using UnifiSmoobuTool.App.ViewModels;

namespace UnifiSmoobuTool.App.Views;

public partial class BackupView : System.Windows.Controls.UserControl
{
    public BackupView()
    {
        InitializeComponent();
    }

    private void ExportPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is BackupViewModel vm)
        {
            vm.ExportPassphrase = ExportPasswordBox.Password;
        }
    }

    private void ImportPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is BackupViewModel vm)
        {
            vm.ImportPassphrase = ImportPasswordBox.Password;
        }
    }
}
