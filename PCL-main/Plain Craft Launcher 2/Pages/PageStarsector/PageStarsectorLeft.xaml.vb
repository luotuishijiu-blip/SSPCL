Imports System.Windows.Media.Imaging

Public Class PageStarsectorLeft

    Private Sub Init() Handles Me.Loaded
        RefreshInfo()
    End Sub

    Private Sub Mode_Checked(sender As MyRadioButton, raiseByMouse As Boolean) Handles RbDirect.Check, RbLauncher.Check
        ModMain.SkipLauncher = (sender.Tag = "direct")
    End Sub

    Public Sub RefreshInfo()
        Try
            Dim install = ModStarsector.Detect(ModMain.StarsectorPath)
            If install.IsValid Then
                LabName.Text = If(String.IsNullOrWhiteSpace(install.DisplayName), "远行星号", install.DisplayName)
                LabVersionSub.Text = install.Version & " · 汉化"
            Else
                LabName.Text = "远行星号"
                LabVersionSub.Text = "未检测到安装"
            End If
        Catch ex As Exception
            LabVersionSub.Text = "检测失败"
        End Try
        LoadAvatar()
    End Sub

    Private Sub LoadAvatar()
        Try
            Dim shipPath = Settings.Get(Of String)("StarsectorAvatarShip")
            If String.IsNullOrWhiteSpace(shipPath) Then
                shipPath = IO.Path.Combine(ModMain.StarsectorPath, "starsector-core\graphics\ships\onslaught\onslaught_base.png")
            End If
            If IO.File.Exists(shipPath) Then
                Dim bmp As New BitmapImage()
                bmp.BeginInit()
                bmp.UriSource = New Uri(shipPath, UriKind.Absolute)
                bmp.CacheOption = BitmapCacheOption.OnLoad
                bmp.EndInit()
                ImgAvatar.Source = bmp
            Else
                ImgAvatar.Source = Nothing
            End If
        Catch ex As Exception
            ImgAvatar.Source = Nothing
        End Try
    End Sub

    Private Sub Avatar_Click(sender As Object, e As MouseButtonEventArgs)
        Dim shipsDir = IO.Path.Combine(ModMain.StarsectorPath, "starsector-core\graphics\ships")
        Dim dlg As New Microsoft.Win32.OpenFileDialog With {
            .Title = "选择舰船头像",
            .Filter = "PNG 图片|*.png",
            .InitialDirectory = If(IO.Directory.Exists(shipsDir), shipsDir, ModMain.StarsectorPath)
        }
        If dlg.ShowDialog() = True Then
            Settings.Set("StarsectorAvatarShip", dlg.FileName)
            LoadAvatar()
        End If
    End Sub

    Private Sub BtnLaunch_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLaunch.Click
        Try
            Dim install = ModStarsector.Detect(ModMain.StarsectorPath)
            If Not install.IsValid Then
                MsgBox("未检测到有效的远行星号安装。", MsgBoxStyle.Exclamation, "无法启动")
                Return
            End If
            Dim mem = ModStarsector.ReadMemoryOf(ModMain.StarsectorPath)
            Dim w = Settings.Get(Of Integer)("StarsectorWindowWidth")
            Dim h = Settings.Get(Of Integer)("StarsectorWindowHeight")
            Dim resolution = If(w > 0 AndAlso h > 0, w & "x" & h, "1920x1080")
            Dim priority = If(Settings.Get(Of Integer)("StarsectorProcessPriority") > 0, ProcessPriorityClass.High, ProcessPriorityClass.Normal)
            Dim vmArgs = Settings.Get(Of String)("StarsectorVmArgs")
            ModStarsector.Launch(ModMain.StarsectorPath, mem.XmxMb, mem.XmsMb, ModMain.SkipLauncher, resolution, priority, vmArgs)
            If Settings.Get(Of Boolean)("StarsectorCloseAfterLaunch") Then
                ModMain.FrmMain.Close()
            End If
        Catch ex As Exception
            MsgBox("启动失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

    Private Sub BtnVersionSelect_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnVersionSelect.Click
        ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSelect)
    End Sub

    Private Sub BtnVersionSetup_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnVersionSetup.Click
        ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSetup)
    End Sub

End Class
