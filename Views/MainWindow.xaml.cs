using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using FlightClub.FsClient.ViewModels;
using WinForms = System.Windows.Forms;

namespace FlightClub.FsClient.Views;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private WinForms.NotifyIcon? _notifyIcon;
    private bool _isRealExit;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;

        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new WinForms.NotifyIcon
        {
            Text = "FlightClub",
            Icon = CreateTrayIcon(),
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => RestoreWindow();
    }

    private static Icon CreateTrayIcon()
    {
        // Draw a small colored circle as the tray icon (no external .ico needed)
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(System.Drawing.Color.FromArgb(203, 166, 247)); // #CBA6F7 purple accent
        g.FillEllipse(brush, 1, 1, 14, 14);
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    private WinForms.ContextMenuStrip CreateContextMenu()
    {
        var menu = new WinForms.ContextMenuStrip();

        var showItem = new WinForms.ToolStripMenuItem("Show");
        showItem.Click += (_, _) => RestoreWindow();
        menu.Items.Add(showItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        var exitItem = new WinForms.ToolStripMenuItem("Exit");
        exitItem.Click += async (_, _) =>
        {
            _isRealExit = true;
            await ViewModel.CleanupAsync();
            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);

        return menu;
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        ViewModel.Initialize(helper.Handle);
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isRealExit)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        await ViewModel.CleanupAsync();
    }
}
