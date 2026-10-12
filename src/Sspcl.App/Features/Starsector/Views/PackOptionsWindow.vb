Imports Sspcl.Core.Modpack

Public Class PackOptionsWindow
    Inherits Window
    Private ReadOnly SourceBox As New ComboBox With {.Margin = New Thickness(0, 4, 0, 12)}
    Private ReadOnly NameBox As New TextBox With {.Margin = New Thickness(0, 4, 0, 12), .Padding = New Thickness(6)}
    Private ReadOnly TargetBox As New TextBox With {.Margin = New Thickness(0, 4, 0, 12), .Padding = New Thickness(6)}
    Private ReadOnly ModOption As New CheckBox With {.Content = "MOD 列表（只分享 ID、名称和版本）", .IsChecked = True}
    Private ReadOnly SettingsOption As New CheckBox With {.Content = "settings.json 与玩家配置", .IsChecked = True}
    Private ReadOnly VersionOption As New CheckBox With {.Content = "游戏版本", .IsChecked = True}
    Private ReadOnly NameOption As New CheckBox With {.Content = "整合包名字", .IsChecked = True}
    Private ReadOnly SaveOption As New CheckBox With {.Content = "存档", .IsChecked = False}
    Public ReadOnly Property Options As ExportOptions
        Get
            Return New ExportOptions With {.IncludeModList = ModOption.IsChecked = True, .IncludeGameSettings = SettingsOption.IsChecked = True,
                .IncludeGameVersion = VersionOption.IsChecked = True, .IncludePackName = NameOption.IsChecked = True, .IncludeSaves = SaveOption.IsChecked = True}
        End Get
    End Property
    Public ReadOnly Property GamePath As String
        Get
            Return CStr(DirectCast(SourceBox.SelectedItem, ComboBoxItem).Tag)
        End Get
    End Property
    Public ReadOnly Property PackName As String
        Get
            Return NameBox.Text.Trim()
        End Get
    End Property
    Public ReadOnly Property TargetPath As String
        Get
            Return TargetBox.Text.Trim()
        End Get
    End Property

    Public Sub New(exporting As Boolean, manifest As PackManifest, candidates As IEnumerable(Of String))
        Title = If(exporting, "导出整合包", "导入整合包")
        Width = 510 : SizeToContent = SizeToContent.Height : ResizeMode = ResizeMode.NoResize
        WindowStartupLocation = WindowStartupLocation.CenterOwner
        If ModMain.FrmMain IsNot Nothing Then Owner = ModMain.FrmMain
        Dim panel As New StackPanel With {.Margin = New Thickness(22)}
        panel.Children.Add(New TextBlock With {.Text = If(exporting, "选择要分享的内容，所有项目都可以取消。", "导入为 MOD 启用方案，共用游戏文件；缺失 MOD 单独下载。"), .TextWrapping = TextWrapping.Wrap})
        panel.Children.Add(New TextBlock With {.Text = If(exporting, "导出来源", "共享游戏目录"), .Margin = New Thickness(0, 12, 0, 0)})
        For Each path In candidates.Distinct(StringComparer.OrdinalIgnoreCase)
            Dim detected = ModStarsector.Detect(path)
            If Not detected.IsValid Then Continue For
            Dim item As New ComboBoxItem With {.Content = IO.Path.GetFileName(path.TrimEnd("\"c)) & " · " & detected.Version, .Tag = path, .ToolTip = path}
            SourceBox.Items.Add(item)
            If SourceBox.SelectedItem Is Nothing OrElse (Not exporting AndAlso manifest.GameVersion = detected.Version) OrElse
                (exporting AndAlso String.Equals(path, ModMain.SelectedFolderPath, StringComparison.OrdinalIgnoreCase)) Then SourceBox.SelectedItem = item
        Next
        panel.Children.Add(SourceBox)
        panel.Children.Add(New TextBlock With {.Text = "整合包名字"})
        NameBox.Text = If(exporting, "我的整合包", If(manifest.Name, "未命名整合包"))
        NameBox.IsReadOnly = Not exporting
        panel.Children.Add(NameBox)
        For Each optionBox In New CheckBox() {ModOption, SettingsOption, VersionOption, NameOption, SaveOption}
            optionBox.Margin = New Thickness(0, 0, 0, 10)
            panel.Children.Add(optionBox)
        Next
        If Not exporting Then
            ModOption.IsEnabled = manifest.Mods IsNot Nothing : ModOption.IsChecked = manifest.Mods IsNot Nothing
            SettingsOption.IsEnabled = manifest.HasSettings : SettingsOption.IsChecked = manifest.HasSettings
            VersionOption.IsEnabled = Not String.IsNullOrWhiteSpace(manifest.GameVersion) : VersionOption.IsChecked = VersionOption.IsEnabled
            NameOption.IsEnabled = Not String.IsNullOrWhiteSpace(manifest.Name) : NameOption.IsChecked = NameOption.IsEnabled
            SaveOption.IsEnabled = manifest.HasSaves : SaveOption.IsChecked = manifest.HasSaves
            panel.Children.Add(New TextBlock With {.Text = "方案保存目录：" & ModMain.ProfileStore().Root, .TextWrapping = TextWrapping.Wrap})
            VersionOption.IsEnabled = False : VersionOption.IsChecked = False
        End If
        panel.Children.Add(New TextBlock With {.Text = "包内禁止携带 MOD 文件和游戏本体。", .Margin = New Thickness(0, 10, 0, 10)})
        Dim confirm As New Button With {.Content = If(exporting, "选择文件并导出", "导入启用方案"), .Padding = New Thickness(10, 6, 10, 6), .IsDefault = True}
        AddHandler confirm.Click, Sub()
            If SourceBox.SelectedItem Is Nothing Then
                MessageBox.Show(Me, "请先添加本机已有的有效游戏目录。") : Return
            End If
            DialogResult = True
        End Sub
        panel.Children.Add(confirm)
        Content = panel
    End Sub
End Class
