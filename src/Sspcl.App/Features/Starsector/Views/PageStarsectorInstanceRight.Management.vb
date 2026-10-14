Imports System.ComponentModel
Imports System.Windows.Data
Imports System.Windows.Threading
Imports Sspcl.Core.Mods
Imports Sspcl.Core.Launch

Public Partial Class PageStarsectorInstanceRight
    Private _modView As ICollectionView
    Private ReadOnly _automaticAdded As New HashSet(Of String)
    Private _availableUpdates As New Dictionary(Of String, String)
    Private _searchTimer As DispatcherTimer
    Private _checkingUpdates As Boolean

    Private Sub FitModList(sender As Object, e As SizeChangedEventArgs) Handles Me.SizeChanged
        If PanModList Is Nothing OrElse ActualHeight <= 0 OrElse PanMod.Visibility <> Visibility.Visible Then Return
        Dim top = PanModList.TranslatePoint(New Point(0, 0), Me).Y + PanBack.VerticalOffset
        PanModList.Height = Math.Max(180, Math.Min(600, ActualHeight - top - 30))
    End Sub

    Private Sub ConfigureModFilters()
        Dim author = TryCast(FilterModAuthor.SelectedItem, String)
        FilterModAuthor.ItemsSource = New String() {"全部作者"}.Concat(_mods.SelectMany(Function(m) m.Authors).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Function(a) a)).ToList()
        FilterModAuthor.SelectedItem = If(author IsNot Nothing AndAlso DirectCast(FilterModAuthor.ItemsSource, IEnumerable(Of String)).Contains(author), author, "全部作者")
        _modView = CollectionViewSource.GetDefaultView(_modRows)
        _modView.Filter = Function(item)
            Dim row = DirectCast(item, ModRow)
            Dim status = TryCast(FilterModStatus.SelectedItem, ComboBoxItem)
            Dim chosenAuthor = TryCast(FilterModAuthor.SelectedItem, String)
            Return ModManagement.Matches(row.Mod, row.Id, BoxModSearch.Text.Trim(), row.IsChecked, If(status Is Nothing, "all", CStr(status.Tag)), If(chosenAuthor Is Nothing OrElse chosenAuthor = "全部作者", "", chosenAuthor), _availableUpdates.ContainsKey(row.Id))
        End Function
        PanModList.ItemsSource = _modView
        RefreshModFilter()
        Dispatcher.BeginInvoke(New Action(Sub() FitModList(Nothing, Nothing)), DispatcherPriority.Loaded)
    End Sub

    Private Sub RefreshModFilter(Optional selectionOnly As Boolean = False)
        If _modView Is Nothing OrElse LabModResults Is Nothing Then Return
        Dim status = TryCast(FilterModStatus.SelectedItem, ComboBoxItem)
        If selectionOnly AndAlso status IsNot Nothing AndAlso CStr(status.Tag) <> "enabled" AndAlso CStr(status.Tag) <> "disabled" Then Return
        _modView.Refresh()
        LabModResults.Text = "显示 " & _modView.Cast(Of ModRow)().Count() & " / " & _modRows.Count & " 项 · 全选与全部取消作用于完整清单。"
    End Sub

    Private Sub SearchMods_Changed(sender As Object, e As TextChangedEventArgs)
        If _searchTimer Is Nothing Then
            _searchTimer = New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(180)}
            AddHandler _searchTimer.Tick, Sub()
                _searchTimer.Stop()
                RefreshModFilter()
            End Sub
        End If
        _searchTimer.Stop() : _searchTimer.Start()
    End Sub

    Private Sub FilterMods_Changed(sender As Object, e As SelectionChangedEventArgs)
        RefreshModFilter()
    End Sub

    Private Async Sub CheckUpdates_Click(sender As Object, e As RoutedEventArgs)
        If _checkingUpdates OrElse _loading OrElse _modOperation OrElse _mods Is Nothing Then Return
        _checkingUpdates = True : BtnCheckUpdates.IsEnabled = False
        Dim mods = _mods, path = ModMain.SharedModsDir()
        LabModSummary.Text = "正在检查论坛目录（优先使用本地缓存）…"
        Try
            Dim catalog = Await PageStarsectorDownload.GetForumCatalogAsync()
            Dim updates = Await Task.Run(Function() ModManagement.FindUpdates(mods, catalog.Catalog.Mods.Data))
            If path <> ModMain.SharedModsDir() OrElse mods IsNot _mods Then Return
            _availableUpdates = updates
            _loading = True
            Try
                BindModRows()
            Finally
                _loading = False
            End Try
            LabModSummary.Text = "发现 " & updates.Count & " 个版本号更高的候选更新。选择可更新筛选，再点击论坛更新确认。" & If(_draftDirty, " 勾选修改仍未保存。", "") & If(String.IsNullOrWhiteSpace(catalog.Warning), "", vbCrLf & catalog.Warning)
        Catch ex As Exception
            LabModSummary.Text = "检查更新失败：" & ex.Message
        Finally
            _checkingUpdates = False : BtnCheckUpdates.IsEnabled = True
        End Try
    End Sub

    Private Function DescribeDiff(previous As IEnumerable(Of String)) As String
        Dim diff = ModManagement.Compare(previous, _enabled, _automaticAdded)
        Dim names As Func(Of IEnumerable(Of String), String) = Function(ids) String.Join("、", ids.Select(Function(id)
            Dim modInfo = _mods.FirstOrDefault(Function(m) m.Id = id)
            Return If(modInfo Is Nothing, id, modInfo.Name & " [" & id & "]")
        End Function))
        Return "新增 " & diff.Added.Count & "：" & If(diff.Added.Count = 0, "无", names(diff.Added)) & vbCrLf &
            "移除 " & diff.Removed.Count & "：" & If(diff.Removed.Count = 0, "无", names(diff.Removed)) & vbCrLf &
            "本次自动补齐前置 " & diff.Automatic.Count & "：" & If(diff.Automatic.Count = 0, "无", names(diff.Automatic))
    End Function

    Private Function ManagementWindow(title As String) As Window
        Return New Window With {.Title = title, .Width = Math.Min(760, SystemParameters.WorkArea.Width - 40), .Height = Math.Min(560, SystemParameters.WorkArea.Height - 40), .MinWidth = 500, .MinHeight = 360, .MaxHeight = SystemParameters.WorkArea.Height, .WindowStartupLocation = WindowStartupLocation.CenterOwner, .Owner = ModMain.FrmMain, .Background = Brushes.WhiteSmoke}
    End Function

    Private Sub Diagnostics_Click(sender As Object, e As RoutedEventArgs)
        If _loading OrElse _modOperation OrElse _enabled Is Nothing Then Return
        BuildDiagnosticsWindow(ModMain.StarsectorPath, _enabled.ToList()).ShowDialog()
    End Sub

    Private Function BuildDiagnosticsWindow(game As String, ids As List(Of String)) As Window
        Dim window = ManagementWindow("启动诊断 · 当前暂存清单")
        Dim panel As New DockPanel With {.Margin = New Thickness(18)}
        Dim actions As New StackPanel With {.Orientation = Orientation.Horizontal}
        Dim refresh As New Button With {.Content = "重新诊断", .Padding = New Thickness(12, 7, 12, 7)}
        Dim export As New Button With {.Content = "导出报告", .Padding = New Thickness(12, 7, 12, 7), .Margin = New Thickness(8, 0, 0, 0), .IsEnabled = False}
        actions.Children.Add(refresh) : actions.Children.Add(export) : DockPanel.SetDock(actions, Dock.Bottom) : panel.Children.Add(actions)
        Dim hint As New TextBlock With {.Text = "检查当前暂存的启用清单。实际启动使用已保存方案；诊断不会修改游戏文件。", .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 10)}
        DockPanel.SetDock(hint, Dock.Top) : panel.Children.Add(hint)
        Dim output As New TextBox With {.IsReadOnly = True, .Text = "正在检查…", .TextWrapping = TextWrapping.Wrap, .VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .Padding = New Thickness(12), .Margin = New Thickness(0, 0, 0, 12)}
        panel.Children.Add(output) : window.Content = panel
        Dim report As LaunchDiagnosticReport = Nothing
        Dim diagnose As Func(Of Task) = Async Function()
            refresh.IsEnabled = False : export.IsEnabled = False : output.Text = "正在检查…"
            Try
                report = Await Task.Run(Function() LaunchDiagnostics.Check(game, ids))
                output.Text = If(report.CanLaunch, "检查通过。", "检测到阻止启动的问题。") & vbCrLf & vbCrLf & report.ToText()
                export.IsEnabled = True
            Catch ex As Exception
                output.Text = "诊断失败：" & ex.Message
            Finally
                refresh.IsEnabled = True
            End Try
        End Function
        AddHandler refresh.Click, Async Sub() Await diagnose()
        AddHandler export.Click, Sub()
            Dim dialog As New Microsoft.Win32.SaveFileDialog With {.FileName = "SSPCL-诊断.txt", .Filter = "文本报告|*.txt"}
            If dialog.ShowDialog(window) <> True Then Return
            Try
                IO.File.WriteAllText(dialog.FileName, report.ToText(), New Text.UTF8Encoding(True))
            Catch ex As Exception
                MessageBox.Show(window, "导出失败：" & ex.Message)
            End Try
        End Sub
        AddHandler window.Loaded, Async Sub() Await diagnose()
        Return window
    End Function

    Private Sub Backups_Click(sender As Object, e As RoutedEventArgs)
        If _loading OrElse _modOperation Then Return
        Dim game = ModMain.StarsectorPath
        Dim catalog As New BackupCatalog(ModMain.ProfileStore().Root, ModMain.SharedModsDir())
        BuildBackupsWindow(game, catalog).ShowDialog()
        LoadMods()
    End Sub

    Private Function BuildBackupsWindow(game As String, catalog As BackupCatalog) As Window
        Dim window = ManagementWindow("备份管理")
        Dim panel As New DockPanel With {.Margin = New Thickness(18)}
        Dim hint As New TextBlock With {.Text = "方案保留上一次替换前的清单；MOD 更新保留旧文件。恢复会替换当前内容，清理只删除所选备份。", .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 10)}
        DockPanel.SetDock(hint, Dock.Top) : panel.Children.Add(hint)
        Dim actions As New WrapPanel
        Dim refresh As New Button With {.Content = "刷新", .Padding = New Thickness(12, 7, 12, 7)}
        Dim restore As New Button With {.Content = "恢复所选", .Padding = New Thickness(12, 7, 12, 7), .Margin = New Thickness(8, 0, 0, 0)}
        Dim remove As New Button With {.Content = "清理所选备份", .Padding = New Thickness(12, 7, 12, 7), .Margin = New Thickness(8, 0, 0, 0)}
        actions.Children.Add(refresh) : actions.Children.Add(restore) : actions.Children.Add(remove)
        DockPanel.SetDock(actions, Dock.Bottom) : panel.Children.Add(actions)
        Dim outcome As New TextBlock With {.TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 8, 0, 10)}
        DockPanel.SetDock(outcome, Dock.Bottom) : panel.Children.Add(outcome)
        Dim list As New ListBox With {.Margin = New Thickness(0, 0, 0, 12), .HorizontalContentAlignment = HorizontalAlignment.Stretch}
        Dim label As New FrameworkElementFactory(GetType(TextBlock))
        label.SetBinding(TextBlock.TextProperty, New Binding("Display"))
        label.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap)
        label.SetValue(TextBlock.MarginProperty, New Thickness(6))
        list.ItemTemplate = New DataTemplate With {.VisualTree = label}
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled)
        panel.Children.Add(list) : window.Content = panel
        Dim busy As Boolean = False
        Dim scan As Func(Of Task) = Async Function()
            If busy Then Return
            busy = True : actions.IsEnabled = False
            Try
                Dim entries = Await Task.Run(Function() catalog.List())
                list.ItemsSource = entries
                hint.Text = "共 " & entries.Count & " 个有效备份 · " & (entries.Sum(Function(entry) entry.SizeBytes) / 1048576.0).ToString("F1") & " MB。恢复会替换当前内容；清理只删除所选备份。"
            Catch ex As Exception
                hint.Text = "读取失败：" & ex.Message
            Finally
                busy = False : actions.IsEnabled = True
            End Try
        End Function
        Dim operate As Func(Of Boolean, Task) = Async Function(restoring)
            Dim selected = TryCast(list.SelectedItem, BackupEntry)
            If busy OrElse selected Is Nothing Then Return
            If restoring AndAlso Not ConfirmDiscardDraft() Then Return
            Dim description = If(restoring, "恢复「" & selected.Name & "」会替换当前" & If(selected.Kind = "profile", "方案清单", "同 ID MOD 的全部文件") & "，替换前的内容将保留为备份。", "永久删除「" & selected.Name & "」的这份备份，不影响当前使用内容。")
            If MessageBox.Show(window, description & vbCrLf & "是否继续？", "确认操作", MessageBoxButton.YesNo, MessageBoxImage.Question) <> MessageBoxResult.Yes Then Return
            busy = True : actions.IsEnabled = False : list.IsEnabled = False
            _modOperation = True
            Dim entered As Boolean = False
            Try
                Await PageStarsectorDownload.WaitForInstallAsync(CancellationToken.None)
                entered = True
                If restoring AndAlso (Await Task.Run(Function() GameMemoryMonitor.Read(game))).ProcessCount > 0 Then Throw New InvalidOperationException("请先关闭游戏，再恢复备份。")
                Await Task.Run(Sub()
                    If restoring Then catalog.Restore(selected.Key) Else catalog.Delete(selected.Key)
                End Sub)
                If restoring Then
                    DiscardDraft()
                    If ModMain.FrmStarsectorSelectRight IsNot Nothing Then ModMain.FrmStarsectorSelectRight.RefreshCards()
                    If ModMain.FrmStarsectorLeft IsNot Nothing Then CType(ModMain.FrmStarsectorLeft, PageStarsectorLeft).RefreshInfo()
                End If
                outcome.Text = If(restoring, "已恢复。", "已清理所选备份。")
            Catch ex As Exception
                outcome.Text = "操作失败：" & ex.Message
            Finally
                If entered Then PageStarsectorDownload.ReleaseInstall()
                _modOperation = False : busy = False : actions.IsEnabled = True : list.IsEnabled = True
            End Try
            Await scan()
        End Function
        AddHandler refresh.Click, Async Sub() Await scan()
        AddHandler restore.Click, Async Sub() Await operate(True)
        AddHandler remove.Click, Async Sub() Await operate(False)
        AddHandler window.Closing, Sub(s, args)
            If busy Then args.Cancel = True
        End Sub
        AddHandler window.Loaded, Async Sub() Await scan()
        Return window
    End Function
End Class
