Imports Sspcl.Core.Mods
Imports Sspcl.Core.Store.Forum

Public Class ModUpdateWindow
    Inherits Window

    Public Property SelectedChoice As ReleaseChoice
    Private ReadOnly Releases As New ComboBox With {.Margin = New Thickness(0, 12, 0, 10), .DisplayMemberPath = "Display"}
    Private ReadOnly Hint As New TextBlock With {.TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 12)}
    Private ReadOnly DownloadButton As New Button With {.Content = "下载并更新", .Padding = New Thickness(12, 6, 12, 6), .IsEnabled = False}
    Private ReadOnly GameVersion As String

    Public Sub New(current As ModSpec, gameVersion As String, mods As IEnumerable(Of ForumMod), warning As String)
        Me.GameVersion = gameVersion
        Title = "更新 MOD"
        Width = 620
        SizeToContent = SizeToContent.Height
        WindowStartupLocation = WindowStartupLocation.CenterOwner
        Dim panel As New StackPanel With {.Margin = New Thickness(20)}
        panel.Children.Add(New TextBlock With {.Text = current.Name & " · " & current.Id & vbCrLf & "当前 MOD：" & current.VersionRaw & " · 游戏：" & gameVersion, .TextWrapping = TextWrapping.Wrap})
        panel.Children.Add(New TextBlock With {.Text = "请选择完整发布包。更新会完整替换旧目录，保留 MOD 的启用状态。", .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 10, 0, 0)})
        Dim choices As New List(Of ReleaseChoice)
        For Each modInfo In mods
            If modInfo.Releases Is Nothing Then Continue For
            For Each release In modInfo.Releases.OrderByDescending(Function(r) r.UploadedAt.GetValueOrDefault())
                choices.Add(New ReleaseChoice With {.ModInfo = modInfo, .Release = release})
            Next
        Next
        Releases.ItemsSource = choices
        panel.Children.Add(Releases)
        Hint.Text = If(choices.Count = 0, "论坛没有找到此 MOD 的发布包。可使用 MOD 管理中的「本地更新」。", "请选择发布包，确认文件名与游戏版本。") & If(String.IsNullOrWhiteSpace(warning), "", vbCrLf & warning)
        panel.Children.Add(Hint)
        Dim buttons As New WrapPanel
        buttons.Children.Add(DownloadButton)
        Dim forum As New Button With {.Content = "打开发布帖", .Padding = New Thickness(12, 6, 12, 6), .Margin = New Thickness(8, 0, 0, 0)}
        buttons.Children.Add(forum)
        Dim cancel As New Button With {.Content = "关闭", .Padding = New Thickness(12, 6, 12, 6), .Margin = New Thickness(8, 0, 0, 0)}
        buttons.Children.Add(cancel)
        panel.Children.Add(buttons)
        Content = panel
        AddHandler Releases.SelectionChanged, AddressOf SelectionChanged
        AddHandler DownloadButton.Click, Sub()
            SelectedChoice = TryCast(Releases.SelectedItem, ReleaseChoice)
            DialogResult = True
        End Sub
        AddHandler cancel.Click, Sub() DialogResult = False
        AddHandler forum.Click, Sub()
            Dim choice = TryCast(Releases.SelectedItem, ReleaseChoice)
            Dim modInfo = If(choice Is Nothing, mods.FirstOrDefault(), choice.ModInfo)
            If modInfo Is Nothing Then Return
            Dim address = If(modInfo.Thread.ThreadId > 0, "https://www.fossic.org/thread-" & modInfo.Thread.ThreadId & "-1-1.html", modInfo.PublishUrls.FirstOrDefault())
            Dim uri As Uri = Nothing
            If Uri.TryCreate(address, UriKind.Absolute, uri) AndAlso (uri.Scheme = "http" OrElse uri.Scheme = "https") Then Process.Start(New ProcessStartInfo(uri.AbsoluteUri) With {.UseShellExecute = True})
        End Sub
    End Sub

    Private Sub SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim choice = TryCast(Releases.SelectedItem, ReleaseChoice)
        DownloadButton.IsEnabled = False
        If choice Is Nothing Then Return
        If Not ModUpdateService.IsCompatible(choice.Release.GameVersion, GameVersion) Then
            Hint.Text = "此发布包适配 " & choice.Release.GameVersion & "，与当前游戏版本不同，不能覆盖旧版 MOD。"
            Return
        End If
        Dim uri As Uri = Nothing
        If Not choice.ModInfo.AllowDirectDownload OrElse Not Uri.TryCreate(choice.Release.DownloadUrl, UriKind.Absolute, uri) OrElse (uri.Scheme <> "http" AndAlso uri.Scheme <> "https") Then
            Hint.Text = "论坛未提供此包的可用直下载。请打开发布帖下载，再使用「本地更新」。"
            Return
        End If
        Hint.Text = "游戏版本匹配。下载后还会核对压缩包中的 MOD ID、适配版本和完整性，验证通过才替换旧版。"
        DownloadButton.IsEnabled = True
    End Sub

    Public Class ReleaseChoice
        Public Property ModInfo As ForumMod
        Public Property Release As ForumModRelease
        Public ReadOnly Property Display As String
            Get
                Return Release.ModVersion & " · " & Release.GameVersion & " · " & ModInfo.InfoType & " · " & If(Release.FileName, Release.DisplayName)
            End Get
        End Property
    End Class
End Class
