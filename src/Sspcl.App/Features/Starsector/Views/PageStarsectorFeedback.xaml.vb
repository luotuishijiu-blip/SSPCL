Imports System.Net.Http
Imports System.Text

Public Class PageStarsectorFeedback

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
    End Sub

    Private Async Sub Send_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSend.Click
        Dim url = UrlBox.Text.Trim()
        Dim text = BoxFeedback.Text.Trim()
        If url = "" Then
            MsgBox("请先填写接收地址（开发者提供的反馈接口）。", MsgBoxStyle.Exclamation, "反馈")
            Return
        End If
        If text = "" Then
            MsgBox("请填写反馈内容。", MsgBoxStyle.Exclamation, "反馈")
            Return
        End If
        BtnSend.IsEnabled = False
        Try
            Dim json = "{""content"":""【sspcl 反馈】" & text.Replace("\", "\\").Replace("""", "\""").Replace(vbLf, "\n").Replace(vbCr, "") & """}"
            Using http As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(30)}
                Using req = New StringContent(json, Encoding.UTF8, "application/json")
                    Await http.PostAsync(url, req)
                End Using
            End Using
            BoxFeedback.Text = ""
            MsgBox("已发送", MsgBoxStyle.Information, "反馈")
        Catch ex As Exception
            MsgBox("发送失败：" & ex.Message, MsgBoxStyle.Exclamation, "反馈")
        Finally
            BtnSend.IsEnabled = True
        End Try
    End Sub

End Class
