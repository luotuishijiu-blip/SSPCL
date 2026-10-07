using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Sspcl.Core.Install;
using Sspcl.Core.Mods;
using Sspcl.Core.Store;
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;

namespace Sspcl.Desktop.Views;

public partial class DownloadPage : UserControl
{
    private Installation _install = null!;
    private readonly List<StoreItem> _storeItems = new();
    private readonly ObservableCollection<StoreItem> _storeView = new();
    private string _search = "";
    private bool _loaded;

    public DownloadPage()
    {
        InitializeComponent();
        StoreList.ItemsSource = _storeView;
    }

    public void Load(Installation install)
    {
        _install = install;
        if (_loaded) return;
        _loaded = true;
        _ = LoadStoreAsync();
    }

    private async System.Threading.Tasks.Task LoadStoreAsync()
    {
        try
        {
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = "正在加载中文论坛 Mod 索引…";
            _storeItems.Clear();
            var all = await ModRepoClient.FetchAsync();
            _storeItems.AddRange(all.Where(IsChineseForumMod));
            StatusText.Visibility = Visibility.Collapsed;
            RefreshView();
        }
        catch (Exception ex)
        {
            StatusText.Text = "加载失败：\n" + ex.Message;
        }
    }

    private static bool IsChineseForumMod(StoreItem it) =>
        (it.ForumUrl?.Contains("fossic.org", StringComparison.OrdinalIgnoreCase) ?? false)
        || (it.HomeUrl?.Contains("fossic.org", StringComparison.OrdinalIgnoreCase) ?? false);

    private void RefreshView()
    {
        _storeView.Clear();
        foreach (var it in _storeItems)
            if (string.IsNullOrWhiteSpace(_search)
                || (it.Name + " " + it.Meta + " " + it.Summary).Contains(_search, StringComparison.OrdinalIgnoreCase))
                _storeView.Add(it);
        StatusText.Visibility = _storeItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text;
        RefreshView();
    }

    private void DownloadVanilla_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.fossic.org/forum.php?mod=attachment&aid=ODU2MTR8NGIwODUyOTJ8MTc5MTI2MDQ1M3wwfDE5NDMw") { UseShellExecute = true });
    }

    private void OpenHome_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is StoreItem it && !string.IsNullOrEmpty(it.HomeUrl))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(it.HomeUrl) { UseShellExecute = true });
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not StoreItem it) return;
        if (GameProcessGuard.IsGameRunning(_install.Path))
        {
            MessageBox.Show("游戏正在运行，请先退出。", "无法安装", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = $"正在下载 {it.Name}…";
            string file = await ModRepoClient.DownloadToTempAsync(it.DirectDownloadUrl!);
            StatusText.Text = $"正在安装 {it.Name}…";
            var result = ModInstaller.InstallArchive(file, Path.Combine(_install.Path, "mods"));
            StatusText.Text = result.Success
                ? $"已安装 {result.Name}（{result.ModId}）" + (result.WasUpdate ? " · 旧版已进回收站" : "")
                : "安装失败：" + result.Error;
        }
        catch (Exception ex)
        {
            StatusText.Text = "下载失败：" + ex.Message;
        }
    }
}
