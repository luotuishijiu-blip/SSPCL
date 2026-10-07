using System.Windows;
using Sspcl.Desktop.Services;

namespace Sspcl.Desktop.Views;

public partial class ToolEditDialog : Window
{
    public ToolEntry Tool { get; private set; } = new();

    public ToolEditDialog()
    {
        InitializeComponent();
    }

    public ToolEditDialog(ToolEntry t)
    {
        InitializeComponent();
        NameBox.Text = t.Name;
        PathBox.Text = t.Path;
        ArgsBox.Text = t.Args;
        WorkDirBox.Text = t.WorkDir;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "选择程序 / 文件" };
        if (dlg.ShowDialog() == true) PathBox.Text = dlg.FileName;
    }

    private void WorkDirBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择工作目录" };
        if (dlg.ShowDialog() == true) WorkDirBox.Text = dlg.FolderName;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Tool = new ToolEntry
        {
            Name = NameBox.Text.Trim(),
            Path = PathBox.Text.Trim(),
            Args = ArgsBox.Text,
            WorkDir = WorkDirBox.Text,
        };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
