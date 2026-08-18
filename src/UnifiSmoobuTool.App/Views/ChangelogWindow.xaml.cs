using System.IO;
using System.Windows;

namespace UnifiSmoobuTool.App.Views;

public partial class ChangelogWindow : Window
{
    public ChangelogWindow()
    {
        InitializeComponent();

        var path = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
        ChangelogText.Text = File.Exists(path)
            ? File.ReadAllText(path)
            : "Couldn't find CHANGELOG.md next to the application - it may be missing from this install.";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
