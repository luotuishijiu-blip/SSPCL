Imports Sspcl.Core.Mods
Imports Sspcl.Core.Launch
Imports Sspcl.Core.Store.Forum

Public Class PageStarsectorInstanceRight

    Private _mods As List(Of ModSpec)
    Private _enabled As HashSet(Of String)
    Private _loading As Boolean = False
    Private _modOperation As Boolean = False
    Private _draftDirty As Boolean
    Private _draftProfile As String
    Private _modRows As New List(Of ModRow)
    Public Class ModRow
        Implements ComponentModel.INotifyPropertyChanged
        Public Property Id As String
        Public Property Label As String
        Public Property Details As String
        Public Property [Mod] As ModSpec
        Public Property Changed As Action(Of String, Boolean)
        Private _checked As Boolean
        Public Property IsChecked As Boolean
            Get
                Return _checked
            End Get
            Set(value As Boolean)
                If value = _checked Then Return
                _checked = value
                RaiseEvent PropertyChanged(Me, New ComponentModel.PropertyChangedEventArgs(NameOf(IsChecked)))
                If Changed IsNot Nothing Then Changed.Invoke(Id, value)
            End Set
        End Property
        Public Event PropertyChanged As ComponentModel.PropertyChangedEventHandler Implements ComponentModel.INotifyPropertyChanged.PropertyChanged
    End Class
    Private Sub BindModRows()
        _modRows = _mods.Select(Function(m) New ModRow With {.Id = m.Id, .Label = m.Name & "  [" & m.Id & "]  " & m.VersionRaw, .Details = m.Path, .Mod = m, .IsChecked = _enabled.Contains(m.Id), .Changed = AddressOf RowChanged}).ToList()
        Dim installed As New HashSet(Of String)(_mods.Select(Function(m) m.Id))
        For Each id In _enabled.Where(Function(value) Not installed.Contains(value))
            _modRows.Add(New ModRow With {.Id = id, .Label = "缺失 MOD · " & id, .IsChecked = True, .Changed = AddressOf RowChanged})
        Next
        PanModList.ItemsSource = _modRows
    End Sub
    Private Sub RowChanged(id As String, value As Boolean)
        If _loading OrElse _modOperation Then Return
        Dim desired As New HashSet(Of String)(_enabled)
        If value Then desired.Add(id) Else desired.Remove(id)
        ApplyDraft(desired)
    End Sub
    Private Sub ApplyDraft(desired As IEnumerable(Of String))
        _enabled = ModProfileStore.ResolveSelection(desired, _mods)
        _draftDirty = True : _loading = True
        Try
            For Each row In _modRows
                row.IsChecked = _enabled.Contains(row.Id)
            Next
        Finally
            _loading = False
        End Try
        LabModSummary.Text = "已暂存 " & _enabled.Count & " 项选择，尚未保存。请点击上方蓝色保存按钮。"
    End Sub
    Private Sub SelectAllMods(sender As Object, e As RoutedEventArgs)
        If Not _loading AndAlso Not _modOperation AndAlso _enabled IsNot Nothing Then ApplyDraft(_enabled.Concat(_mods.Select(Function(m) m.Id)).ToList())
    End Sub
    Private Sub ClearAllMods(sender As Object, e As RoutedEventArgs)
        If Not _loading AndAlso Not _modOperation AndAlso _enabled IsNot Nothing Then ApplyDraft(New String() {})
    End Sub
    Private Async Sub SelectModsFolder(sender As Object, e As RoutedEventArgs)
        If _loading OrElse _modOperation Then Return
        Try
            Using dialog As New System.Windows.Forms.FolderBrowserDialog With {.Description = "选择 MOD 集文件夹，可直接选择已有的 mods，无需改名"}
                If dialog.ShowDialog() <> System.Windows.Forms.DialogResult.OK Then Return
                If TypeOf ModMain.FrmStarsectorDownload Is PageStarsectorDownload AndAlso DirectCast(ModMain.FrmStarsectorDownload, PageStarsectorDownload).HasActiveTransfers() Then Throw New InvalidOperationException("请结束下载和安装后选择 MOD 文件夹。")
                Await PageStarsectorDownload.WaitForInstallAsync(CancellationToken.None)
                Try
                    If GameMemoryMonitor.Read(ModMain.StarsectorPath).ProcessCount > 0 Then Throw New InvalidOperationException("请先关闭游戏。")
                    ModMain.SetModsFolder(dialog.SelectedPath)
                    If IO.File.Exists(IO.Path.Combine(dialog.SelectedPath, "enabled_mods.json")) Then
                        _enabled = New HashSet(Of String)(ModStarsector.ReadEnabled(ModMain.StarsectorPath))
                        _draftProfile = ModMain.ActiveProfileId : _draftDirty = True
                    End If
                Finally
                    PageStarsectorDownload.ReleaseInstall()
                End Try
                LoadMods()
            End Using
        Catch ex As Exception
            LabModSummary.Text = "选择 MOD 文件夹失败：" & ex.Message
        End Try
    End Sub
    Public Function ConfirmDiscardDraft() As Boolean
        If _modOperation OrElse _loading Then Return False
        If Not _draftDirty Then Return True
        If MessageBox.Show("MOD 勾选尚未保存，是否丢弃修改并切换？", "未保存的方案", MessageBoxButton.YesNo) <> MessageBoxResult.Yes Then Return False
        _draftDirty = False : Return True
    End Function
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

    Private Async Sub LoadMods()
        If _modOperation OrElse _loading Then Return
        _loading = True
        PanModList.ItemsSource = Nothing
        _modBoxes.Clear()
        Try
            Await ModMain.EnsureSharedModsAsync()
            Dim directory = ModMain.SharedModsDir()
            _mods = Await Task.Run(Function() ModStarsector.ScanMods(directory).Where(Function(m) Not String.IsNullOrWhiteSpace(m.Id)).GroupBy(Function(m) m.Id).Select(Function(group) group.First()).ToList())
            If Not _draftDirty OrElse _draftProfile <> ModMain.ActiveProfileId Then
                _enabled = New HashSet(Of String)(If(ModMain.ActiveProfileId = "", ModStarsector.ReadEnabled(ModMain.StarsectorPath), ModMain.ProfileStore().Read(ModMain.ActiveProfileId).EnabledMods))
                _draftProfile = ModMain.ActiveProfileId : _draftDirty = False
            End If
            BindModRows()
            Dim ok = _mods.Count
            LabModSummary.Text = "共享目录共 " & ok & " 个 MOD。勾选仅暂存，保存时选择添加新版本或替换已有版本。" & If(_draftDirty, " 当前有未保存修改。", "")
        Catch ex As Exception
            LabModSummary.Text = "扫描失败：" & ex.Message
        Finally
            _loading = False
        End Try
    End Sub

    Private Sub ModCheck_Changed(sender As Object, user As Boolean)
        If _loading OrElse _modOperation OrElse Not user Then Return
        Dim box = TryCast(sender, MyCheckBox)
        If box Is Nothing Then Return
        Dim id As String = If(box.Tag Is Nothing, "", box.Tag.ToString())
        If id = "" Then Return
        Dim desired As New HashSet(Of String)(_enabled)
        If box.Checked Then desired.Add(id) Else desired.Remove(id)
        _enabled = ModProfileStore.ResolveSelection(desired, _mods) : _draftDirty = True
        _loading = True
        For Each cb In _modBoxes
            If cb.Checked <> _enabled.Contains(CStr(cb.Tag)) Then cb.Checked = _enabled.Contains(CStr(cb.Tag))
        Next
        _loading = False
        LabModSummary.Text = "已暂存 " & _enabled.Count & " 项选择，尚未保存。前置依赖自动勾选。"
    End Sub

    Private Async Sub OnlineUpdate_Click(sender As Object, e As RoutedEventArgs)
        Dim current = TryCast(DirectCast(sender, Button).Tag, ModSpec)
        If current IsNot Nothing Then Await UpdateModAsync(current, Nothing)
    End Sub

    Private Sub SaveMods(sender As Object, e As RoutedEventArgs)
        If _modOperation OrElse _loading OrElse _enabled Is Nothing Then Return
        Try
            Dim store = ModMain.ProfileStore(), profiles = store.List()
            Dim window As New Window With {.Title = "保存 MOD 启用方案", .Width = 480, .SizeToContent = SizeToContent.Height, .WindowStartupLocation = WindowStartupLocation.CenterOwner, .Owner = ModMain.FrmMain, .ResizeMode = ResizeMode.NoResize}
            Dim panel As New StackPanel With {.Margin = New Thickness(22)}
            panel.Children.Add(New TextBlock With {.Text = "版本只保存启用清单，共用游戏与 MOD 文件。", .TextWrapping = TextWrapping.Wrap})
            Dim name As New TextBox With {.Text = If(_draftProfile = "", "我的 MOD 方案", store.Read(_draftProfile).Name), .Margin = New Thickness(0, 12, 0, 12), .Padding = New Thickness(8)}
            panel.Children.Add(New TextBlock With {.Text = "方案名字", .Margin = New Thickness(0, 12, 0, 0)}) : panel.Children.Add(name)
            Dim target As New ComboBox With {.ItemsSource = profiles, .DisplayMemberPath = "Name", .SelectedItem = profiles.FirstOrDefault(Function(p) p.Id = _draftProfile), .Margin = New Thickness(0, 8, 0, 12)}
            panel.Children.Add(New TextBlock With {.Text = "替换目标（添加新版本时忽略）"}) : panel.Children.Add(target)
            Dim actions As New WrapPanel, replacement As String = Nothing
            Dim add As New Button With {.Content = "添加新版本", .Padding = New Thickness(10, 6, 10, 6)}
            Dim replace As New Button With {.Content = "替换现有版本", .Padding = New Thickness(10, 6, 10, 6), .Margin = New Thickness(8, 0, 0, 0), .IsEnabled = profiles.Count > 0}
            Dim cancel As New Button With {.Content = "取消", .Padding = New Thickness(10, 6, 10, 6), .Margin = New Thickness(8, 0, 0, 0)}
            AddHandler add.Click, Sub() window.DialogResult = True
            AddHandler replace.Click, Sub()
                Dim chosen = TryCast(target.SelectedItem, ModProfile)
                If chosen Is Nothing Then Return
                replacement = chosen.Id : window.DialogResult = True
            End Sub
            AddHandler cancel.Click, Sub() window.DialogResult = False
            actions.Children.Add(add) : actions.Children.Add(replace) : actions.Children.Add(cancel)
            panel.Children.Add(actions) : window.Content = panel
            If window.ShowDialog() <> True Then Return
            Dim profile = store.Save(name.Text, ModProfileStore.ResolveSelection(_enabled, _mods), replacement)
            ModMain.ActiveProfileId = profile.Id : _draftProfile = profile.Id : _draftDirty = False
            LabModSummary.Text = "已保存「" & profile.Name & "」，下次启动使用此清单。MOD 文件未复制。"
            If ModMain.FrmStarsectorSelectRight IsNot Nothing Then ModMain.FrmStarsectorSelectRight.RefreshCards()
            If ModMain.FrmStarsectorLeft IsNot Nothing Then CType(ModMain.FrmStarsectorLeft, PageStarsectorLeft).RefreshInfo()
        Catch ex As Exception
            LabModSummary.Text = "保存失败：" & ex.Message
        End Try
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
        Dim sharedPath = ModMain.SharedModsDir()
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
                Dim result = Await Task.Run(Function() ModUpdateService.UpdateArchive(path, current.Id, sharedPath, gamePath, False))
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

    Private Sub RescanMods(sender As Object, e As RoutedEventArgs)
        LoadMods()
    End Sub

    Private Sub BtnExportClip_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnExportClip.Click
        Try
            Dim ids = ModMain.SelectedModIds()
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
