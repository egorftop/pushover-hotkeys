using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using PushoverHotkeys.Models;
using MessageBox = System.Windows.MessageBox;

namespace PushoverHotkeys;

public partial class BindingDialog : Window
{
    private readonly ObservableCollection<Recipient> _recipients = [];
    private Guid _id = Guid.NewGuid();
    private KeyChord _chord = new();
    private bool _isCapturing;
    private Key? _pendingModifier;
    private static readonly IReadOnlyList<PriorityOption> PriorityOptions =
    [
        new(-2, "Самая низкая — без уведомления"),
        new(-1, "Низкая — без звука и вибрации"),
        new(0, "Обычная"),
        new(1, "Высокая — обходит тихие часы")
    ];

    public BindingDialog(HotkeyBinding? existingBinding = null)
    {
        InitializeComponent();
        Owner = System.Windows.Application.Current.MainWindow;
        RecipientsList.ItemsSource = _recipients;

        PriorityComboBox.ItemsSource = PriorityOptions;
        PriorityComboBox.DisplayMemberPath = nameof(PriorityOption.Name);
        PriorityComboBox.SelectedValuePath = nameof(PriorityOption.Value);
        SoundComboBox.ItemsSource = PushoverSounds.Options;
        SoundComboBox.DisplayMemberPath = nameof(PushoverSoundOption.Name);
        SoundComboBox.SelectedValuePath = nameof(PushoverSoundOption.Id);

        if (existingBinding is not null)
        {
            _id = existingBinding.Id;
            _chord = existingBinding.Chord.DeepCopy();
            foreach (var recipient in existingBinding.Recipients)
            {
                _recipients.Add(recipient.DeepCopy());
            }

            MessageTextBox.Text = string.IsNullOrWhiteSpace(existingBinding.Message) ? "GM" : existingBinding.Message;
            PriorityComboBox.SelectedValue = Enum.IsDefined(typeof(PushoverPriority), existingBinding.Priority)
                ? existingBinding.Priority
                : (int)PushoverPriority.Normal;
            SoundComboBox.SelectedValue = PushoverSounds.IsValid(existingBinding.Sound)
                ? existingBinding.Sound
                : PushoverSounds.DefaultId;
        }
        else
        {
            MessageTextBox.Text = "GM";
            PriorityComboBox.SelectedValue = (int)PushoverPriority.Normal;
            SoundComboBox.SelectedValue = PushoverSounds.DefaultId;
        }

        UpdateChordDisplay();
    }

    public HotkeyBinding? Binding { get; private set; }

    private void Capture_Click(object sender, RoutedEventArgs eventArgs)
    {
        _isCapturing = true;
        _pendingModifier = null;
        CaptureButton.IsEnabled = false;
        CaptureHintText.Text = "Нажмите клавишу или сочетание. Кнопка «Отмена» внизу закрывает окно без сохранения.";
        Focus();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (!_isCapturing)
        {
            return;
        }

        var key = eventArgs.Key == Key.System ? eventArgs.SystemKey : eventArgs.Key;
        if (IsModifierKey(key))
        {
            _pendingModifier = key;
            eventArgs.Handled = true;
            CaptureHintText.Text = $"Удерживайте {key} и нажмите основную клавишу для сочетания. Отпустите {key}, чтобы назначить его отдельно.";
            return;
        }

        var chord = KeyChord.FromWpfKey(key, Keyboard.Modifiers);
        if (!chord.IsValid)
        {
            return;
        }

        eventArgs.Handled = true;
        _chord = chord;
        _isCapturing = false;
        CaptureButton.IsEnabled = true;
        CaptureHintText.Text = "Клавиша записана. Добавьте одного или нескольких получателей и сохраните привязку.";
        UpdateChordDisplay();
    }

    private void Window_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (!_isCapturing || _pendingModifier is null)
        {
            return;
        }

        var key = eventArgs.Key == Key.System ? eventArgs.SystemKey : eventArgs.Key;
        if (key != _pendingModifier)
        {
            return;
        }

        eventArgs.Handled = true;
        _chord = KeyChord.FromWpfKey(key, ModifierKeys.None);
        _pendingModifier = null;
        _isCapturing = false;
        CaptureButton.IsEnabled = true;
        CaptureHintText.Text = "Клавиша записана. Добавьте одного или нескольких получателей и сохраните привязку.";
        UpdateChordDisplay();
    }

    private void AddRecipient_Click(object sender, RoutedEventArgs eventArgs)
    {
        var key = RecipientKeyBox.Text.Trim();
        if (!PushoverKeyValidator.IsValid(key))
        {
            ShowWarning("Pushover Key должен состоять из 30 латинских букв и цифр.");
            return;
        }

        if (_recipients.Any(recipient => string.Equals(recipient.UserKey, key, StringComparison.Ordinal)))
        {
            ShowWarning("Этот Pushover Key уже добавлен к привязке.");
            return;
        }

        if (_recipients.Count == 50)
        {
            ShowWarning("К одной горячей клавише можно назначить не более 50 получателей.");
            return;
        }

        _recipients.Add(new Recipient { Name = RecipientNameBox.Text.Trim(), UserKey = key });
        RecipientNameBox.Clear();
        RecipientKeyBox.Clear();
    }

    private void RemoveRecipient_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (RecipientsList.SelectedItem is Recipient recipient)
        {
            _recipients.Remove(recipient);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (!_chord.IsValid)
        {
            ShowWarning("Сначала запишите горячую клавишу.");
            return;
        }

        if (_recipients.Count == 0)
        {
            ShowWarning("Добавьте хотя бы одного получателя.");
            return;
        }

        var message = MessageTextBox.Text.Trim();
        if (message.Length is < 1 or > 1024)
        {
            ShowWarning("Сообщение должно содержать от 1 до 1024 символов.");
            return;
        }

        var priority = PriorityComboBox.SelectedValue is int value
            ? value
            : (int)PushoverPriority.Normal;
        var sound = SoundComboBox.SelectedValue as string ?? PushoverSounds.DefaultId;

        Binding = new HotkeyBinding
        {
            Id = _id,
            Chord = _chord.DeepCopy(),
            Message = message,
            Priority = priority,
            Sound = sound,
            Recipients = _recipients.Select(recipient => recipient.DeepCopy()).ToList()
        };
        DialogResult = true;
    }

    private void UpdateChordDisplay() => ChordText.Text = _chord.IsValid ? _chord.ToString() : "Не выбрана";

    private static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin;

    private void ShowWarning(string message) => MessageBox.Show(message, "Проверьте данные", MessageBoxButton.OK, MessageBoxImage.Warning);

    private sealed record PriorityOption(int Value, string Name);
}
