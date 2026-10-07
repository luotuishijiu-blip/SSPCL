Public Class PageStarsectorSettings

    Private _loading As Boolean = False
    Private _ramTimer As System.Windows.Threading.DispatcherTimer

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        ShowTab(0)
        ' 内存实时刷新（照抄 Sspcl 原版：每 1 秒刷新一次内存占用显示）
        _ramTimer = New System.Windows.Threading.DispatcherTimer With {.Interval = TimeSpan.FromSeconds(1)}
        AddHandler _ramTimer.Tick, Sub()
                                     If PanLaunch.Visibility = Visibility.Visible Then UpdateMemoryDisplay()
                                 End Sub
        _ramTimer.Start()
    End Sub

    Private Sub StopRamTimer() Handles Me.Unloaded
        If _ramTimer IsNot Nothing Then
            _ramTimer.Stop()
            _ramTimer = Nothing
        End If
    End Sub

    Public Sub ShowTab(tag As Integer)
        PanLaunch.Visibility = If(tag = 0, Visibility.Visible, Visibility.Collapsed)
        PanUI.Visibility = If(tag = 1, Visibility.Visible, Visibility.Collapsed)
        PanSystem.Visibility = If(tag = 2, Visibility.Visible, Visibility.Collapsed)
        If tag = 0 Then LoadLaunchOptions()
    End Sub

    Private Sub LoadLaunchOptions()
        _loading = True
        ChkSkipLauncher.Checked = ModMain.SkipLauncher
        ChkCloseAfterLaunch.Checked = Settings.Get(Of Boolean)("StarsectorCloseAfterLaunch")
        ChkHighPriority.Checked = Settings.Get(Of Integer)("StarsectorProcessPriority") > 0
        BoxWinW.Text = Settings.Get(Of Integer)("StarsectorWindowWidth").ToString()
        BoxWinH.Text = Settings.Get(Of Integer)("StarsectorWindowHeight").ToString()
        BoxVmArgs.Text = Settings.Get(Of String)("StarsectorVmArgs")
        ChkRamOptimize.Checked = Settings.Get(Of Boolean)("StarsectorRamOptimize")
        ChkWarmStart.Checked = Settings.Get(Of Boolean)("StarsectorWarmStart")
        Dim ramType = Settings.Get(Of Integer)("StarsectorRamType")
        UpdateSliderMax()
        SliderRam.Value = Settings.Get(Of Integer)("StarsectorRamCustom")
        If ramType = 1 Then
            RbRamCustom.Checked = True
            SliderRam.IsEnabled = True
        Else
            RbRamAuto.Checked = True
            SliderRam.IsEnabled = False
        End If
        _loading = False
        UpdateMemoryDisplay()
    End Sub

    Private Sub ChkSkipLauncher_Change(sender As Object, user As Boolean) Handles ChkSkipLauncher.Change
        ModMain.SkipLauncher = ChkSkipLauncher.Checked
    End Sub

    Private Sub ChkCloseAfterLaunch_Change(sender As Object, user As Boolean) Handles ChkCloseAfterLaunch.Change
        Settings.Set("StarsectorCloseAfterLaunch", ChkCloseAfterLaunch.Checked)
    End Sub

    Private Sub ChkHighPriority_Change(sender As Object, user As Boolean) Handles ChkHighPriority.Change
        Settings.Set("StarsectorProcessPriority", If(ChkHighPriority.Checked, 1, 0))
    End Sub

    Private Sub ChkWarmStart_Change(sender As Object, user As Boolean) Handles ChkWarmStart.Change
        Settings.Set("StarsectorWarmStart", ChkWarmStart.Checked)
    End Sub

    Private Sub ChkRamOptimize_Change(sender As Object, user As Boolean) Handles ChkRamOptimize.Change
        Settings.Set("StarsectorRamOptimize", ChkRamOptimize.Checked)
    End Sub

    Private Sub RamMode_Checked(sender As MyRadioButton, raiseByMouse As Boolean) Handles RbRamAuto.Check, RbRamCustom.Check
        If _loading OrElse SliderRam Is Nothing Then Return
        SliderRam.IsEnabled = (sender.Tag = "custom")
        Settings.Set("StarsectorRamType", If(sender.Tag = "custom", 1, 0))
        ApplyMemory()
    End Sub

    Private Sub SliderRam_Change(sender As Object, user As Boolean) Handles SliderRam.Change
        If _loading Then Return
        Settings.Set("StarsectorRamCustom", SliderRam.Value)
        If RbRamCustom.Checked Then ApplyMemory()
    End Sub

    Private Function GetModCount() As Integer
        Try
            Return IO.Directory.GetDirectories(ModStarsector.ModsDir(ModMain.StarsectorPath)).Length
        Catch
            Return 0
        End Try
    End Function

    ''' <summary>获取当前内存分配值（GB），算法与 Sspcl 原版一致。</summary>
    Private Function GetRamGB() As Double
        If Settings.Get(Of Integer)("StarsectorRamType") = 1 Then
            Dim v = SliderRam.Value
            If v <= 12 Then Return v * 0.1 + 0.3
            If v <= 25 Then Return (v - 12) * 0.5 + 1.5
            If v <= 33 Then Return (v - 25) * 1 + 8
            Return (v - 33) * 2 + 16
        End If
        ' 自动配置（Sspcl 算法：按剩余内存分阶段分配，Mod 越多需求越高）
        Dim ramAvailable As Double = My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024 / 1024
        Dim modCount = GetModCount()
        Dim ramMinimum = 0.5 + modCount / 150
        Dim ramTarget1 = 1.5 + modCount / 90
        Dim ramTarget2 = 2.7 + modCount / 50
        Dim ramTarget3 = 4.5 + modCount / 25
        Dim ramGive As Double = 0
        Dim ramDelta As Double
        ramDelta = ramTarget1
        ramGive += Math.Min(ramAvailable, ramDelta)
        ramAvailable -= ramDelta
        If ramAvailable < 0.1 Then GoTo RamDone
        ramDelta = ramTarget2 - ramTarget1
        ramGive += Math.Min(ramAvailable * 0.7, ramDelta)
        ramAvailable -= ramDelta / 0.7
        If ramAvailable < 0.1 Then GoTo RamDone
        ramDelta = ramTarget3 - ramTarget2
        ramGive += Math.Min(ramAvailable * 0.4, ramDelta)
        ramAvailable -= ramDelta / 0.4
        If ramAvailable < 0.1 Then GoTo RamDone
        ramDelta = ramTarget3
        ramGive += Math.Min(ramAvailable * 0.15, ramDelta)
        ramAvailable -= ramDelta / 0.15
        If ramAvailable < 0.1 Then GoTo RamDone
RamDone:
        Return Math.Max(ramGive, ramMinimum)
    End Function

    ''' <summary>滑块最大值随主机内存动态计算（Sspcl 算法）。</summary>
    Private Sub UpdateSliderMax()
        Dim ramTotal = My.Computer.Info.TotalPhysicalMemory / 1024 / 1024 / 1024
        If ramTotal <= 1.5 Then
            SliderRam.MaxValue = CInt(Math.Max(Math.Floor((ramTotal - 0.3) / 0.1), 1))
        ElseIf ramTotal <= 8 Then
            SliderRam.MaxValue = CInt(Math.Floor((ramTotal - 1.5) / 0.5) + 12)
        ElseIf ramTotal <= 16 Then
            SliderRam.MaxValue = CInt(Math.Floor((ramTotal - 8) / 1) + 25)
        Else
            SliderRam.MaxValue = CInt(Math.Floor((ramTotal - 16) / 2) + 33)
        End If
    End Sub

    Private Function CalcXmxMb() As Integer
        Return CInt(GetRamGB() * 1024)
    End Function

    Private Sub ApplyMemory()
        Try
            Dim xmx = CalcXmxMb()
            Dim xms = Math.Min(xmx, 1024)
            ModStarsector.WriteMemory(ModMain.StarsectorPath, xmx, xms)
            UpdateMemoryDisplay()
        Catch ex As Exception
        End Try
    End Sub

    Private Sub UpdateMemoryDisplay()
        Try
            Dim total = CInt(My.Computer.Info.TotalPhysicalMemory / 1024 / 1024)
            Dim available = CInt(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024)
            Dim used = total - available
            Dim game = CalcXmxMb()
            Dim free = Math.Max(0, available - game)
            ColRamUsed.Width = New GridLength(Math.Max(used, 1), GridUnitType.Star)
            ColRamGame.Width = New GridLength(Math.Max(game, 1), GridUnitType.Star)
            ColRamFree.Width = New GridLength(Math.Max(free, 1), GridUnitType.Star)
            LabRamInfo.Text = "已使用 " & (used / 1024).ToString("0.0") & " GB / 总 " & (total / 1024).ToString("0.0") &
                " GB · 游戏分配 " & (game / 1024).ToString("0.0") & " GB"
        Catch ex As Exception
        End Try
    End Sub

    Private Sub BoxWinW_TextChanged() Handles BoxWinW.TextChanged
        Dim v As Integer
        If Integer.TryParse(BoxWinW.Text.Trim(), v) Then Settings.Set("StarsectorWindowWidth", v)
    End Sub

    Private Sub BoxWinH_TextChanged() Handles BoxWinH.TextChanged
        Dim v As Integer
        If Integer.TryParse(BoxWinH.Text.Trim(), v) Then Settings.Set("StarsectorWindowHeight", v)
    End Sub

    Private Sub BoxVmArgs_TextChanged() Handles BoxVmArgs.TextChanged
        Settings.Set("StarsectorVmArgs", BoxVmArgs.Text)
    End Sub

    Private Sub BtnOpenSettingsJson_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenSettingsJson.Click
        Try
            Process.Start(New ProcessStartInfo(IO.Path.Combine(ModMain.StarsectorPath, "starsector-core\data\config\settings.json")) With {.UseShellExecute = True})
        Catch ex As Exception
            MsgBox("打开失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

    Private Sub BtnOpenVmparams_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenVmparams.Click
        Try
            Process.Start(New ProcessStartInfo(IO.Path.Combine(ModMain.StarsectorPath, "vmparams")) With {.UseShellExecute = True})
        Catch ex As Exception
            MsgBox("打开失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

    Private Sub BtnOpenInstanceSetup_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenInstanceSetup.Click
        ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSetup)
    End Sub

    Private Sub BtnOpenGameDir_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenGameDir.Click
        Try
            Process.Start(New ProcessStartInfo(ModMain.StarsectorPath) With {.UseShellExecute = True})
        Catch ex As Exception
            MsgBox("打开失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

    Private Sub BtnOpenModPool_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenModPool.Click
        Try
            IO.Directory.CreateDirectory(ModMain.ModPoolDir())
            Process.Start(New ProcessStartInfo(ModMain.ModPoolDir()) With {.UseShellExecute = True})
        Catch ex As Exception
            MsgBox("打开失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

    Private Sub BtnOpenLog_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenLog.Click
        Try
            Process.Start(New ProcessStartInfo(IO.Path.Combine(ModMain.StarsectorPath, "starsector-core\starsector.log")) With {.UseShellExecute = True})
        Catch ex As Exception
            MsgBox("打开失败：" & ex.Message, MsgBoxStyle.Exclamation, "错误")
        End Try
    End Sub

End Class
