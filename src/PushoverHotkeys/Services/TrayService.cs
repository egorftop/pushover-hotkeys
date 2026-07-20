using System.Drawing;
using Forms = System.Windows.Forms;

namespace PushoverHotkeys.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _toggleItem;

    public TrayService()
    {
        var menu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("Открыть");
        _toggleItem = new Forms.ToolStripMenuItem();
        var exitItem = new Forms.ToolStripMenuItem("Выйти");
        menu.Items.AddRange([openItem, _toggleItem, new Forms.ToolStripSeparator(), exitItem]);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Pushover Hotkeys",
            ContextMenuStrip = menu
        };

        openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _toggleItem.Click += (_, _) => ToggleHotkeysRequested?.Invoke(this, EventArgs.Empty);
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        SetHotkeysEnabled(false);
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? ToggleHotkeysRequested;
    public event EventHandler? ExitRequested;

    public void SetHotkeysEnabled(bool enabled)
    {
        _toggleItem.Text = enabled ? "Отключить горячие клавиши" : "Включить горячие клавиши";
    }

    public void SetTooltip(string status)
    {
        _notifyIcon.Text = status.Length > 63 ? status[..63] : status;
    }

    public void ShowError(string message)
    {
        _notifyIcon.ShowBalloonTip(5000, "Pushover Hotkeys", message, Forms.ToolTipIcon.Error);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}

