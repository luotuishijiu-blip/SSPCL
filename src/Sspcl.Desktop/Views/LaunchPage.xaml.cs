using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Sspcl.Desktop.Services;
using Sspcl.Desktop.ViewModels;
using Sspcl.Core.Install;
using Sspcl.Core.Launch;
using Sspcl.Core.Logs;
using Sspcl.Core.Mods;
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;

namespace Sspcl.Desktop.Views;

public partial class LaunchPage : UserControl
{
    private readonly AppSettings _settings;
    private Installation _install = null!;
    private Installation? _settingsTarget;
    private List<InstallItemViewModel> _installs = new();
    private bool _loading;
    private bool _settingsFromVersionSelect;
    private bool _populatingSettings;
    private ModPage _embeddedModPage = null!;
    private readonly ObservableCollection<ToolEntry> _pinnedTools = new();

    /// <summary>请求切换到指定安装路径。</summary>
    public event Action<string>? InstallSwitchRequested;

    public LaunchPage(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        SettingsCategoryList.SelectedIndex = 0;
        _embeddedModPage = new ModPage(_settings);
        InstallModPanel.Content = _embeddedModPage;

        _configFields = LoadFieldDefs();
        ConfigFieldList.ItemsSource = _configView;
        PinnedTools.ItemsSource = _pinnedTools;
    }

    public void Load(Installation install)
    {
        _install = install;
        LaunchBtnSub.Text = install.DisplayName;
        AccountName.Text = install.DisplayName;
        AccountVersion.Text = $"{install.Version} · 汉化 {install.LocalizationPackageVersion}";

        _loading = true;
        try
        {
            _installs = _settings.InstallPaths
                .Select(p => new InstallItemViewModel(InstallationDetector.Detect(p),
                    string.Equals(p, install.Path, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            FolderList.ItemsSource = _installs;
            VersionCardList.ItemsSource = _installs;
            var current = _installs.FirstOrDefault(i => i.IsCurrent);
            FolderList.SelectedItem = current;
            VersionCardList.SelectedItem = current;
            DirectLaunchRadio.IsChecked = _settings.SkipLauncherDefault;
            GameLauncherRadio.IsChecked = !_settings.SkipLauncherDefault;
        }
        finally { _loading = false; }

        RefreshPinned();
    }

    private void ShowMain()
    {
        MainView.Visibility = Visibility.Visible;
        VersionSelectView.Visibility = Visibility.Collapsed;
        InstallSettingsView.Visibility = Visibility.Collapsed;
    }

    /// <summary>从其他页跳回启动页时重置到主页，并刷新已部署工具。</summary>
    public void ResetToHome()
    {
        ShowMain();
        RefreshPinned();
    }

    private void ShowVersionSelect()
    {
        MainView.Visibility = Visibility.Collapsed;
        VersionSelectView.Visibility = Visibility.Visible;
        InstallSettingsView.Visibility = Visibility.Collapsed;
    }

    private void ShowInstallSettings()
    {
        MainView.Visibility = Visibility.Collapsed;
        VersionSelectView.Visibility = Visibility.Collapsed;
        InstallSettingsView.Visibility = Visibility.Visible;
    }

    private void ShowVersionSelect_Click(object sender, RoutedEventArgs e)
    {
        _loading = true;
        try
        {
            var current = _installs.FirstOrDefault(i => i.IsCurrent);
            FolderList.SelectedItem = current;
            VersionCardList.SelectedItem = current;
        }
        finally { _loading = false; }
        ShowVersionSelect();
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ShowMain();

    private void OpenCurrentInstallSettings_Click(object sender, RoutedEventArgs e)
    {
        _settingsFromVersionSelect = false;
        PopulateInstallSettings(_install);
        ShowInstallSettings();
    }

    private void LaunchMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || (sender as RadioButton)?.Tag is not string tag) return;
        _settings.SkipLauncherDefault = tag == "direct";
        _settings.Save();
    }

    private void RefreshPinned()
    {
        _pinnedTools.Clear();
        foreach (var t in _settings.Tools.Where(t => t.Pinned))
            _pinnedTools.Add(t);
    }

    private void AddPin_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var unpinned = _settings.Tools.Where(t => !t.Pinned).ToList();
        if (unpinned.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "（百宝箱里没有可部署的工具）", IsEnabled = false });
        }
        else
        {
            foreach (var t in unpinned)
            {
                var mi = new MenuItem { Header = t.Name };
                var captured = t;
                mi.Click += (_, _) => { captured.Pinned = true; _settings.Save(); RefreshPinned(); };
                menu.Items.Add(mi);
            }
        }
        menu.PlacementTarget = (Button)sender;
        menu.IsOpen = true;
    }

    private void PinnedTool_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ToolEntry t)
        {
            string err = ToolRunner.Run(t);
            if (err.Length > 0) MessageBox.Show("运行失败：" + err, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UnpinTool_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ToolEntry t)
        {
            t.Pinned = false;
            _settings.Save();
            RefreshPinned();
        }
    }

    private void SettingsBack_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsFromVersionSelect) ShowVersionSelect();
        else ShowMain();
    }

    private void VersionCardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (VersionCardList.SelectedItem is InstallItemViewModel vm)
        {
            if (!string.Equals(vm.Installation.Path, _install.Path, StringComparison.OrdinalIgnoreCase))
                InstallSwitchRequested?.Invoke(vm.Installation.Path);
            ShowMain();
        }
    }

    private void OpenInstallSettings_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is InstallItemViewModel vm)
        {
            _settingsFromVersionSelect = true;
            PopulateInstallSettings(vm.Installation);
            ShowInstallSettings();
        }
    }

    private void PopulateInstallSettings(Installation target)
    {
        _settingsTarget = target;
        SettingsTitle.Text = "版本设置 - " + target.DisplayName;
        SettingsCategoryList.SelectedIndex = 0;

        var mem = _settings.GetMemory(target.Path);
        bool custom = mem.XmxMb > 0 || mem.XmsMb > 0;
        _populatingSettings = true;
        try
        {
            MemFollowGlobalRadio.IsChecked = !custom;
            MemCustomRadio.IsChecked = custom;
            InstMemCustomPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
            InstXmxBox.Text = mem.XmxMb > 0 ? mem.XmxMb.ToString() : "";
            InstXmsBox.Text = mem.XmsMb > 0 ? mem.XmsMb.ToString() : "";
        }
        finally { _populatingSettings = false; }
        NoteBox.Text = _settings.GetInstallNote(target.Path);

        var mods = ModScanner.Scan(Path.Combine(target.Path, "mods"));
        var report = DependencyResolver.Analyze(mods, target.ParsedVersion);
        int enabled = EnabledModsFile.Read(target.Path).Count;
        int problems = report.Missing.Count + report.Duplicates.Count + report.OverVersion.Count;
        InstallOverviewText.Text =
            $"{target.DisplayName} · {target.Version} · 汉化 {target.LocalizationPackageVersion}\n{target.Path}\n\n" +
            $"共 {mods.Count} 个 mod · 启用 {enabled} · {target.SaveCount} 个存档\n" +
            (problems > 0 ? $"⚠ {problems} 处问题（缺前置 / 重复 id / 版本超标）" : "✅ 无依赖 / 兼容性问题") +
            (target.Problems.Count > 0 ? "\n\n问题：" + string.Join("；", target.Problems) : "");
    }

    private void SettingsCategory_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (InstallSettingsPanel is null || InstallOverviewPanel is null || InstallModPanel is null || InstallConfigPanel is null || InstallLogPanel is null || InstallExportPanel is null) return;
        int idx = SettingsCategoryList.SelectedIndex;
        InstallSettingsPanel.Visibility = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
        InstallOverviewPanel.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
        InstallModPanel.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
        InstallConfigPanel.Visibility = idx == 3 ? Visibility.Visible : Visibility.Collapsed;
        InstallLogPanel.Visibility = idx == 4 ? Visibility.Visible : Visibility.Collapsed;
        InstallExportPanel.Visibility = idx == 5 ? Visibility.Visible : Visibility.Collapsed;
        if (idx == 2 && _settingsTarget is not null) _embeddedModPage.Load(_settingsTarget);
        if (idx == 3 && _settingsTarget is not null) RefreshConfig();
        if (idx == 4 && _settingsTarget is not null) RefreshInstallLog();
    }

    private string ConfigPath(Installation install) =>
        Path.Combine(install.Path, "starsector-core", "data", "config", "settings.json");

    private JsonObject? _configJson;
    private List<SettingsFieldVM> _configFields = new();
    private readonly ObservableCollection<SettingsFieldVM> _configView = new();
    private string _configSearch = "";

    private static List<SettingsFieldVM> LoadFieldDefs()
    {
        var list = new List<SettingsFieldVM>();
        using var stream = typeof(LaunchPage).Assembly.GetManifestResourceStream("Sspcl.Desktop.settings_fields.json");
        if (stream is null) return list;
        using var doc = JsonDocument.Parse(stream);
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            list.Add(new SettingsFieldVM
            {
                Name = e.GetProperty("name").GetString() ?? "",
                Translation = e.GetProperty("translation").GetString() ?? "",
                Format = e.GetProperty("format").GetString() ?? "",
                Note = e.GetProperty("note").GetString() ?? "",
                Kind = e.GetProperty("kind").GetString() ?? "string",
                Deprecated = e.TryGetProperty("deprecated", out var d) && d.GetBoolean(),
            });
        }
        return list;
    }

    private static string NodeToText(JsonNode? n)
    {
        if (n is null) return "";
        if (n is JsonValue v && v.TryGetValue<string>(out var s)) return s;
        return n.ToJsonString();
    }

    private void RefreshConfig()
    {
        if (_settingsTarget is null) return;
        string path = ConfigPath(_settingsTarget);
        ConfigPathText.Text = path;
        _configJson = null;
        foreach (var vm in _configFields) { vm.Original = null; vm.IsChecked = false; vm.Text = ""; }
        try
        {
            if (!File.Exists(path)) { ConfigPathText.Text = path + "（未找到）"; RefreshConfigView(); return; }
            var node = JsonNode.Parse(File.ReadAllText(path));
            _configJson = node as JsonObject ?? new JsonObject();
            foreach (var vm in _configFields)
            {
                _configJson.TryGetPropertyValue(vm.Name, out var v);
                vm.Original = v;
                if (vm.Kind == "bool") vm.IsChecked = v?.GetValue<bool>() ?? false;
                else vm.Text = NodeToText(v);
            }
        }
        catch (Exception ex) { ConfigPathText.Text = "解析失败：" + ex.Message; }
        RefreshConfigView();
    }

    private void RefreshConfigView()
    {
        _configView.Clear();
        string q = _configSearch.Trim();
        foreach (var vm in _configFields)
            if (q.Length == 0
                || vm.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || vm.Translation.Contains(q, StringComparison.OrdinalIgnoreCase))
                _configView.Add(vm);
    }

    private void ConfigSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _configSearch = ConfigSearch.Text;
        RefreshConfigView();
    }

    private void ConfigFieldHeader_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is SettingsFieldVM vm)
            vm.IsExpanded = !vm.IsExpanded;
    }

    private void ConfigSave_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsTarget is null) return;
        if (GameProcessGuard.IsGameRunning(_settingsTarget.Path))
        {
            MessageBox.Show("游戏正在运行，请先退出。", "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_configJson is null)
        {
            MessageBox.Show("settings.json 未加载或解析失败，请先刷新。", "配置文件", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            foreach (var vm in _configFields) WriteConfigField(_configJson, vm);
            string path = ConfigPath(_settingsTarget);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, _configJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            MessageBox.Show("保存失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void WriteConfigField(JsonObject json, SettingsFieldVM vm)
    {
        bool existed = vm.Original is not null;
        if (vm.Kind == "bool")
        {
            if (existed || vm.IsChecked) json[vm.Name] = vm.IsChecked;
            return;
        }
        string t = vm.Text.Trim();
        if (!existed && t.Length == 0) return;

        if (vm.Kind == "array")
        {
            try { if (JsonNode.Parse(vm.Text) is JsonArray arr) json[vm.Name] = arr; } catch { }
            return;
        }

        if (vm.Kind == "number")
        {
            if (long.TryParse(t, out var l)) { json[vm.Name] = l; return; }
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) { json[vm.Name] = d; return; }
            return;
        }

        if (vm.Original is JsonValue ov)
        {
            if (ov.TryGetValue<bool>(out _)) { json[vm.Name] = t.Equals("true", StringComparison.OrdinalIgnoreCase); return; }
            if (ov.TryGetValue<long>(out _) || ov.TryGetValue<double>(out _))
            {
                if (long.TryParse(t, out var l)) { json[vm.Name] = l; return; }
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) { json[vm.Name] = d; return; }
                return;
            }
        }
        json[vm.Name] = vm.Text;
    }

    private void ConfigRefresh_Click(object sender, RoutedEventArgs e) => RefreshConfig();

    private void RefreshInstallLog()
    {
        if (_settingsTarget is null) return;
        InstallLogText.Text = GameLog.ReadTail(_settingsTarget.Path, 3000, InstallLogFilter.Text, InstallLogErrors.IsChecked == true);
        InstallLogText.ScrollToEnd();
    }

    private void InstallLog_Changed(object sender, RoutedEventArgs e) => RefreshInstallLog();
    private void InstallLogRefresh_Click(object sender, RoutedEventArgs e) => RefreshInstallLog();

    private void SaveMemory()
    {
        if (_settingsTarget is null) return;
        int xmx = int.TryParse(InstXmxBox.Text.Trim(), out var a) ? a : 0;
        int xms = int.TryParse(InstXmsBox.Text.Trim(), out var b) ? b : 0;
        _settings.SetMemory(_settingsTarget.Path, xmx, xms);
        _settings.Save();
    }

    private void SaveMemory_Click(object sender, RoutedEventArgs e) => SaveMemory();

    private void InstMemMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_populatingSettings || _settingsTarget is null) return;
        bool custom = MemCustomRadio.IsChecked == true;
        InstMemCustomPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        if (!custom)
        {
            _settings.SetMemory(_settingsTarget.Path, 0, 0);
            _settings.Save();
        }
    }

    private void SaveNote_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsTarget is null) return;
        _settings.SetInstallNote(_settingsTarget.Path, NoteBox.Text.Trim());
        _settings.Save();
    }

    private void ExportClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsTarget is null) return;
        var ids = EnabledModsFile.Read(_settingsTarget.Path);
        Clipboard.SetText(string.Join(Environment.NewLine, ids));
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择远行星号安装目录" };
        if (dialog.ShowDialog() != true) return;

        var inst = InstallationDetector.Detect(dialog.FolderName);
        if (!inst.IsValid)
        {
            MessageBox.Show("该目录不是有效的远行星号安装：\n" + string.Join("\n", inst.Problems),
                "目录无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!_settings.InstallPaths.Contains(dialog.FolderName))
            _settings.InstallPaths.Add(dialog.FolderName);
        _settings.CurrentInstallPath = dialog.FolderName;
        _settings.Save();
        InstallSwitchRequested?.Invoke(dialog.FolderName);
        ShowMain();
    }

    private void ImportArchive_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择要导入的 Mod 压缩包",
            Filter = "Mod 压缩包|*.zip;*.7z;*.rar|所有文件|*.*",
        };
        if (dialog.ShowDialog() != true) return;
        if (GameProcessGuard.IsGameRunning(_install.Path))
        {
            MessageBox.Show("游戏正在运行，请先退出。", "无法导入", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var r = ModInstaller.InstallArchive(dialog.FileName, Path.Combine(_install.Path, "mods"));
        MessageBox.Show(r.Success
                ? $"已安装 {r.Name}（{r.ModId}）" + (r.WasUpdate ? " · 旧版已进回收站" : "")
                : "安装失败：" + r.Error,
            "导入", MessageBoxButton.OK, r.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private async void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (!_install.IsValid)
        {
            Launch();
            return;
        }

        var btn = (Button)sender;
        var original = btn.Content;
        btn.IsEnabled = false;
        btn.Content = "启动中…";
        await System.Threading.Tasks.Task.Delay(250);
        try
        {
            Launch();
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = original;
        }
    }

    private void Launch()
    {
        if (!_install.IsValid)
        {
            MessageBox.Show("当前版本无效，请在「版本选择」中添加游戏目录。", "无法启动", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var (defXmx, defXms) = GetDefaultMemory();
        try
        {
            var priority = _settings.ProcessPriority switch
            {
                "High" => System.Diagnostics.ProcessPriorityClass.High,
                "AboveNormal" => System.Diagnostics.ProcessPriorityClass.AboveNormal,
                _ => System.Diagnostics.ProcessPriorityClass.Normal,
            };
            Launcher.Launch(_install.Path, defXmx, defXms,
                _settings.SkipLauncherDefault, GetResolution(), fullscreen: false, sound: true, priority: priority);
            if (_settings.LaunchMinimize && Window.GetWindow(this) is { } w) w.WindowState = WindowState.Minimized;
        }
        catch (Exception ex)
        {
            MessageBox.Show("启动失败：\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private (int XmxMb, int XmsMb) GetDefaultMemory()
    {
        var mem = _settings.GetMemory(_install.Path);
        int vmXmx = 0, vmXms = 0;
        try { (vmXmx, vmXms) = Launcher.ReadMemoryMb(File.ReadAllText(Launcher.VmparamsPath(_install.Path))); }
        catch { }

        int auto = MemoryHelper.AutoMemoryMb();
        int xmx = mem.XmxMb > 0 ? mem.XmxMb
            : (_settings.DefaultMemoryAuto ? auto
            : (_settings.DefaultMemoryMb > 0 ? _settings.DefaultMemoryMb : 8192));
        int xms = mem.XmsMb > 0 ? mem.XmsMb
            : (_settings.DefaultMemoryAuto ? auto
            : (_settings.DefaultXmsMb > 0 ? _settings.DefaultXmsMb : 8192));
        return (xmx, xms);
    }

    private static string GetResolution() =>
        $"{(int)System.Windows.SystemParameters.WorkArea.Width}x{(int)System.Windows.SystemParameters.WorkArea.Height}";
}
