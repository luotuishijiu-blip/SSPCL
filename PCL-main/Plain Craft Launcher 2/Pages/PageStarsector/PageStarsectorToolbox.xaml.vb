Public Class PageStarsectorToolbox

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        RefreshTools()
    End Sub

    Private Sub RefreshTools()
        If ModMain.StarsectorTools.Count = 0 Then ModMain.LoadStarsectorTools()
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
            PanToolList.Children.Add(row)
        Next
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
        Dim name = InputBox("工具名称：", "添加工具")
        If String.IsNullOrWhiteSpace(name) Then Return
        Dim path = InputBox("工具路径（.exe / .bat / .cmd 等）：", "添加工具")
        If String.IsNullOrWhiteSpace(path) Then Return
        ModMain.StarsectorTools.Add(name & "|" & path)
        ModMain.SaveStarsectorTools()
        RefreshTools()
    End Sub

End Class
