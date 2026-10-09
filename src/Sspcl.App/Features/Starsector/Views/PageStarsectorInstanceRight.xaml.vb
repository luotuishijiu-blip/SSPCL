Imports Sspcl.Core.Mods
Imports Sspcl.Core.Launch
Imports Sspcl.Core.Store.Forum

Public Class PageStarsectorInstanceRight

    Private _mods As List(Of ModSpec)
    Private _enabled As HashSet(Of String)
    Private _loading As Boolean = False
    Private _modOperation As Boolean = False
    Private ReadOnly _modBoxes As New List(Of MyCheckBox)
    Private ReadOnly _memoryTimer As New System.Windows.Threading.DispatcherTimer With {.Interval = TimeSpan.FromSeconds(2)}
    Private _readingMemory As Boolean
    Private _updateCancel As CancellationTokenSource
    Private Shared _lastTab As Integer = 2

    Private Sub Init() Handles Me.Loaded
        RemoveHandler _memoryTimer.Tick, AddressOf MemoryTimer_Tick
        AddHandler _memoryTimer.Tick, AddressOf MemoryTimer_Tick
        _memoryTimer.Start()
        PanBack.ScrollToHome()
        ShowTab(_lastTab)
    End Sub

    Private Sub LeavePage() Handles Me.Unloaded
        _memoryTimer.Stop()
    End Sub

    Private Async Sub MemoryTimer_Tick(sender As Object, e As EventArgs)
        If PanOverview.Visibility = Visibility.Visible Then Await RefreshMemoryAsync()
    End Sub

    Private Async Function RefreshMemoryAsync() As Task
        If _readingMemory Then Return
        _readingMemory = True
        Dim gamePath = ModMain.StarsectorPath
        Try
            Dim text = Await Task.Run(Function()
                Dim allocation As String
                Try
                    Dim memory = ModStarsector.ReadMemoryOf(gamePath)
                    allocation = "内存分配：最大堆 Xmx " & memory.XmxMb & " MB · 初始堆 Xms " & memory.XmsMb & " MB"
                Catch ex As Exception
                    allocation = "内存分配：无法读取 vmparams（" & ex.Message & "）"
                End Try
                Dim usage = GameMemoryMonitor.Read(gamePath)
                Dim runtime = If(usage.ProcessCount = 0, "实际占用：未检测到可读取的当前版本游戏进程", "实际物理内存：" & (usage.WorkingSetBytes / 1048576.0).ToString("F0") & " MB · 私有内存：" & (usage.PrivateBytes / 1048576.0).ToString("F0") & " MB（" & usage.ProcessCount & " 个游戏进程）")
                Return allocation & vbCrLf & runtime
            End Function)
            If String.Equals(gamePath, ModMain.StarsectorPath, StringComparison.OrdinalIgnoreCase) Then LabOverviewMemory.Text = text
        Catch ex As Exception
            LabOverviewMemory.Text = "内存信息读取失败：" & ex.Message
        Finally
            _readingMemory = False
        End Try
    End Function

    Public Sub ShowTab(tag As Integer)
        _lastTab = tag
        Logger.Info("ShowTab tag=" & tag)
        PanMemory.Visibility = If(tag = 1, Visibility.Visible, Visibility.Collapsed)
        PanOverview.Visibility = If(tag = 2, Visibility.Visible, Visibility.Collapsed)
        PanMod.Visibility = If(tag = 3, Visibility.Visible, Visibility.Collapsed)
        PanExport.Visibility = If(tag = 4, Visibility.Visible, Visibility.Collapsed)
        PanSettings.Visibility = If(tag = 5, Visibility.Visible, Visibility.Collapsed)
        Select Case tag
            Case 1 : LoadMemory()
            Case 2 : LoadOverview()
            Case 3 : LoadMods()
            Case 5 : LoadSettingsJson()
        End Select
    End Sub

    Private Async Sub LoadOverview()
        Try
            Dim install = ModStarsector.Detect(ModMain.StarsectorPath)
            If install.IsValid Then
                LabOverview.Text = "游戏版本：" & install.Version & vbCrLf &
                    "汉化包：" & install.LocalizationPackageVersion & vbCrLf &
                    "Mod 数量：" & install.ModCount & " 个" & vbCrLf &
                    "存档数量：" & install.SaveCount & " 个" & vbCrLf &
                    "安装目录：" & install.Path
            Else
                LabOverview.Text = "未检测到有效的远行星号安装（" & ModMain.StarsectorPath & "）"
            End If
        Catch ex As Exception
            LabOverview.Text = "读取失败：" & ex.Message
        End Try
        Await RefreshMemoryAsync()
    End Sub

    Private Sub LoadMemory()
        ChkFollowGlobal.Checked = Settings.Get(Of Boolean)("StarsectorFollowGlobalMem")
        PanMemManual.IsEnabled = Not ChkFollowGlobal.Checked
        BtnSaveMem.IsEnabled = Not ChkFollowGlobal.Checked
        If ChkFollowGlobal.Checked Then
            LabMemHint.Text = "跟随全局设置：本版本使用 设置 → 启动 中的内存分配。"
            Return
        End If
        Try
            Dim mem = ModStarsector.ReadMemoryOf(ModMain.StarsectorPath)
            BoxXmx.Text = mem.XmxMb.ToString()
            BoxXms.Text = mem.XmsMb.ToString()
        Catch ex As Exception
            LabMemHint.Text = "读取失败：" & ex.Message
        End Try
    End Sub

    Private Sub ChkFollowGlobal_Change(sender As Object, user As Boolean) Handles ChkFollowGlobal.Change
        If _loading Then Return
        Settings.Set("StarsectorFollowGlobalMem", ChkFollowGlobal.Checked)
        PanMemManual.IsEnabled = Not ChkFollowGlobal.Checked
        BtnSaveMem.IsEnabled = Not ChkFollowGlobal.Checked
        If ChkFollowGlobal.Checked Then
            LabMemHint.Text = "跟随全局设置：本版本使用 设置 → 启动 中的内存分配。"
        Else
            LabMemHint.Text = "保存后写入 vmparams 文件，下次启动生效。"
        End If
    End Sub

    Private Sub LoadMods()
        If _modOperation Then Return
        _loading = True
        PanModList.Children.Clear()
        _modBoxes.Clear()
        Try
            _mods = ModStarsector.ScanMods(ModStarsector.ModsDir(ModMain.StarsectorPath)).Concat(ModStarsector.ScanMods(ModMain.ModPoolDir())).Where(Function(m) Not String.IsNullOrWhiteSpace(m.Id)).GroupBy(Function(m) m.Id).Select(Function(group) group.First()).ToList()
            _enabled = New HashSet(Of String)(ModStarsector.ReadEnabled(ModMain.StarsectorPath))
            Dim ok = 0
            For Each m In _mods
                If String.IsNullOrWhiteSpace(m.Id) Then Continue For
                ok += 1
                Dim box As New MyCheckBox With {
                    .Text = m.Name & "  [" & m.Id & "]  " & m.VersionRaw,
                    .ToolTip = "适配游戏：" & m.GameVersionRaw & vbCrLf & m.Path,
                    .Checked = _enabled.Contains(m.Id),
                    .Tag = m.Id,
                    .Margin = New Thickness(0, 0, 0, 6)
                }
                AddHandler box.Change, AddressOf ModCheck_Changed
                _modBoxes.Add(box)
                Dim row As New DockPanel With {.Margin = New Thickness(0, 0, 0, 8)}
                Dim actions As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(10, 0, 0, 0)}
                DockPanel.SetDock(actions, Dock.Right)
                Dim online As New Button With {.Content = "论坛更新", .Tag = m, .Padding = New Thickness(8, 3, 8, 3)}
                Dim local As New Button With {.Content = "本地更新", .Tag = m, .Padding = New Thickness(8, 3, 8, 3), .Margin = New Thickness(6, 0, 0, 0)}
                AddHandler online.Click, AddressOf OnlineUpdate_Click
                AddHandler local.Click, AddressOf LocalUpdate_Click
                actions.Children.Add(online) : actions.Children.Add(local)
                row.Children.Add(actions) : row.Children.Add(box)
                PanModList.Children.Add(row)
            Next
            If ok = 0 Then
                LabModSummary.Text = "mod 池为空——请到「下载」页下载 mod，或在「设置 → 其他」点「打开 mod 池」手动放入。"
            Else
                LabModSummary.Text = "共 " & ok & " 个 MOD（游戏目录与 MOD 池）。勾选即启用，前置依赖自动勾选；更新只覆盖适配当前游戏的完整包。"
            End If
        Catch ex As Exception
            LabModSummary.Text = "扫描失败：" & ex.Message
        Finally
            _loading = False
        End Try
    End Sub

    Private Async Sub ModCheck_Changed(sender As Object, user As Boolean)
        If _loading OrElse _modOperation OrElse Not user Then Return
        Dim box = TryCast(sender, MyCheckBox)
        If box Is Nothing Then Return
        Dim id As String = If(box.Tag Is Nothing, "", box.Tag.ToString())
        If id = "" Then Return
        Dim desired As New HashSet(Of String)(_enabled)
        If box.Checked Then desired.Add(id) Else desired.Remove(id)
        Dim gamePath = ModMain.StarsectorPath
        Dim pool = _mods.ToList()
        _modOperation = True
        PanModList.IsEnabled = False
        LabModSummary.Text = "正在后台应用 MOD 选择…"
        Dim entered As Boolean = False
        Try
            Await PageStarsectorDownload.WaitForInstallAsync(CancellationToken.None)
            entered = True
            _enabled = Await Task.Run(Function() ModActivationService.Apply(gamePath, pool, desired))
            LabModSummary.Text = "已保存 MOD 选择。前置依赖自动启用；停用保留文件，再次启用无需重复复制。"
            If Not box.Checked AndAlso _enabled.Contains(id) Then LabModSummary.Text &= " 该 MOD 是其他已启用 MOD 的前置，因此仍保持启用。"
        Catch ex As Exception
            LabModSummary.Text = "应用 MOD 选择失败：" & ex.Message
        Finally
            If entered Then PageStarsectorDownload.ReleaseInstall()
            _loading = True
            For Each cb In _modBoxes
                If cb.Tag IsNot Nothing Then cb.Checked = _enabled.Contains(cb.Tag.ToString())
            Next
            _loading = False
            _modOperation = False
            PanModList.IsEnabled = True
            If Not String.Equals(gamePath, ModMain.StarsectorPath, StringComparison.OrdinalIgnoreCase) Then LoadMods()
        End Try
    End Sub

    Private Async Sub OnlineUpdate_Click(sender As Object, e As RoutedEventArgs)
        Dim current = TryCast(DirectCast(sender, Button).Tag, ModSpec)
        If current IsNot Nothing Then Await UpdateModAsync(current, Nothing)
    End Sub

    Private Async Sub LocalUpdate_Click(sender As Object, e As RoutedEventArgs)
        If _modOperation Then Return
        Dim current = TryCast(DirectCast(sender, Button).Tag, ModSpec)
        If current Is Nothing Then Return
        Dim dialog As New Microsoft.Win32.OpenFileDialog With {.Title = "选择 " & current.Name & " 的完整更新包", .Filter = "MOD 压缩包|*.zip;*.rar;*.7z", .CheckFileExists = True}
        If dialog.ShowDialog() = True Then Await UpdateModAsync(current, dialog.FileName)
    End Sub

    Private Sub CancelUpdate_Click(sender As Object, e As RoutedEventArgs)
        Dim cancellation = _updateCancel
        If cancellation Is Nothing Then Return
        BtnCancelUpdate.IsEnabled = False
        LabModSummary.Text = "正在取消更新下载…"
        Task.Run(Sub()
            Try
                cancellation.Cancel()
            Catch ex As ObjectDisposedException
            End Try
        End Sub)
    End Sub

    Private Async Function UpdateModAsync(current As ModSpec, localArchive As String) As Task
        If _modOperation Then Return
        _modOperation = True
        PanModList.IsEnabled = False
        Dim gamePath = ModMain.StarsectorPath
        Dim poolPath = ModMain.ModPoolDir()
        Dim downloaded As DownloadedModArchive = Nothing
        Dim entered As Boolean = False
        Dim receiving As Boolean = False
        Dim finalStatus As String = Nothing
        Using cancellation As New CancellationTokenSource()
            _updateCancel = cancellation
            Try
                Dim usage = Await Task.Run(Function() GameMemoryMonitor.Read(gamePath))
                If usage.ProcessCount > 0 Then
                    finalStatus = "请先关闭当前版本游戏，再更新 MOD。"
                    Return
                End If
                Dim path = localArchive
                If path Is Nothing Then
                    LabModSummary.Text = "正在读取缓存并检查论坛发布包…"
                    Dim catalog As ForumCatalogResult = Nothing
                    Dim warning As String = Nothing
                    Try
                        catalog = Await PageStarsectorDownload.GetForumCatalogAsync()
                        warning = catalog.Warning
                    Catch ex As Exception
                        warning = "读取论坛目录失败：" & ex.Message
                    End Try
                    cancellation.Token.ThrowIfCancellationRequested()
                    Dim candidates = If(catalog Is Nothing OrElse catalog.Catalog.Mods.Data Is Nothing, New List(Of ForumMod)(), catalog.Catalog.Mods.Data.Where(Function(m) m.Id = current.Id).ToList())
                    Dim gameVersion = (Await Task.Run(Function() ModStarsector.Detect(gamePath))).Version
                    Dim window As New ModUpdateWindow(current, gameVersion, candidates, warning) With {.Owner = ModMain.FrmMain}
                    If window.ShowDialog() <> True Then
                        finalStatus = "已关闭更新选择，旧版 MOD 保持不变。"
                        Return
                    End If
                    BtnCancelUpdate.Visibility = Visibility.Visible
                    BtnCancelUpdate.IsEnabled = True
                    receiving = True
                    Dim progress As New Progress(Of ModDownloadProgress)(Sub(p)
                        If receiving AndAlso Not cancellation.IsCancellationRequested Then LabModSummary.Text = "正在下载更新：" & current.Name & " · " & (p.ReceivedBytes / 1048576.0).ToString("F1") & " MB" & If(p.Percent.HasValue, "（" & p.Percent.Value.ToString("F0") & "%）", "")
                    End Sub)
                    Dim choice = window.SelectedChoice
                    downloaded = Await Task.Run(Function() PageStarsectorDownload.DownloadUpdateAsync(choice.ModInfo, choice.Release, progress, cancellation.Token))
                    receiving = False
                    path = downloaded.Path
                End If
                LabModSummary.Text = "等待验证并完整替换旧版 MOD…"
                Await PageStarsectorDownload.WaitForInstallAsync(cancellation.Token)
                entered = True
                cancellation.Token.ThrowIfCancellationRequested()
                usage = Await Task.Run(Function() GameMemoryMonitor.Read(gamePath))
                If usage.ProcessCount > 0 Then
                    finalStatus = "游戏正在运行，已保留旧版 MOD。请关闭游戏后重试。"
                    Return
                End If
                BtnCancelUpdate.IsEnabled = False
                Dim result = Await Task.Run(Function() ModUpdateService.UpdateArchive(path, current.Id, poolPath, gamePath))
                finalStatus = If(result.Success, "已更新：" & result.Name & "。旧目录已完整替换，启用状态保持不变。" & If(String.IsNullOrWhiteSpace(result.Error), "", vbCrLf & result.Error), "更新失败：" & result.Error)
            Catch ex As OperationCanceledException When cancellation.IsCancellationRequested
                finalStatus = "更新下载已取消，旧版 MOD 保持不变。"
            Catch ex As Exception
                finalStatus = "更新失败：" & ex.Message
            Finally
                receiving = False
                If entered Then PageStarsectorDownload.ReleaseInstall()
                If downloaded IsNot Nothing Then downloaded.Dispose()
                _updateCancel = Nothing
                BtnCancelUpdate.Visibility = Visibility.Collapsed
                _modOperation = False
                PanModList.IsEnabled = True
                LoadMods()
                If finalStatus IsNot Nothing Then LabModSummary.Text = finalStatus
            End Try
        End Using
    End Function

    Private Sub BtnSaveMem_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSaveMem.Click
        Try
            Dim xmx = Integer.Parse(BoxXmx.Text.Trim())
            Dim xms = Integer.Parse(BoxXms.Text.Trim())
            ModStarsector.WriteMemory(ModMain.StarsectorPath, xmx, xms)
            LabMemHint.Text = "已保存（Xmx " & xmx & " MB · Xms " & xms & " MB）"
        Catch ex As Exception
            MsgBox("保存失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

    Private Sub BtnRescan_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRescan.Click
        LoadMods()
    End Sub

    Private Sub BtnExportClip_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnExportClip.Click
        Try
            Dim ids = ModStarsector.ReadEnabled(ModMain.StarsectorPath)
            Clipboard.SetText(String.Join(Environment.NewLine, ids))
            MsgBox("已复制 " & ids.Count & " 个 mod id 到剪贴板", MsgBoxStyle.Information, "导出")
        Catch ex As Exception
            MsgBox("导出失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

    Private Function SettingsJsonPath() As String
        Return IO.Path.Combine(ModMain.StarsectorPath, "starsector-core\data\config\settings.json")
    End Function

    Private Sub LoadSettingsJson()
        Try
            BoxSettingsJson.Text = IO.File.ReadAllText(SettingsJsonPath())
            LabSettingsHint.Text = "当前文件：" & SettingsJsonPath()
        Catch ex As Exception
            LabSettingsHint.Text = "读取失败：" & ex.Message
        End Try
    End Sub

    Private Sub BtnSaveSettings_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSaveSettings.Click
        Try
            IO.File.WriteAllText(SettingsJsonPath(), BoxSettingsJson.Text)
            LabSettingsHint.Text = "已保存 settings.json"
        Catch ex As Exception
            MsgBox("保存失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

    Private Sub BtnReloadSettings_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnReloadSettings.Click
        LoadSettingsJson()
    End Sub

End Class
