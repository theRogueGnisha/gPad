using System.IO;
using System.Windows;

namespace gPad;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private System.Drawing.Icon? _trayIcon;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
        if (File.Exists(iconPath))
        {
            try
            {
                using var bmp = new System.Drawing.Bitmap(iconPath);
                _trayIcon = System.Drawing.Icon.FromHandle(((System.Drawing.Bitmap)bmp.Clone()).GetHicon());
            }
            catch { /* ignore */ }
        }
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _trayIcon ?? System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "gPad"
        };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings");
        settingsItem.Click += (_, _) =>
        {
            if (MainWindow is MainWindow mw)
            {
                var sw = new SettingsWindow();
                sw.SetOwner(mw);
                sw.Show();
            }
        };
        contextMenu.Items.Add(settingsItem);
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Shutdown();
        };
        contextMenu.Items.Add(exitItem);
        _notifyIcon.ContextMenuStrip = contextMenu;

        _notifyIcon.DoubleClick += (_, _) =>
        {
            if (MainWindow is { } w)
            {
                w.Show();
                w.WindowState = WindowState.Normal;
                w.Activate();
            }
        };

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            mainWindow.Hide();
        };
        mainWindow.Show();
    }
}
