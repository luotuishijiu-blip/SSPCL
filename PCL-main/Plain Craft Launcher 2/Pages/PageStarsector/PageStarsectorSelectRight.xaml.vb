Imports System.Windows.Media

Public Class PageStarsectorSelectRight

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        ModMain.SelectedFolderPath = ModMain.StarsectorPath
        RefreshCards()
    End Sub

    Public Sub AddFolder()
        Dim path = InputBox("输入远行星号安装目录：", "添加文件夹", ModMain.StarsectorPath)
        If String.IsNullOrWhiteSpace(path) Then Return
        If Not ModMain.StarsectorPaths.Contains(path) Then
            ModMain.StarsectorPaths.Add(path)
            ModMain.SaveStarsectorPaths()
        End If
        ModMain.SelectedFolderPath = path
        ModMain.FrmStarsectorSelectLeft.RefreshList()
        RefreshCards()
    End Sub

    Public Sub RefreshCards()
        If ModMain.StarsectorPaths.Count = 0 Then ModMain.LoadStarsectorPaths()
        Dim path = If(String.IsNullOrWhiteSpace(ModMain.SelectedFolderPath), ModMain.StarsectorPath, ModMain.SelectedFolderPath)
        PanMain.Children.Clear()
        Dim install = ModStarsector.Detect(path)
        Dim isCurrent = String.Equals(path, ModMain.StarsectorPath, StringComparison.OrdinalIgnoreCase)
        Dim title = If(install.IsValid, install.Version & " · 汉化 " & install.LocalizationPackageVersion, "未检测到安装")
        Dim card As New MyCard With {.Title = title, .Margin = New Thickness(0, 0, 0, 10)}
        Dim panel As New StackPanel With {.Margin = New Thickness(20, 38, 20, 18)}
        Dim info As String
        If install.IsValid Then
            info = "游戏版本 " & install.Version & vbCrLf &
                "汉化包 " & install.LocalizationPackageVersion & vbCrLf &
                "Mod " & install.ModCount & " 个 · 存档 " & install.SaveCount & " 个" & vbCrLf &
                path
        Else
            info = "未检测到有效安装" & vbCrLf & path
        End If
        panel.Children.Add(New TextBlock With {.Text = info, .TextWrapping = TextWrapping.Wrap, .Foreground = Brushes.Gray, .FontSize = 12})
        Dim row As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 12, 0, 0)}
        Dim useBtn As New MyButton With {.Text = If(isCurrent, "正在使用", "使用此版本"), .Padding = New Thickness(12, 5, 12, 5)}
        If Not isCurrent Then
            AddHandler useBtn.Click, Sub(s, e2) UseInstall(path)
        End If
        row.Children.Add(useBtn)
        If Not isCurrent Then
            Dim delBtn As New MyButton With {.Text = "移除该版本", .Padding = New Thickness(12, 5, 12, 5), .Margin = New Thickness(10, 0, 0, 0)}
            AddHandler delBtn.Click, Sub(s, e2) RemoveInstall(path)
            row.Children.Add(delBtn)
        End If
        panel.Children.Add(row)
        card.BorderChild = panel
        PanMain.Children.Add(card)
    End Sub

    Private Sub UseInstall(path As String)
        Logger.Info("UseInstall=" & path)
        ModMain.StarsectorPath = path
        ModMain.SelectedFolderPath = path
        If Not ModMain.StarsectorPaths.Contains(path) Then ModMain.StarsectorPaths.Add(path)
        ModMain.SaveStarsectorPaths()
        CType(ModMain.FrmStarsectorLeft, PageStarsectorLeft).RefreshInfo()
        CType(ModMain.FrmStarsectorRight, PageStarsectorRight).RefreshAll()
        ModMain.FrmMain.PageChange(FormMain.PageType.Launch)
    End Sub

    Private Sub RemoveInstall(path As String)
        If String.Equals(path, ModMain.StarsectorPath, StringComparison.OrdinalIgnoreCase) Then
            MsgBox("无法移除正在使用的版本。", MsgBoxStyle.Exclamation, "移除")
            Return
        End If
        ModMain.StarsectorPaths.Remove(path)
        ModMain.SaveStarsectorPaths()
        ModMain.SelectedFolderPath = ModMain.StarsectorPath
        ModMain.FrmStarsectorSelectLeft.RefreshList()
        RefreshCards()
    End Sub

End Class
