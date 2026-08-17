using System.Reflection;
using System.Windows;

namespace UnifiSmoobuTool.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Version {version?.ToString(3) ?? "dev"}";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
