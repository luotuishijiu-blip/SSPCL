using System.IO;
using System.Windows;
using System.Windows.Controls;
using Sspcl.App.Services;
using Sspcl.Core.Install;
using Sspcl.Core.Launch;
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;

namespace Sspcl.App.Views;

public partial class SettingsPage : UserControl
{
    private readonly AppSettings _settings;
    private Installation _install = null!;
    private bool _loading;

    public SettingsPage(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        CategoryList.SelectedIndex = 0;
    }

    public void Load(Installation install)
    {
        _install = install;
        _loading = true;
        try
        {
            MinimizeRadio.IsChecked = _settings.LaunchMinimize;
            KeepRadio.IsChecked = !_settings.LaunchMinimize;
            NormalRadio.IsChecked = _settings.ProcessPriority != "AboveNormal" && _settings.ProcessPriority != "High";
            AboveNormalRadio.IsChecked = _settings.ProcessPriority == "AboveNormal";
            HighRadio.IsChecked = _settings.ProcessPriority == "High";
            MemAutoRadio.IsChecked = _settings.DefaultMemoryAuto;
            MemCustomRadio.IsChecked = !_settings.DefaultMemoryAuto;
            MemCustomPanel.Visibility = _settings.DefaultMemoryAuto ? Visibility.Collapsed : Visibility.Visible;
            GlobalXmxBox.Text = _settings.DefaultMemoryMb > 0 ? _settings.DefaultMemoryMb.ToString() : "";
            GlobalXmsBox.Text = _settings.DefaultXmsMb > 0 ? _settings.DefaultXmsMb.ToString() : "";
        }
        finally { _loading = false; }

        LoadMemory();
        LoadVmparams();
        AboutText.Text =
            "sspcl — 远行星号启动器 + Mod 管理器\n\n" +
            "· Mod 索引：StarsectorModRepo（与 TriOS 同源）\n" +
            "· 解压：内置 7-Zip（.zip / .7z / .rar）\n" +
            "· 直接启动：接管游戏自带启动器（-DlaunchDirect + saves\\.launching）\n\n" +
            "配置文件：%APPDATA%\\sspcl\\config.json";
    }

    private void LoadMemory()
    {
        try
        {
            var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
            double total = ci.TotalPhysicalMemory / 1024.0 / 1024 / 1024;
            double avail = ci.AvailablePhysicalMemory / 1024.0 / 1024 / 1024;
            double used = total - avail;
            MemBar.Maximum = 100;
            MemBar.Value = total > 0 ? Math.Clamp(used / total * 100, 0, 100) : 0;
            MemText.Text = $"系统内存：已用 {used:F1} GB / 总 {total:F1} GB";
        }
        catch
        {
            MemText.Text = "无法读取系统内存";
        }

        int autoMb = MemoryHelper.AutoMemoryMb();
        MemAutoHint.Text = $"自动配置 ≈ {autoMb / 1024} GB（主机内存 ÷ 2 − 2）";

        int xmx = 0, xms = 0;
        try { (xmx, xms) = Launcher.ReadMemoryMb(File.ReadAllText(Launcher.VmparamsPath(_install.Path))); }
        catch { }
        GameMemText.Text = $"当前版本游戏分配：-Xmx{xmx}m / -Xms{xms}m（可在「启动游戏」时自定义并保存）";
    }

    private void Category_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LaunchPanel is null || PersonalizePanel is null || OtherPanel is null) return;
        LaunchPanel.Visibility = CategoryList.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        PersonalizePanel.Visibility = CategoryList.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        OtherPanel.Visibility = CategoryList.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Behavior_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || (sender as RadioButton)?.Tag is not string tag) return;
        _settings.LaunchMinimize = tag == "minimize";
        _settings.Save();
    }

    private void Priority_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || (sender as RadioButton)?.Tag is not string tag) return;
        _settings.ProcessPriority = tag;
        _settings.Save();
    }

    private void MemMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool custom = MemCustomRadio.IsChecked == true;
        MemCustomPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        _settings.DefaultMemoryAuto = !custom;
        _settings.Save();
    }

    private void GlobalMemSave_Click(object sender, RoutedEventArgs e)
    {
        _settings.DefaultMemoryMb = int.TryParse(GlobalXmxBox.Text.Trim(), out var xmx) ? xmx : 0;
        _settings.DefaultXmsMb = int.TryParse(GlobalXmsBox.Text.Trim(), out var xms) ? xms : 0;
        _settings.Save();
    }

    private void LoadVmparams()
    {
        try { VmparamsBox.Text = File.ReadAllText(Launcher.VmparamsPath(_install.Path)); }
        catch { VmparamsBox.Text = ""; }
    }

    private void VmparamsSave_Click(object sender, RoutedEventArgs e)
    {
        if (GameProcessGuard.IsGameRunning(_install.Path))
        {
            MessageBox.Show("游戏正在运行，请先退出。", "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            File.WriteAllText(Launcher.VmparamsPath(_install.Path), VmparamsBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show("保存失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void VmparamsRefresh_Click(object sender, RoutedEventArgs e) => LoadVmparams();
}
