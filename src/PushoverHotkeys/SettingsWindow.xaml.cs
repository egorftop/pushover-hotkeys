using System.Windows;
using PushoverHotkeys.Models;
using MessageBox = System.Windows.MessageBox;

namespace PushoverHotkeys;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _original;

    public SettingsWindow(AppSettings currentSettings)
    {
        _original = currentSettings.DeepCopy();
        InitializeComponent();
        AppTokenBox.Password = _original.AppToken;
        AutostartCheckBox.IsChecked = _original.StartWithWindows;
    }

    public AppSettings? Settings { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs eventArgs)
    {
        var token = AppTokenBox.Password.Trim();
        if (!PushoverKeyValidator.IsValid(token))
        {
            MessageBox.Show(
                "App Token должен состоять из 30 латинских букв и цифр.",
                "Проверьте App Token",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _original.AppToken = token;
        _original.StartWithWindows = AutostartCheckBox.IsChecked == true;
        Settings = _original;
        DialogResult = true;
    }
}
