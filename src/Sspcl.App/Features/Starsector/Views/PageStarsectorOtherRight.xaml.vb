Imports System.Net.Http
Imports System.Text

Public Class PageStarsectorOtherRight

    Private Shared _lastTab As Integer = 0

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        ShowTab(_lastTab)
    End Sub

    Public Sub ShowTab(tag As Integer)
        _lastTab = tag
        PanHelp.Visibility = If(tag = 0, Visibility.Visible, Visibility.Collapsed)
        PanAbout.Visibility = If(tag = 1, Visibility.Visible, Visibility.Collapsed)
        PanToolbox.Visibility = If(tag = 2, Visibility.Visible, Visibility.Collapsed)
        PanFeedback.Visibility = If(tag = 3, Visibility.Visible, Visibility.Collapsed)
        PanVote.Visibility = If(tag = 4, Visibility.Visible, Visibility.Collapsed)
        If tag = 2 Then RefreshTools()
    End Sub

    ' ============ 百宝箱 ============
    Private Sub RefreshTools()
        If ModMain.StarsectorTools.Count = 0 Then ModMain.LoadStarsectorTools()
        LabToolsDir.Text = "插件目录：" & ModMain.StarsectorToolsDir()
        PanToolList.Children.Clear()
        If ModMain.StarsectorTools.Count = 0 Then Return
        For Each t In ModMain.StarsectorTools
            Dim parts = t.Split("|"c)
            Dim name = If(parts.Length > 0, parts(0), "?")
            Dim path = If(parts.Length > 1, parts(1), "")
            Dim row As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 0, 0, 6)}
            row.Children.Add(New TextBlock With {.Text = name, .VerticalAlignment = VerticalAlignment.Center, .Width = 220, .TextTrimming = TextTrimming.CharacterEllipsis})
            Dim run As New MyButton With {.Text = "运行", .Padding = New Thickness(12, 5, 12, 5)}
            AddHandler run.Click, Sub(s, e2) RunTool(path)
            row.Children.Add(run)
            Dim del As New MyButton With {.Text = "删除", .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(10, 0, 0, 0)}
            AddHandler del.Click, Sub(s, e2) DeleteTool(t)
            row.Children.Add(del)
            Dim re As New MyButton With {.Text = "重命名", .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(10, 0, 0, 0)}
            AddHandler re.Click, Sub(s, e2) RenameTool(t)
            row.Children.Add(re)
            PanToolList.Children.Add(row)
        Next
    End Sub

    Private Sub RenameTool(t As String)
        Dim parts = t.Split("|"c)
        Dim name = If(parts.Length > 0, parts(0), "")
        Dim path = If(parts.Length > 1, parts(1), "")
        Dim newName = InputBox("插件名称：", "重命名插件", name)
        If String.IsNullOrWhiteSpace(newName) Then Return
        Dim idx = ModMain.StarsectorTools.IndexOf(t)
        If idx >= 0 Then ModMain.StarsectorTools(idx) = newName & "|" & path
        ModMain.SaveStarsectorTools()
        RefreshTools()
    End Sub

    Private Sub RunTool(path As String)
        Try
            Process.Start(New ProcessStartInfo(path) With {.UseShellExecute = True})
        Catch ex As Exception
            MsgBox("运行失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

    Private Sub DeleteTool(t As String)
        ModMain.StarsectorTools.Remove(t)
        ModMain.SaveStarsectorTools()
        RefreshTools()
    End Sub

    Private Sub AddTool_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnAddTool.Click
        Dim dlg As New Microsoft.Win32.OpenFileDialog With {
            .Title = "导入插件（exe / bat / cmd / lnk）",
            .Filter = "程序|*.exe;*.bat;*.cmd;*.lnk|所有文件|*.*"
        }
        If dlg.ShowDialog() <> True Then Return
        Dim name = InputBox("插件名称：", "导入插件", IO.Path.GetFileNameWithoutExtension(dlg.FileName))
        If String.IsNullOrWhiteSpace(name) Then Return
        Dim entry = ModMain.ImportStarsectorTool(dlg.FileName, name)
        If entry = "" Then
            MsgBox("导入失败：无法移动文件到插件目录。", MsgBoxStyle.Exclamation, "错误")
            Return
        End If
        ModMain.StarsectorTools.Add(entry)
        ModMain.SaveStarsectorTools()
        RefreshTools()
    End Sub

    Private Sub BtnChangeDir_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnChangeDir.Click
        Dim dlg As New Ookii.Dialogs.Wpf.VistaFolderBrowserDialog()
        dlg.Description = "选择插件存放目录"
        dlg.SelectedPath = ModMain.StarsectorToolsDir()
        If dlg.ShowDialog() = True Then
            If ModMain.ChangeStarsectorToolsDir(dlg.SelectedPath) Then
                RefreshTools()
            Else
                MsgBox("改变路径失败：无法迁移插件文件。", MsgBoxStyle.Exclamation, "错误")
            End If
        End If
    End Sub

    Private Sub BtnAiLoadout_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnAiLoadout.Click
        Dim workbench As New LoadoutWorkbench(ModMain.StarsectorPath)
        workbench.Show()
    End Sub
End Class