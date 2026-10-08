Imports System.Windows.Media

Public Class PageStarsectorDownload

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        ' 下载功能暂时锁定，正在沟通
        LabStatus.Text = "下载功能暂时锁定，正在沟通……"
    End Sub

    Private Sub DownloadGame_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnDownloadGame.Click
        MsgBox("下载功能暂时锁定，正在沟通中。", MsgBoxStyle.Information, "提示")
    End Sub

End Class
