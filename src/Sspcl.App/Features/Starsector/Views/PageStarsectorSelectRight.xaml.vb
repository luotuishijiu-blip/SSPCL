Imports Sspcl.Core.Modpack
Imports System.Windows.Media

Public Class PageStarsectorSelectRight

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        ModMain.SelectedFolderPath = ModMain.StarsectorPath
        RefreshCards()
    End Sub

    Public Sub AddFolder()
        Dim path As String
        Using dialog As New System.Windows.Forms.FolderBrowserDialog With {.Description = "选择远行星号安装目录"}
            If dialog.ShowDialog() <> System.Windows.Forms.DialogResult.OK Then Return
            path = dialog.SelectedPath
        End Using
        If Not ModStarsector.Detect(path).IsValid Then
            _status.Text = "此文件夹不是有效的远行星号安装目录。" : Return
        End If
        If Not ModMain.StarsectorPaths.Contains(path) Then
            ModMain.StarsectorPaths.Add(path)
            ModMain.SaveStarsectorPaths()
        End If
        ModMain.SelectedFolderPath = path
        ModMain.FrmStarsectorSelectLeft.RefreshList()
        RefreshCards()
    End Sub

    Private _packBusy As Boolean
    Private _packCancel As CancellationTokenSource
    Private _downloadCancel As CancellationTokenSource
    Private ReadOnly _status As New TextBlock With {.TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 10)}
    Private ReadOnly _gamePanel As New StackPanel With {.Margin = New Thickness(0, 0, 0, 12)}
    Public Const DefaultGameDownloadUrl As String = "https://cdn.fossic.cn/forum/202609/05/014446l82kxv7e16f63953.attach"

    Public Sub RefreshCards()
        If ModMain.StarsectorPaths.Count = 0 Then ModMain.LoadStarsectorPaths()
        PanMain.Children.Clear()
        PanMain.Children.Add(_status)
        PanMain.Children.Add(_gamePanel)
        For Each path In ModMain.StarsectorPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            Dim install = ModStarsector.Detect(path)
            If Not install.IsValid Then Continue For
            Dim manifest = Sspcl.Core.Modpack.ModpackImporter.ReadInstalledManifest(path)
            Dim name = If(manifest Is Nothing OrElse String.IsNullOrWhiteSpace(manifest.Name), IO.Path.GetFileName(path.TrimEnd("\"c)), manifest.Name)
            Dim isCurrent = String.Equals(path, ModMain.StarsectorPath, StringComparison.OrdinalIgnoreCase)
            Dim card As New MyCard With {.Title = name, .Margin = New Thickness(0, 0, 0, 12), .ToolTip = path}
            Dim panel As New StackPanel With {.Margin = New Thickness(20, 38, 20, 18)}
            panel.Children.Add(New TextBlock With {.Text = "远行星号 " & install.Version & If(manifest Is Nothing, "", " · 整合包实例")})
            If manifest IsNot Nothing AndAlso manifest.Mods IsNot Nothing Then
                Dim installed = Sspcl.Core.Mods.ModScanner.Scan(IO.Path.Combine(path, "mods"))
                Dim missing = manifest.Mods.Where(Function(m) Not installed.Any(Function(local) local.Id = m.Id AndAlso (String.IsNullOrWhiteSpace(m.Version) OrElse local.VersionRaw = m.Version))).ToList()
                If missing.Count > 0 Then panel.Children.Add(New TextBlock With {.Text = "缺失 MOD（需单独下载）：" & String.Join("、", missing.Select(Function(m) If(String.IsNullOrWhiteSpace(m.Name), m.Id, m.Name))), .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 6, 0, 0)})
            End If
            Dim row As New WrapPanel With {.Margin = New Thickness(0, 10, 0, 0)}
            Dim useButton As New MyButton With {.Text = If(isCurrent, "正在使用", "使用此版本"), .Padding = New Thickness(12, 5, 12, 5)}
            If Not isCurrent Then AddHandler useButton.Click, Sub(s, e) UseInstall(path)
            row.Children.Add(useButton)
            Dim selectButton As New MyButton With {.Text = "选为导出来源", .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(8, 0, 0, 0)}
            AddHandler selectButton.Click, Sub(s, e)
                ModMain.SelectedFolderPath = path
                _status.Text = "导出来源：" & name
            End Sub
            row.Children.Add(selectButton)
            If Not isCurrent Then
                Dim removeButton As New MyButton With {.Text = "移除", .Padding = New Thickness(10, 5, 10, 5), .Margin = New Thickness(8, 0, 0, 0)}
                AddHandler removeButton.Click, Sub(s, e) RemoveInstall(path)
                row.Children.Add(removeButton)
            End If
            panel.Children.Add(row)
            card.BorderChild = panel
            PanMain.Children.Add(card)
        Next
        If PanMain.Children.Count = 2 Then PanMain.Children.Add(New TextBlock With {.Text = "还没有可用版本。左侧添加已有游戏文件夹，或点击右下角 + 下载游戏。", .TextWrapping = TextWrapping.Wrap})
    End Sub

    Public Async Function ExportPackAsync() As Task
        If _packBusy Then Return
        Dim options As New PackOptionsWindow(True, New Sspcl.Core.Modpack.PackManifest(), ModMain.StarsectorPaths)
        If options.ShowDialog() <> True Then Return
        Dim dialog As New Microsoft.Win32.SaveFileDialog With {.FileName = "整合包.sspack", .Filter = "SSPCL 整合包|*.sspack|ZIP 压缩包|*.zip", .DefaultExt = ".sspack", .OverwritePrompt = True}
        If dialog.ShowDialog() <> True Then Return
        Dim sourcePath = options.GamePath
        Dim name = options.PackName
        Dim selectedOptions = options.Options
        _packBusy = True
        Using cancellation As New CancellationTokenSource()
            _packCancel = cancellation
            Dim cancelButton As New Button With {.Content = "取消导出", .Padding = New Thickness(8, 4, 8, 4)}
            AddHandler cancelButton.Click, Sub() cancellation.Cancel()
            _gamePanel.Children.Add(cancelButton)
            Try
                _status.Text = "正在导出清单、配置和存档…"
                Await Task.Run(Function() Sspcl.Core.Modpack.ModpackExporter.Export(sourcePath, dialog.FileName, name, selectedOptions, cancellation.Token))
                _status.Text = "整合包已导出：" & dialog.FileName & "。包内没有 MOD 文件和游戏本体。"
            Catch ex As OperationCanceledException
                _status.Text = "导出已取消，已有整合包文件未被覆盖。"
            Catch ex As Exception
                _status.Text = "导出失败：" & ex.Message
            Finally
                _packBusy = False
                _packCancel = Nothing
                _gamePanel.Children.Remove(cancelButton)
            End Try
        End Using
    End Function

    Public Async Function ImportPackAsync() As Task
        If _packBusy Then Return
        Dim dialog As New Microsoft.Win32.OpenFileDialog With {.Filter = "SSPCL 整合包|*.sspack;*.zip", .Title = "导入清单整合包"}
        If dialog.ShowDialog() <> True Then Return
        _packBusy = True
        Using cancellation As New CancellationTokenSource()
            _packCancel = cancellation
            Try
                Dim manifest = Await Task.Run(Function() Sspcl.Core.Modpack.ModpackImporter.Inspect(dialog.FileName))
                Dim options As New PackOptionsWindow(False, manifest, ModMain.StarsectorPaths)
                If options.ShowDialog() <> True Then Return
                _status.Text = "正在创建独立版本并导入整合包…"
                Dim cancelButton As New Button With {.Content = "取消导入", .Padding = New Thickness(8, 4, 8, 4)}
                AddHandler cancelButton.Click, Sub() cancellation.Cancel()
                _gamePanel.Children.Add(cancelButton)
                Dim gamePath = options.GamePath
                Dim targetPath = options.TargetPath
                Dim pool = ModMain.ModPoolDir()
                Dim selectedOptions = options.Options
                Dim result = Await Task.Run(Function() Sspcl.Core.Modpack.ModpackImporter.Import(dialog.FileName, gamePath, targetPath, pool, selectedOptions, cancellation.Token))
                ModMain.StarsectorPaths.Add(result.Path)
                ModMain.SelectedFolderPath = result.Path
                ModMain.SaveStarsectorPaths()
                _status.Text = "整合包已导入，右侧已生成独立版本。" & If(result.MissingMods.Count > 0, "缺失 " & result.MissingMods.Count & " 个 MOD，请单独下载后启用。", "")
                RefreshCards()
                _gamePanel.Children.Remove(cancelButton)
            Catch ex As OperationCanceledException
                _status.Text = "导入已取消，基础游戏没有改动。"
            Catch ex As Exception
                _status.Text = "导入失败：" & ex.Message
            Finally
                _packBusy = False
                _packCancel = Nothing
                For Each taskButton As Button In _gamePanel.Children.OfType(Of Button)().Where(Function(b) CStr(b.Content) = "取消导入").ToList()
                    _gamePanel.Children.Remove(taskButton)
                Next
            End Try
        End Using
    End Function

    Private Async Sub DownloadGame_Click(sender As Object, e As MouseButtonEventArgs)
        If _downloadCancel IsNot Nothing Then Return
        Dim url = InputBox("游戏下载地址（可修改，默认使用论坛提供的最新包）：", "下载游戏", DefaultGameDownloadUrl)
        If String.IsNullOrWhiteSpace(url) Then Return
        Dim uri As Uri = Nothing
        If Not Uri.TryCreate(url, UriKind.Absolute, uri) OrElse (uri.Scheme <> "https" AndAlso uri.Scheme <> "http") Then
            _status.Text = "请输入 HTTP(S) 下载地址。" : Return
        End If
        Dim dialog As New Microsoft.Win32.SaveFileDialog With {.FileName = "Starsector-setup.exe", .Filter = "游戏安装包|*.exe;*.zip;*.rar;*.7z|所有文件|*.*", .OverwritePrompt = True, .Title = "保存游戏安装包"}
        If dialog.ShowDialog() <> True Then Return
        Using cancellation As New CancellationTokenSource()
            _downloadCancel = cancellation
            Dim progressBar As New ProgressBar With {.Height = 5, .Maximum = 100, .IsIndeterminate = True, .Margin = New Thickness(0, 6, 0, 6)}
            Dim label As New TextBlock With {.Text = "正在连接游戏下载服务器…", .TextWrapping = TextWrapping.Wrap}
            Dim cancelButton As New Button With {.Content = "取消游戏下载", .HorizontalAlignment = HorizontalAlignment.Left, .Padding = New Thickness(8, 4, 8, 4)}
            Dim receiving = True
            AddHandler cancelButton.Click, Sub()
                receiving = False
                cancelButton.IsEnabled = False
                label.Text = "正在取消游戏下载…"
                Task.Run(Sub()
                Try
                    cancellation.Cancel()
                Catch ex As ObjectDisposedException
                End Try
                End Sub)
            End Sub
            _gamePanel.Children.Add(label) : _gamePanel.Children.Add(progressBar) : _gamePanel.Children.Add(cancelButton)
            Try
                Dim progress As New Progress(Of Sspcl.Core.Store.Forum.ModDownloadProgress)(Sub(p)
                    If Not receiving Then Return
                    progressBar.IsIndeterminate = Not p.Percent.HasValue
                    progressBar.Value = p.Percent.GetValueOrDefault()
                    label.Text = "游戏下载：" & (p.ReceivedBytes / 1048576.0).ToString("F1") & " MB" & If(p.Percent.HasValue, "（" & p.Percent.Value.ToString("F0") & "%）", "")
                End Sub)
                Using http As New HttpClient With {.Timeout = TimeSpan.FromSeconds(45)}
                    Dim downloader As New Sspcl.Core.Downloads.HttpPackageDownloader(http)
                    Using package = Await Task.Run(Function() downloader.DownloadAsync(uri, progress, cancellation.Token, True))
                        receiving = False
                        cancellation.Token.ThrowIfCancellationRequested()
                        cancelButton.IsEnabled = False
                        Dim destination = IO.Path.ChangeExtension(dialog.FileName, IO.Path.GetExtension(package.Path))
                        Await Task.Run(Sub() IO.File.Copy(package.Path, destination, True))
                        label.Text = "已保存游戏包：" & destination & "。完成安装或解压后，用左侧「添加文件夹」添加游戏。"
                    End Using
                End Using
            Catch ex As OperationCanceledException When cancellation.IsCancellationRequested
                _status.Text = "游戏下载已取消，可重新下载。"
            Catch ex As Exception
                label.Text = "游戏下载失败：" & ex.Message
            Finally
                receiving = False
                _downloadCancel = Nothing
                progressBar.IsIndeterminate = False
                cancelButton.IsEnabled = False
                If cancellation.IsCancellationRequested Then
                    _gamePanel.Children.Remove(label)
                    _gamePanel.Children.Remove(progressBar)
                    _gamePanel.Children.Remove(cancelButton)
                End If
            End Try
        End Using
    End Sub

    Private Sub UseInstall(path As String)
        Logger.Info("UseInstall=" & path)
        ModMain.StarsectorPath = path
        ModMain.SelectedFolderPath = path
        If Not ModMain.StarsectorPaths.Contains(path) Then ModMain.StarsectorPaths.Add(path)
        ModMain.SaveStarsectorPaths()
        CType(ModMain.FrmStarsectorLeft, PageStarsectorLeft).RefreshInfo()
        CType(ModMain.FrmStarsectorRight, PageStarsectorRight).RefreshAll()
        ModMain.FrmMain.PageChange(FormMain.PageType.Launch)
    End Sub

    Private Sub RemoveInstall(path As String)
        If String.Equals(path, ModMain.StarsectorPath, StringComparison.OrdinalIgnoreCase) Then
            MsgBox("无法移除正在使用的版本。", MsgBoxStyle.Exclamation, "移除")
            Return
        End If
        ModMain.StarsectorPaths.Remove(path)
        ModMain.SaveStarsectorPaths()
        ModMain.SelectedFolderPath = ModMain.StarsectorPath
        ModMain.FrmStarsectorSelectLeft.RefreshList()
        RefreshCards()
    End Sub

End Class
