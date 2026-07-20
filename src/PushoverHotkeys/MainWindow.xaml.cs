using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PushoverHotkeys.Models;
using PushoverHotkeys.Services;
using MessageBox = System.Windows.MessageBox;

namespace PushoverHotkeys;

public partial class MainWindow : Window
{
    private readonly App _app;
    private bool _isRefreshing;
    private bool _allowClose;

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();
        _app.StateChanged += (_, _) => Dispatcher.Invoke(Refresh);
        Refresh();
    }

    public void AllowClose() => _allowClose = true;

    private void Refresh()
    {
        _isRefreshing = true;
        try
        {
            StatusText.Text = _app.RuntimeStatus;
            HotkeysEnabledCheckBox.IsChecked = _app.Settings.HotkeysEnabled;
            BindingsGrid.ItemsSource = _app.Settings.Bindings
                .Select(binding => new BindingRow(
                    binding.Id,
                    binding.Chord.ToString(),
                    binding.Message,
                    $"{PriorityDisplay(binding.Priority)}; {PushoverSounds.DisplayName(binding.Sound)}",
                    string.Join("; ", binding.Recipients.Select(recipient => recipient.DisplayName)),
                    binding.Recipients.Count))
                .ToList();
            UpdateSelectionButtons();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void UpdateSelectionButtons()
    {
        var selected = SelectedRow is not null;
        EditButton.IsEnabled = selected;
        DeleteButton.IsEnabled = selected;
        SendButton.IsEnabled = selected;
    }

    private BindingRow? SelectedRow => BindingsGrid.SelectedItem as BindingRow;

    private async void HotkeysEnabledCheckBox_Changed(object sender, RoutedEventArgs eventArgs)
    {
        if (_isRefreshing || HotkeysEnabledCheckBox.IsChecked is not bool isEnabled || _app.Settings.HotkeysEnabled == isEnabled)
        {
            return;
        }

        var updated = _app.Settings.DeepCopy();
        updated.HotkeysEnabled = isEnabled;
        await _app.ReplaceSettingsAsync(updated);
    }

    private async void AddBinding_Click(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new BindingDialog();
        if (dialog.ShowDialog() != true || dialog.Binding is null)
        {
            return;
        }

        var updated = _app.Settings.DeepCopy();
        try
        {
            BindingMerger.Upsert(updated.Bindings, dialog.Binding);
        }
        catch (ArgumentException exception)
        {
            ShowValidationError(exception.Message);
            return;
        }

        await _app.ReplaceSettingsAsync(updated);
    }

    private async void EditBinding_Click(object sender, RoutedEventArgs eventArgs)
    {
        var selected = SelectedRow;
        if (selected is null)
        {
            return;
        }

        var binding = _app.Settings.Bindings.FirstOrDefault(item => item.Id == selected.Id);
        if (binding is null)
        {
            return;
        }

        var dialog = new BindingDialog(binding);
        if (dialog.ShowDialog() != true || dialog.Binding is null)
        {
            return;
        }

        var updated = _app.Settings.DeepCopy();
        try
        {
            BindingMerger.Upsert(updated.Bindings, dialog.Binding);
        }
        catch (ArgumentException exception)
        {
            ShowValidationError(exception.Message);
            return;
        }

        await _app.ReplaceSettingsAsync(updated);
    }

    private async void DeleteBinding_Click(object sender, RoutedEventArgs eventArgs)
    {
        var selected = SelectedRow;
        if (selected is null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Удалить привязку «{selected.ChordText}»?",
            "Удаление привязки",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var updated = _app.Settings.DeepCopy();
        updated.Bindings.RemoveAll(binding => binding.Id == selected.Id);
        await _app.ReplaceSettingsAsync(updated);
    }

    private async void SendBinding_Click(object sender, RoutedEventArgs eventArgs)
    {
        var selected = SelectedRow;
        if (selected is null)
        {
            return;
        }

        SendButton.IsEnabled = false;
        try
        {
            var result = await _app.SendBindingAsync(selected.Id);
            MessageBox.Show(
                result.IsSuccess ? "Сообщение успешно отправлено." : result.ErrorMessage,
                result.IsSuccess ? "Pushover" : "Ошибка отправки",
                MessageBoxButton.OK,
                result.IsSuccess ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        finally
        {
            UpdateSelectionButtons();
        }
    }

    private async void Settings_Click(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new SettingsWindow(_app.Settings) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Settings is null)
        {
            return;
        }

        await _app.ReplaceSettingsAsync(dialog.Settings);
    }

    private void BindingsGrid_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs) => UpdateSelectionButtons();

    private void Window_Closing(object? sender, CancelEventArgs eventArgs)
    {
        if (_allowClose || _app.IsExiting)
        {
            return;
        }

        eventArgs.Cancel = true;
        Hide();
    }

    private void ShowValidationError(string message) => MessageBox.Show(message, "Проверьте привязку", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static string PriorityDisplay(int priority) => priority switch
    {
        -2 => "Самая низкая",
        -1 => "Низкая",
        1 => "Высокая",
        _ => "Обычная"
    };

    private sealed record BindingRow(
        Guid Id,
        string ChordText,
        string Message,
        string DeliveryText,
        string RecipientsText,
        int RecipientCount);
}
