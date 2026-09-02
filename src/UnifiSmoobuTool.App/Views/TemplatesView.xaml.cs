using System.Windows;
using UnifiSmoobuTool.App.ViewModels;

namespace UnifiSmoobuTool.App.Views;

public partial class TemplatesView : System.Windows.Controls.UserControl
{
    public TemplatesView()
    {
        InitializeComponent();
    }

    private async void ResetToDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = System.Windows.MessageBox.Show(
            "This replaces every template (including any custom edits or extra languages you've added) " +
            "with the built-in English/Dutch/German/French defaults. This can't be undone.\n\nContinue?",
            "Reset templates to defaults", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        if (DataContext is TemplatesViewModel viewModel)
        {
            await viewModel.ResetToDefaultsCommand.ExecuteAsync(null);
        }
    }
}
