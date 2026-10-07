using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Sspcl.Desktop.Services;
using Sspcl.Core.Install;
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;

namespace Sspcl.Desktop.Views;

public partial class ToolboxPage : UserControl
{
    private readonly AppSettings _settings;
    private Installation _install = null!;
    private readonly ObservableCollection<ToolEntry> _tools = new();

    public ToolboxPage(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        ToolList.ItemsSource = _tools;
        Reload();
    }

    public void Load(Installation install) => _install = install;

    private void AIFit_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AIFitDialog(_settings, _install) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
    }

    public void Reload()
    {
        _tools.Clear();
        foreach (var t in _settings.Tools) _tools.Add(t);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ToolEditDialog { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
        {
            _settings.Tools.Add(dlg.Tool);
            _settings.Save();
            Reload();
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ToolEntry t) return;
        var dlg = new ToolEditDialog(t) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
        {
            t.Name = dlg.Tool.Name;
            t.Path = dlg.Tool.Path;
            t.Args = dlg.Tool.Args;
            t.WorkDir = dlg.Tool.WorkDir;
            _settings.Save();
            Reload();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ToolEntry t) return;
        var r = MessageBox.Show($"删除工具「{t.Name}」？", "删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        _settings.Tools.Remove(t);
        _settings.Save();
        Reload();
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ToolEntry t) return;
        string err = ToolRunner.Run(t);
        if (err.Length > 0) MessageBox.Show("运行失败：" + err, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
