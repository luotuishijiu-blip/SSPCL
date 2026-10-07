Public Class PageStarsectorRight

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        RefreshShortcuts()
    End Sub

    Public Sub RefreshAll()
        RefreshShortcuts()
    End Sub

    Private Sub RefreshShortcuts()
        If ModMain.StarsectorHomeShortcuts.Count = 0 Then ModMain.LoadStarsectorHomeShortcuts()
        PanShortcuts.Children.Clear()
        For Each entry In ModMain.StarsectorHomeShortcuts
            Dim parts = entry.Split("|"c)
            Dim name = If(parts.Length > 0, parts(0), "?")
            Dim path = If(parts.Length > 1, parts(1), "")
            PanShortcuts.Children.Add(BuildShortcutTile(name, path, entry))
        Next
    End Sub

    Private Function BuildShortcutTile(name As String, path As String, entry As String) As FrameworkElement
        Dim card As New MyCard With {.Title = name, .Width = 190, .Margin = New Thickness(0, 0, 12, 12)}
        Dim panel As New StackPanel With {.Margin = New Thickness(16, 34, 16, 14)}
        Dim row As New StackPanel With {.Orientation = Orientation.Horizontal}
        Dim run As New MyButton With {.Text = "运行", .Padding = New Thickness(12, 5, 12, 5)}
        AddHandler run.Click, Sub(s, e2) RunTool(path)
        row.Children.Add(run)
        Dim del As New MyButton With {.Text = "移除", .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(10, 0, 0, 0), .ColorType = MyButton.ColorState.Red}
        AddHandler del.Click, Sub(s, e2)
                                   ModMain.StarsectorHomeShortcuts.Remove(entry)
                                   ModMain.SaveStarsectorHomeShortcuts()
                                   RefreshShortcuts()
                               End Sub
        row.Children.Add(del)
        panel.Children.Add(row)
        card.BorderChild = panel
        Return card
    End Function

    Private Sub RunTool(path As String)
        Try
            Process.Start(New ProcessStartInfo(path) With {.UseShellExecute = True})
        Catch ex As Exception
            MsgBox("运行失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

    Private Sub BtnAddShortcut_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnAddShortcut.MouseLeftButtonUp
        ShowShortcutPicker()
    End Sub

    Private Sub ShowShortcutPicker()
        If ModMain.StarsectorTools.Count = 0 Then ModMain.LoadStarsectorTools()
        If ModMain.StarsectorTools.Count = 0 Then
            MsgBox("百宝箱中还没有插件，请先到「更多 → 百宝箱」添加。", MsgBoxStyle.Information, "添加快捷方式")
            Return
        End If
        Dim win As New Window With {
            .Title = "添加快捷方式（百宝箱插件）",
            .Width = 360,
            .Height = 440,
            .WindowStartupLocation = WindowStartupLocation.CenterScreen,
            .Background = Brushes.White,
            .FontFamily = New FontFamily("Microsoft YaHei UI"),
            .ResizeMode = ResizeMode.NoResize
        }
        Dim scroll As New ScrollViewer With {.VerticalScrollBarVisibility = ScrollBarVisibility.Auto}
        Dim list As New StackPanel With {.Margin = New Thickness(14)}
        For Each t In ModMain.StarsectorTools
            Dim parts = t.Split("|"c)
            Dim name = If(parts.Length > 0, parts(0), "?")
            Dim btn As New Button With {
                .Content = name,
                .Padding = New Thickness(12, 7, 12, 7),
                .Margin = New Thickness(0, 0, 0, 8),
                .HorizontalContentAlignment = HorizontalAlignment.Left,
                .Cursor = Cursors.Hand
            }
            AddHandler btn.Click, Sub(s, e2)
                                      If Not ModMain.StarsectorHomeShortcuts.Contains(t) Then
                                          ModMain.StarsectorHomeShortcuts.Add(t)
                                          ModMain.SaveStarsectorHomeShortcuts()
                                      End If
                                      win.Close()
                                      RefreshShortcuts()
                                  End Sub
            list.Children.Add(btn)
        Next
        scroll.Content = list
        win.Content = scroll
        win.ShowDialog()
    End Sub

End Class
