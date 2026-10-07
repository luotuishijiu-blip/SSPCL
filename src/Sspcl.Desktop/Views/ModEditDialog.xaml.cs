using System.Windows;

namespace Sspcl.Desktop.Views;

public partial class ModEditDialog : Window
{
    public string Note { get; private set; } = "";
    public string Tags { get; private set; } = "";

    public ModEditDialog(string note, string tags)
    {
        InitializeComponent();
        NoteBox.Text = note;
        TagsBox.Text = tags;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Note = NoteBox.Text.Trim();
        Tags = TagsBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
