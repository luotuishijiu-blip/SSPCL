Imports Sspcl.Core.Mods

Public Class PageStarsectorInstanceRight

    Private _mods As List(Of ModSpec)
    Private _enabled As HashSet(Of String)
    Private _loading As Boolean = False
    Private Shared _lastTab As Integer = 2

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        ShowTab(_lastTab)
    End Sub

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

    Private Sub LoadOverview()
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
        PanModList.Children.Clear()
        Try
            _mods = ModStarsector.ScanMods(ModMain.ModPoolDir()).ToList()
            _enabled = New HashSet(Of String)(ModStarsector.ReadEnabled(ModMain.StarsectorPath))
            Dim ok = 0
            For Each m In _mods
                If String.IsNullOrWhiteSpace(m.Id) Then Continue For
                ok += 1
                Dim box As New MyCheckBox With {
                    .Text = m.Name & "  [" & m.Id & "]  " & m.Version.ToString(),
                    .Checked = ModMain.IsModEnabled(m.Folder),
                    .Tag = m.Id,
                    .Margin = New Thickness(0, 0, 0, 6)
                }
                AddHandler box.Change, AddressOf ModCheck_Changed
                PanModList.Children.Add(box)
            Next
            If ok = 0 Then
                LabModSummary.Text = "mod 池为空——请到「下载」页下载 mod，或在「设置 → 其他」点「打开 mod 池」手动放入。"
            Else
                LabModSummary.Text = "mod 池共 " & ok & " 个 mod（勾选即启用：复制到游戏 mods 目录，前置依赖自动勾选）"
            End If
        Catch ex As Exception
            LabModSummary.Text = "扫描失败：" & ex.Message
        End Try
    End Sub

    Private Sub ModCheck_Changed(sender As Object, user As Boolean)
        If _loading Then Return
        Dim box = TryCast(sender, MyCheckBox)
        If box Is Nothing Then Return
        Dim id As String = If(box.Tag Is Nothing, "", box.Tag.ToString())
        If id = "" Then Return
        If box.Checked Then _enabled.Add(id) Else _enabled.Remove(id)
        Dim resolved As HashSet(Of String)
        Try
            resolved = New HashSet(Of String)(ModStarsector.Resolve(_enabled, _mods))
        Catch
            resolved = New HashSet(Of String)(_enabled)
        End Try
        _loading = True
        _enabled = resolved
        ' 应用启用/停用：把勾选的 mod 复制进游戏 mods 目录，取消勾选的移除
        For Each m In _mods
            If String.IsNullOrWhiteSpace(m.Id) OrElse String.IsNullOrWhiteSpace(m.Folder) Then Continue For
            If _enabled.Contains(m.Id) AndAlso Not ModMain.IsModEnabled(m.Folder) Then
                ModMain.EnableMod(m.Folder)
            ElseIf Not _enabled.Contains(m.Id) AndAlso ModMain.IsModEnabled(m.Folder) Then
                ModMain.DisableMod(m.Folder)
            End If
        Next
        For Each child As UIElement In PanModList.Children
            Dim cb = TryCast(child, MyCheckBox)
            If cb IsNot Nothing AndAlso cb.Tag IsNot Nothing Then cb.Checked = _enabled.Contains(cb.Tag.ToString())
        Next
        _loading = False
        Try
            ModStarsector.WriteEnabled(ModMain.StarsectorPath, _enabled)
        Catch ex As Exception
            MsgBox("写回 enabled_mods.json 失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

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
