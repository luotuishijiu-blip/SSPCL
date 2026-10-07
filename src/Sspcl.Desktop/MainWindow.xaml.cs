using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Sspcl.Desktop.Services;
using Sspcl.Desktop.Views;
using Sspcl.Core.Install;
using Application = System.Windows.Application;

namespace Sspcl.Desktop;

public partial class MainWindow : Window
{
    private AppSettings _settings = null!;
    private Installation _current = null!;
    private LaunchPage _launchPage = null!;
    private DownloadPage _downloadPage = null!;
    private SettingsPage _settingsPage = null!;
    private ToolboxPage _toolboxPage = null!;
    private FeedbackPage _feedbackPage = null!;

    private System.Windows.Forms.NotifyIcon? _tray;
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        _launchPage = new LaunchPage(_settings);
        _downloadPage = new DownloadPage();
        _settingsPage = new SettingsPage(_settings);
        _toolboxPage = new ToolboxPage(_settings);
        _feedbackPage = new FeedbackPage(_settings);
        _launchPage.InstallSwitchRequested += path =>
        {
            _settings.CurrentInstallPath = path;
            _settings.Save();
            ApplyCurrentInstall(path);
        };

        NavList.ItemsSource = new[] { "▶ 启动", "⇩ 下载", "⚙ 设置", "▣ 百宝箱", "✉ 反馈" };
        ApplyCurrentInstall(_settings.CurrentInstallPath);
        NavList.SelectedIndex = 0; // 默认进入「启动」首页

        SetupTray();
    }

    private void ApplyCurrentInstall(string path)
    {
        _current = InstallationDetector.Detect(path);
        DetectLocalizationChange(path);
        _launchPage.Load(_current);
        _downloadPage.Load(_current);
        _settingsPage.Load(_current);
        _toolboxPage.Load(_current);
    }

    private void DetectLocalizationChange(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        _settings.SetLocalizationSeen(path, _current.LocalizationPackageVersion);
        _settings.Save();
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_launchPage is null || _downloadPage is null || _settingsPage is null || _toolboxPage is null || _feedbackPage is null) return;
        if (NavList.SelectedIndex == 0) _launchPage.ResetToHome();
        PageHost.Content = NavList.SelectedIndex switch
        {
            0 => _launchPage,
            2 => _settingsPage,
            3 => _toolboxPage,
            4 => _feedbackPage,
            _ => _downloadPage,
        };
        FadeIn(PageHost);
    }

    private static void FadeIn(UIElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 0;
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        element.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // ---- 常驻托盘 ----

    private void SetupTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "sspcl — 远行星号 Mod 管理器",
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示 sspcl", null, (_, _) => ShowWindow());
        menu.Items.Add("退出", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowWindow();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Sspcl.ico");
        return System.IO.File.Exists(path)
            ? new System.Drawing.Icon(path)
            : System.Drawing.SystemIcons.Application;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        TryRoundCorners();
    }

    private void TryRoundCorners()
    {
        try
        {
            if (Environment.OSVersion.Version.Build < 22000) return; // 仅 Windows 11 支持原生圆角
            var hwnd = new WindowInteropHelper(this).Handle;
            int attr = 33;  // DWMWA_WINDOW_CORNER_PREFERENCE
            int pref = 2;   // DWMWCP_ROUND
            DwmSetWindowAttribute(hwnd, attr, ref pref, sizeof(int));
        }
        catch { /* 圆角失败不致命 */ }
    }

    private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && WindowState != WindowState.Maximized)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _exiting = true;
        _tray?.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_exiting)
        {
            base.OnClosing(e);
            return;
        }
        e.Cancel = true;
        Hide();
    }
}
