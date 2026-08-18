using System.Reflection;
using System.Windows;

namespace UnifiSmoobuTool.App.Views;

public partial class AboutWindow : Window
{
    private readonly bool _requireAgreement;

    /// <summary>True once the user has clicked "I Agree" in agreement mode. Always true when the
    /// window wasn't shown in agreement mode (nothing to agree to).</summary>
    public bool UserAgreed { get; private set; }

    public AboutWindow(bool requireAgreement = false)
    {
        InitializeComponent();
        _requireAgreement = requireAgreement;
        UserAgreed = !requireAgreement;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Version {version?.ToString(3) ?? "dev"}";

        if (requireAgreement)
        {
            Title = "Welcome - License Agreement";
            HeadingText.Text = "Welcome to UniFi Access - Smoobu Guest Access Tool";
            AgreementPromptText.Visibility = Visibility.Visible;
            CloseButton.Visibility = Visibility.Collapsed;
            DeclineButton.Visibility = Visibility.Visible;
            AgreeButton.Visibility = Visibility.Visible;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void AgreeButton_Click(object sender, RoutedEventArgs e)
    {
        UserAgreed = true;
        Close();
    }

    private void DeclineButton_Click(object sender, RoutedEventArgs e)
    {
        UserAgreed = false;
        Close();
    }
}
