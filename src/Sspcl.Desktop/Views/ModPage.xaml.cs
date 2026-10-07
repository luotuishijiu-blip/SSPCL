using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Sspcl.Desktop.Services;
using Sspcl.Desktop.ViewModels;
using Sspcl.Core.Install;
using Sspcl.Core.Mods;
using UserControl = System.Windows.Controls.UserControl;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using DataFormats = System.Windows.DataFormats;
using MessageBox = System.Windows.MessageBox;

namespace Sspcl.Desktop.Views;

public partial class ModPage : UserControl
{
    private readonly AppSettings _settings;
    private Installation _install = null!;
    private IReadOnlyList<ModSpec> _mods = Array.Empty<ModSpec>();
    private readonly List<ModListItemViewModel> _items = new();
    private readonly ObservableCollection<ModListItemViewModel> _view = new();
    private HashSet<string> _userChosen = new(StringComparer.Ordinal);
    private HashSet<string> _enabledIds = new(StringComparer.Ordinal);
    private DependencyReport _report = new();
    private HashSet<string> _installedIds = new(StringComparer.Ordinal);
    private HashSet<string> _duplicates = new(StringComparer.Ordinal);
    private HashSet<string> _overVersion = new(StringComparer.Ordinal);
    private bool _applying;
    private string _filter = "all";
    private string _search = "";
    private bool _hideWarnings;
    private readonly HashSet<ModListItemViewModel> _animatedTiles = new();

    public ModPage(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        ModList.ItemsSource = _view;
    }

    public void Load(Installation install)
    {
        _install = install;
        _applying = true;
        try
        {
            _mods = ModScanner.Scan(Path.Combine(install.Path, "mods"));
            _report = DependencyResolver.Analyze(_mods, install.ParsedVersion);
            _installedIds = _mods.Where(m => m.Id.Length > 0).Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            _duplicates = new HashSet<string>(_report.Duplicates.Keys, StringComparer.Ordinal);
            _overVersion = new HashSet<string>(_report.OverVersion.Select(o => o.Id), StringComparer.Ordinal);
            _userChosen = new HashSet<string>(EnabledModsFile.Read(install.Path), StringComparer.Ordinal);
            _enabledIds = new HashSet<string>(DependencyResolver.Closure(_userChosen, _mods), StringComparer.Ordinal);
            _hideWarnings = _settings.HideModWarnings;
            HideWarningsBox.IsChecked = _hideWarnings;

            _items.Clear();
            foreach (var spec in OrderMods())
            {
                var vm = new ModListItemViewModel(spec) { ToggleHandler = OnToggle };
                var meta = _settings.GetModMeta(install.Path, spec.Id);
                vm.Favorite = meta.Favorite;
                vm.Note = meta.Note;
                vm.Tags = meta.Tags;
                ApplyState(vm);
                _items.Add(vm);
            }
        }
        finally { _applying = false; }

        RefreshView();
        UpdateHeader();
        UpdateSummary();
        StatusBar.Text = $"已加载 {_mods.Count} 个 mod · 启用 {_enabledIds.Count}";
    }

    private IEnumerable<ModSpec> OrderMods()
    {
        int Severity(ModSpec m)
        {
            bool missing = m.Dependencies.Any(d => d.Id.Length > 0 && !_installedIds.Contains(d.Id));
            if (missing || _duplicates.Contains(m.Id)) return 0;
            if (_overVersion.Contains(m.Id)) return 1;
            return 2;
        }
        return _mods.OrderBy(Severity).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
    }

    private void ApplyState(ModListItemViewModel vm)
    {
        bool enabled = _enabledIds.Contains(vm.Id);
        bool auto = enabled && !_userChosen.Contains(vm.Id);
        var missing = vm.Spec.Dependencies
            .Where(d => d.Id.Length > 0 && !_installedIds.Contains(d.Id))
            .Select(d => d.Id).ToList();

        string status;
        Brush brush;
        string? problem = null;
        if (!_hideWarnings && missing.Count > 0)
        {
            status = "缺前置: " + string.Join(", ", missing);
            brush = (Brush)FindResource("State.Error");
            problem = status;
        }
        else if (!_hideWarnings && _duplicates.Contains(vm.Id))
        {
            status = "重复 id";
            brush = (Brush)FindResource("State.Error");
            problem = "存在多个同 id 的 mod，建议删除多余的一份";
        }
        else if (!_hideWarnings && _overVersion.Contains(vm.Id))
        {
            status = "版本高于本体 " + _install.Version;
            brush = (Brush)FindResource("State.Warn");
            problem = status;
        }
        else if (auto)
        {
            status = "因前置被自动启用";
            brush = (Brush)FindResource("State.AutoOn");
        }
        else if (enabled)
        {
            status = "已启用";
            brush = (Brush)FindResource("State.Ok");
        }
        else
        {
            status = "未启用";
            brush = (Brush)FindResource("State.Idle");
        }
        vm.ApplyState(enabled, auto, status, problem, brush);
    }

    private void OnToggle(ModListItemViewModel item, bool nowEnabled)
    {
        if (_applying) return;
        _applying = true;
        try
        {
            if (nowEnabled) _userChosen.Add(item.Id); else _userChosen.Remove(item.Id);
            _enabledIds = new HashSet<string>(DependencyResolver.Closure(_userChosen, _mods), StringComparer.Ordinal);
            foreach (var vm in _items) ApplyState(vm);

            WriteEnabled();
            UpdateHeader();
            UpdateSummary();
            if (!nowEnabled && _enabledIds.Contains(item.Id))
                StatusBar.Text = $"「{item.Name}」是其他启用 mod 的前置，已保留启用";
        }
        finally
        {
            _applying = false;
        }
        RefreshView();
    }

    private void WriteEnabled()
    {
        if (GameProcessGuard.IsGameRunning(_install.Path))
        {
            StatusBar.Text = "⚠ 检测到游戏正在运行，已拒绝写入 enabled_mods.json";
            return;
        }
        EnabledModsFile.Write(_install.Path, _enabledIds);
        StatusBar.Text = $"已写入 enabled_mods.json（启用 {_enabledIds.Count} 个 mod）";
    }

    private void RefreshView()
    {
        _view.Clear();
        foreach (var vm in _items)
            if (MatchesFilter(vm)) _view.Add(vm);
    }

    private bool MatchesFilter(ModListItemViewModel vm)
    {
        bool pass = _filter switch
        {
            "enabled" => vm.IsEnabled,
            "disabled" => !vm.IsEnabled,
            "problem" => vm.HasProblem,
            "favorite" => vm.Favorite,
            _ => true
        };
        if (!pass) return false;
        if (string.IsNullOrWhiteSpace(_search)) return true;
        var hay = string.Join(" ", vm.Name, vm.Id, vm.Authors, vm.DepsText, vm.Note, vm.Tags);
        return hay.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateHeader()
    {
        HeaderSub.Text = $"{_install.DisplayName} · {_install.Version} · 共 {_mods.Count} 个 mod，启用 {_enabledIds.Count}";
    }

    private void UpdateSummary()
    {
        if (_hideWarnings)
        {
            SummaryText.Text = "已隐藏警告提醒";
            return;
        }
        int missing = _report.Missing.Count;
        int dup = _report.Duplicates.Count;
        int over = _report.OverVersion.Count;
        if (missing + dup + over == 0)
        {
            SummaryText.Text = "✅ 无依赖 / 兼容性问题";
            return;
        }
        var parts = new List<string>();
        if (missing > 0) parts.Add($"{missing} 处缺失前置");
        if (dup > 0) parts.Add($"{dup} 组重复 id（可用行内「删除」移走多余的一份）");
        if (over > 0) parts.Add($"{over} 个 mod 声明版本高于本体");
        SummaryText.Text = "⚠ " + string.Join(" · ", parts);
    }

    private void PersistMeta(ModListItemViewModel vm)
    {
        var meta = _settings.GetModMeta(_install.Path, vm.Id);
        meta.Favorite = vm.Favorite;
        meta.Note = vm.Note;
        meta.Tags = vm.Tags;
        _settings.SaveModMeta(_install.Path, vm.Id, meta);
        _settings.Save();
    }

    private bool GameRunningGuard()
    {
        if (!GameProcessGuard.IsGameRunning(_install.Path)) return false;
        MessageBox.Show("检测到游戏正在运行，请先退出游戏再操作。", "无法操作", MessageBoxButton.OK, MessageBoxImage.Warning);
        return true;
    }

    // ---- 事件处理 ----

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ModListItemViewModel vm) return;
        vm.Favorite = !vm.Favorite;
        PersistMeta(vm);
        RefreshView();
    }

    private void Note_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ModListItemViewModel vm) return;
        var dialog = new ModEditDialog(vm.Note, vm.Tags) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            vm.Note = dialog.Note;
            vm.Tags = dialog.Tags;
            PersistMeta(vm);
            RefreshView();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ModListItemViewModel vm) return;
        if (GameRunningGuard()) return;
        var r = MessageBox.Show($"把 mod 目录移入回收站？\n\n{vm.Name}\n{vm.Folder}", "删除 Mod",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;

        ModInstaller.RecycleDir(vm.Spec.Path);
        Load(_install);
    }

    private void EnableVisible_Click(object sender, RoutedEventArgs e) => BatchSetEnabled(true);
    private void DisableVisible_Click(object sender, RoutedEventArgs e) => BatchSetEnabled(false);

    private void BatchSetEnabled(bool enable)
    {
        if (GameRunningGuard()) return;
        if (_view.Count == 0) { StatusBar.Text = "当前列表为空"; return; }

        _applying = true;
        try
        {
            foreach (var vm in _view)
            {
                if (enable) _userChosen.Add(vm.Id); else _userChosen.Remove(vm.Id);
            }
            _enabledIds = new HashSet<string>(DependencyResolver.Closure(_userChosen, _mods), StringComparer.Ordinal);
            foreach (var vm in _items) ApplyState(vm);
            WriteEnabled();
            UpdateHeader();
            UpdateSummary();
        }
        finally
        {
            _applying = false;
        }
        RefreshView();
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择 Mod 压缩包",
            Filter = "Mod 压缩包|*.zip;*.7z;*.rar|所有文件|*.*",
        };
        if (dialog.ShowDialog() == true) InstallArchive(dialog.FileName);
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var backups = EnabledModsFile.ListBackups(_install.Path);
        if (backups.Count == 0) { StatusBar.Text = "没有可还原的备份"; return; }
        var newest = backups[0];
        string stamp = Path.GetFileName(newest).Replace("enabled_mods.json.bak-", "");
        var r = MessageBox.Show(
            $"还原到最近一次改动前？\n备份：{stamp}\n（共 {backups.Count} 份备份，也可到 mods 文件夹手动选）",
            "还原", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        EnabledModsFile.RestoreBackup(newest, _install.Path);
        Load(_install);
        StatusBar.Text = "已还原到 " + stamp;
    }

    private void InstallArchive(string archivePath)
    {
        if (GameRunningGuard()) return;
        var result = ModInstaller.InstallArchive(archivePath, Path.Combine(_install.Path, "mods"));
        if (result.Success)
        {
            string msg = $"已安装 {result.Name}（{result.ModId}）→ {result.TargetFolder}";
            if (result.WasUpdate) msg += " · 旧版已移入回收站：" + string.Join(", ", result.RemovedFolders);
            StatusBar.Text = msg;
            Load(_install);
        }
        else
        {
            StatusBar.Text = "安装失败：" + result.Error;
        }
    }

    private void InstallFolder(string folderPath)
    {
        if (GameRunningGuard()) return;
        var result = ModInstaller.InstallFolder(folderPath, Path.Combine(_install.Path, "mods"));
        if (result.Success)
        {
            string msg = $"已安装 {result.Name}（{result.ModId}）→ {result.TargetFolder}";
            if (result.WasUpdate) msg += " · 旧版已移入回收站：" + string.Join(", ", result.RemovedFolders);
            StatusBar.Text = msg;
            Load(_install);
        }
        else
        {
            StatusBar.Text = "安装失败：" + result.Error;
        }
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Page_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var f in files)
        {
            if (Directory.Exists(f)) InstallFolder(f);
            else if (File.Exists(f)) InstallArchive(f);
        }
    }

    private void Rescan_Click(object sender, RoutedEventArgs e) => Load(_install);

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.Combine(_install.Path, "mods");
        if (Directory.Exists(dir))
            System.Diagnostics.Process.Start("explorer.exe", dir);
    }

    private void ExportList_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "mod_list.txt",
            Filter = "文本文件|*.txt",
        };
        if (dialog.ShowDialog() != true) return;
        File.WriteAllLines(dialog.FileName, _enabledIds.OrderBy(x => x, StringComparer.Ordinal));
        StatusBar.Text = $"已导出 {_enabledIds.Count} 个 mod id → {dialog.FileName}";
    }

    private void ImportList_Click(object sender, RoutedEventArgs e)
    {
        if (GameRunningGuard()) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "文本文件|*.txt|所有文件|*.*",
        };
        if (dialog.ShowDialog() != true) return;

        var ids = File.ReadAllLines(dialog.FileName)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);

        int applied = 0, missing = 0;
        _applying = true;
        try
        {
            foreach (var id in ids)
            {
                if (_installedIds.Contains(id)) { _userChosen.Add(id); applied++; }
                else missing++;
            }
            _enabledIds = new HashSet<string>(DependencyResolver.Closure(_userChosen, _mods), StringComparer.Ordinal);
            foreach (var vm in _items) ApplyState(vm);
            WriteEnabled();
            UpdateHeader();
            UpdateSummary();
        }
        finally { _applying = false; }
        RefreshView();
        StatusBar.Text = $"导入完成：启用 {applied} 个，未安装/缺失 {missing} 个";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text;
        RefreshView();
    }

    private void Filter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _filter = tag;
            RefreshView();
        }
    }

    private void HideWarnings_Changed(object sender, RoutedEventArgs e)
    {
        if (_applying) return;
        _hideWarnings = HideWarningsBox.IsChecked == true;
        _settings.HideModWarnings = _hideWarnings;
        _settings.Save();
        foreach (var vm in _items) ApplyState(vm);
        UpdateSummary();
    }

    private void ModTile_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.DataContext is not ModListItemViewModel vm) return;
        if (_animatedTiles.Contains(vm)) return;
        _animatedTiles.Add(vm);
        int idx = ModList.Items.IndexOf(vm);
        if (idx < 0) idx = 0;
        el.BeginAnimation(UIElement.OpacityProperty, null);
        el.Opacity = 0;
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            BeginTime = TimeSpan.FromMilliseconds(Math.Min(idx, 24) * 18),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        el.BeginAnimation(UIElement.OpacityProperty, anim);
    }
}
