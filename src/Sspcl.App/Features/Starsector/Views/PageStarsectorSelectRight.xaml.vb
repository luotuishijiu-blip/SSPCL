Imports Sspcl.Core.Modpack
Imports System.Windows.Media

Public Class PageStarsectorSelectRight

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        ModMain.SelectedFolderPath = ModMain.StarsectorPath
        RefreshCards()
    End Sub

    Public Async Sub AddFolder()
        If TypeOf ModMain.FrmStarsectorDownload Is PageStarsectorDownload AndAlso DirectCast(ModMain.FrmStarsectorDownload, PageStarsectorDownload).HasActiveTransfers() Then
            _status.Text = "请等待下载与安装完成，或取消任务后更换共享目录。" : Return
        End If
        ModMain.InitializeSharedProfiles()
        If ModMain.FrmStarsectorInstanceRight IsNot Nothing AndAlso Not ModMain.FrmStarsectorInstanceRight.ConfirmDiscardDraft() Then Return
        Using dialog As New System.Windows.Forms.FolderBrowserDialog With {.Description = "选择统一维护的远行星号游戏目录"}
            Try
            If dialog.ShowDialog() <> System.Windows.Forms.DialogResult.OK Then Return
            Dim path = dialog.SelectedPath
            If Not ModStarsector.Detect(path).IsValid Then
                _status.Text = "请选择有效游戏目录。" : Return
            End If
            If Sspcl.Core.Launch.GameMemoryMonitor.Read(ModMain.StarsectorPath).ProcessCount > 0 Then
                _status.Text = "请关闭游戏后更换共享目录。" : Return
            End If
            If Not ModMain.StarsectorPaths.Contains(ModMain.StarsectorPath) Then ModMain.StarsectorPaths.Add(ModMain.StarsectorPath)
            ModMain.StarsectorPath = path
            If Not ModMain.StarsectorPaths.Contains(path) Then ModMain.StarsectorPaths.Add(path)
            ModMain.SaveStarsectorPaths()
            Settings.Set("StarsectorSharedGame", path) : Settings.Set("StarsectorSharedMigration", False)
            Await ModMain.EnsureSharedModsAsync()
            If ModMain.ActiveProfileId = "" Then ModMain.ActiveProfileId = ModMain.ProfileStore().Save("默认 MOD 方案", ModStarsector.ReadEnabled(path)).Id
            _status.Text = "共享游戏目录：" & path & "。所有启用方案共用此目录的 MOD 文件。"
            RefreshCards()
            Catch ex As Exception
                _status.Text = "设置共享目录失败：" & ex.Message
            End Try
        End Using
    End Sub

    Private _packBusy As Boolean
    Private _packCancel As CancellationTokenSource
    Private _downloadCancel As CancellationTokenSource
    Private ReadOnly _status As New TextBlock With {.TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 10)}
    Private ReadOnly _gamePanel As New StackPanel With {.Margin = New Thickness(0, 0, 0, 12)}
    Public Const DefaultGameDownloadUrl As String = "https://cdn.fossic.cn/forum/202609/05/014446l82kxv7e16f63953.attach"

    Public Sub RefreshCards()
        Try
            ModMain.InitializeSharedProfiles()
            PanMain.Children.Clear() : PanMain.Children.Add(_status) : PanMain.Children.Add(_gamePanel)
            PanMain.Children.Add(New TextBlock With {.Text = "MOD 启用方案 · 共用游戏：" & ModMain.StarsectorPath, .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 12)})
            Dim mods = Sspcl.Core.Mods.ModScanner.Scan(ModMain.SharedModsDir())
            Dim store = ModMain.ProfileStore(), profiles = store.List()
            For Each profile In profiles
                PanMain.Children.Add(BuildProfileCard(profile, store.DirectoryOf(profile.Id), mods, profile.Id = ModMain.ActiveProfileId))
            Next
            If profiles.Count = 0 Then PanMain.Children.Add(New TextBlock With {.Text = "添加共享游戏目录后，在版本设置 → MOD 管理中选择 MOD 并保存启用方案。", .TextWrapping = TextWrapping.Wrap})
        Catch ex As Exception
            _status.Text = "读取启用方案失败：" & ex.Message
        End Try
    End Sub
    Private Function BuildProfileCard(profile As Sspcl.Core.Mods.ModProfile, directory As String, mods As IReadOnlyList(Of Sspcl.Core.Mods.ModSpec), selected As Boolean) As MyCard
        Dim card As New MyCard With {.Title = profile.Name, .ToolTip = directory, .Margin = New Thickness(0, 0, 0, 12)}
        Dim panel As New StackPanel With {.Margin = New Thickness(20, 38, 20, 18)}
        panel.Children.Add(New TextBlock With {.Text = profile.EnabledMods.Count & " 项启用 MOD · " & If(selected, "当前启动方案", "已保存方案")})
        panel.Children.Add(New TextBlock With {.Text = "启动清单目录：" & directory, .TextWrapping = TextWrapping.Wrap, .FontSize = 11, .Margin = New Thickness(0, 6, 0, 0)})
        Dim missing = Sspcl.Core.Mods.ModProfileStore.Missing(profile.EnabledMods, mods)
        If missing.Count > 0 Then panel.Children.Add(New TextBlock With {.Text = "缺失 MOD / 前置：" & String.Join("、", missing), .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 6, 0, 0)})
        Dim row As New WrapPanel With {.Margin = New Thickness(0, 10, 0, 0)}
        Dim useButton As New MyButton With {.Text = If(selected, "编辑启用 MOD", "使用此版本"), .Padding = New Thickness(12, 5, 12, 5)}
        AddHandler useButton.Click, Async Sub(sender, args) Await UseProfileAsync(profile.Id)
        row.Children.Add(useButton)
        Dim exportButton As New MyButton With {.Text = "选为导出来源", .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(8, 0, 0, 0)}
        AddHandler exportButton.Click, Sub(sender, args)
            _exportProfile = profile.Id : _status.Text = "导出来源：" & profile.Name
        End Sub
        row.Children.Add(exportButton) : panel.Children.Add(row) : card.BorderChild = panel
        Return card
    End Function
    Private _exportProfile As String
    Private Async Function UseProfileAsync(id As String) As Task
        Dim editing = id = ModMain.ActiveProfileId
        If ModMain.FrmStarsectorInstanceRight IsNot Nothing AndAlso Not ModMain.FrmStarsectorInstanceRight.ConfirmDiscardDraft() Then Return
        Try
            Await ModMain.EnsureSharedModsAsync()
            Await PageStarsectorDownload.WaitForInstallAsync(CancellationToken.None)
            Try
                If Sspcl.Core.Launch.GameMemoryMonitor.Read(ModMain.StarsectorPath).ProcessCount > 0 Then Throw New InvalidOperationException("请关闭游戏后切换启用方案。")
                Dim profile = ModMain.ProfileStore().Read(id), game = ModMain.StarsectorPath
                Dim mods = ModStarsector.ScanMods(ModMain.SharedModsDir())
                Await Task.Run(Sub() ModStarsector.WriteEnabled(game, Sspcl.Core.Mods.ModProfileStore.ResolveSelection(profile.EnabledMods, mods).Where(Function(value) mods.Any(Function(m) m.Id = value))))
                ModMain.ActiveProfileId = id
            Finally
                PageStarsectorDownload.ReleaseInstall()
            End Try
            If ModMain.FrmStarsectorLeft IsNot Nothing Then CType(ModMain.FrmStarsectorLeft, PageStarsectorLeft).RefreshInfo()
            RefreshCards()
            If editing Then
                ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSetup)
                ModMain.FrmStarsectorInstanceRight.ShowTab(3)
            Else
                ModMain.FrmMain.PageChange(FormMain.PageType.Launch)
            End If
        Catch ex As Exception
            _status.Text = "切换失败：" & ex.Message
        End Try
    End Function

    Public Async Function ExportPackAsync() As Task
        If _packBusy Then Return
        Dim options As New PackOptionsWindow(True, New Sspcl.Core.Modpack.PackManifest(), {ModMain.StarsectorPath})
        If options.ShowDialog() <> True Then Return
        Dim dialog As New Microsoft.Win32.SaveFileDialog With {.FileName = "整合包.sspack", .Filter = "SSPCL 整合包|*.sspack|ZIP 压缩包|*.zip", .DefaultExt = ".sspack", .OverwritePrompt = True}
        If dialog.ShowDialog() <> True Then Return
        Dim sourcePath = ModMain.StarsectorPath
        Dim profileId = If(String.IsNullOrWhiteSpace(_exportProfile), ModMain.ActiveProfileId, _exportProfile)
        If String.IsNullOrWhiteSpace(profileId) Then
            _status.Text = "请先保存启用方案。" : Return
        End If
        Dim profile = ModMain.ProfileStore().Read(profileId)
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
                Await Task.Run(Function() Sspcl.Core.Modpack.ModpackExporter.Export(sourcePath, dialog.FileName, name, selectedOptions, cancellation.Token, profile.EnabledMods, IO.Path.Combine(ModMain.ProfileStore().DirectoryOf(profileId), "payload")))
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
                Dim options As New PackOptionsWindow(False, manifest, {ModMain.StarsectorPath})
                If options.ShowDialog() <> True Then Return
                _status.Text = "正在导入启用方案与可选配置、存档…"
                Dim cancelButton As New Button With {.Content = "取消导入", .Padding = New Thickness(8, 4, 8, 4)}
                AddHandler cancelButton.Click, Sub() cancellation.Cancel()
                _gamePanel.Children.Add(cancelButton)
                Dim store = ModMain.ProfileStore(), selectedOptions = options.Options
                Dim profile = Await Task.Run(Function() Sspcl.Core.Modpack.ProfilePackImporter.Import(dialog.FileName, store, selectedOptions, cancellation.Token))
                _exportProfile = profile.Id
                _status.Text = "整合包已导入为启用方案「" & profile.Name & "」。缺失 MOD 可在共享目录下载；选择此方案后启动时应用配置。"
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

End Class
