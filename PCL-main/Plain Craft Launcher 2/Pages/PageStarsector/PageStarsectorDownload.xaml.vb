Imports System.Windows.Media

Public Class PageStarsectorDownload

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        LoadStore()
    End Sub

    Private Async Sub LoadStore()
        LabStatus.Text = "正在拉取中文论坛 Mod 商店……"
        Try
            Dim items = Await ModStarsector.FetchStore()
            Dim cn = items.Where(Function(i) (i.ForumUrl IsNot Nothing AndAlso i.ForumUrl.Contains("fossic.org")) OrElse i.HomeUrl.Contains("fossic.org")).ToList()
            PanStoreList.Children.Clear()
            If cn.Count = 0 Then
                LabStatus.Text = "未找到中文论坛 Mod"
                Return
            End If
            LabStatus.Text = "共 " & cn.Count & " 个中文论坛 Mod"
            For Each it In cn
                Dim card As New MyCard With {.Title = it.Name, .Margin = New Thickness(0, 0, 0, 10)}
                Dim panel As New StackPanel With {.Margin = New Thickness(20, 38, 20, 18)}
                panel.Children.Add(New TextBlock With {.Text = it.Meta, .TextWrapping = TextWrapping.Wrap, .Foreground = Brushes.Gray})
                Dim row As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 10, 0, 0)}
                Dim open As New MyButton With {.Text = "打开主页", .Padding = New Thickness(12, 5, 12, 5)}
                AddHandler open.Click, Sub(s, e2) TryOpen(it.HomeUrl)
                row.Children.Add(open)
                If it.HasDirectDownload Then
                    Dim dl As New MyButton With {.Text = "下载安装", .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(10, 0, 0, 0)}
                    AddHandler dl.Click, Sub(s, e2) Install(it.DirectDownloadUrl)
                    row.Children.Add(dl)
                End If
                panel.Children.Add(row)
                card.BorderChild = panel
                PanStoreList.Children.Add(card)
            Next
        Catch ex As Exception
            LabStatus.Text = "拉取失败：" & ex.Message
        End Try
    End Sub

    Private Sub TryOpen(url As String)
        Try
            Process.Start(New ProcessStartInfo(url) With {.UseShellExecute = True})
        Catch ex As Exception
            MsgBox("打开失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

    Private Async Sub Install(url As String)
        LabStatus.Text = "正在下载到 mod 池……"
        Dim err = Await ModStarsector.InstallFromUrl(url, ModMain.ModPoolDir())
        LabStatus.Text = If(err = "", "已导入 mod 池（到 Mod 管理中启用）", "安装失败：" & err)
    End Sub

    Private Sub DownloadGame_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnDownloadGame.Click
        Try
            Process.Start(New ProcessStartInfo("https://www.fossic.org/forum.php?mod=attachment&aid=ODU2MTR8NGIwODUyOTJ8MTc5MTI2MDQ1M3wwfDE5NDMw") With {.UseShellExecute = True})
        Catch ex As Exception
            MsgBox("打开下载页失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

End Class
