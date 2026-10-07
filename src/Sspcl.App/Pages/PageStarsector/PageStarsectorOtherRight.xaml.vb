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
        Dim dlg As New Microsoft.Win32.OpenFileDialog With {
            .Title = "导入插件（exe / bat / cmd / lnk）",
            .Filter = "程序|*.exe;*.bat;*.cmd;*.lnk|所有文件|*.*"
        }
        If dlg.ShowDialog() <> True Then Return
        Dim entry = ModMain.ImportStarsectorTool(dlg.FileName)
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
        OpenAiLoadout()
    End Sub

    Private Sub OpenAiLoadout()
        Dim ships = ModStarsector.ScanShips(ModMain.StarsectorPath)
        If ships.Count = 0 Then
            MsgBox("未扫描到任何舰船。", MsgBoxStyle.Exclamation, "AI 配装")
            Return
        End If

        Dim win As New Window With {
            .Title = "AI 配装（舰船自动装配器）",
            .Width = 860,
            .Height = 620,
            .MinWidth = 700,
            .MinHeight = 500,
            .WindowStartupLocation = WindowStartupLocation.CenterScreen,
            .Background = Brushes.White,
            .FontFamily = New FontFamily("Microsoft YaHei UI")
        }

        Dim grid As New Grid With {.Margin = New Thickness(16)}
        grid.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        grid.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})

        ' 顶部：舰船选择 + 自动装配 + 复制
        Dim top As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 0, 0, 10)}
        top.Children.Add(New TextBlock With {.Text = "舰船：", .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 8, 0), .Foreground = New SolidColorBrush(Color.FromRgb(52, 61, 74))})
        Dim combo As New ComboBox With {
            .Width = 280,
            .ItemsSource = ships,
            .DisplayMemberPath = "Display",
            .IsEditable = True,
            .IsTextSearchEnabled = True,
            .StaysOpenOnEdit = True,
            .MaxDropDownHeight = 360
        }
        top.Children.Add(combo)
        Dim genBtn As New Button With {
            .Content = "自动装配",
            .Margin = New Thickness(10, 0, 0, 0),
            .Padding = New Thickness(16, 6, 16, 6),
            .Cursor = Cursors.Hand
        }
        top.Children.Add(genBtn)
        Dim apiBtn As New Button With {
            .Content = "API 配装",
            .Margin = New Thickness(10, 0, 0, 0),
            .Padding = New Thickness(16, 6, 16, 6),
            .Cursor = Cursors.Hand
        }
        top.Children.Add(apiBtn)
        Dim copyBtn As New Button With {
            .Content = "复制到剪贴板",
            .Margin = New Thickness(10, 0, 0, 0),
            .Padding = New Thickness(16, 6, 16, 6),
            .Cursor = Cursors.Hand
        }
        top.Children.Add(copyBtn)
        grid.Children.Add(top)
        Grid.SetRow(top, 0)

        ' 中间：左=船体视图，右=配装结果
        Dim mid As New Grid
        mid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(400)})
        mid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})

        Dim shipCanvas As New Canvas With {
            .Width = 400,
            .Height = 520,
            .Background = New SolidColorBrush(Color.FromRgb(240, 244, 248)),
            .ClipToBounds = True
        }
        Dim shipBorder As New Border With {
            .BorderBrush = New SolidColorBrush(Color.FromRgb(210, 218, 226)),
            .BorderThickness = New Thickness(1),
            .Child = shipCanvas
        }
        mid.Children.Add(shipBorder)
        Grid.SetColumn(shipBorder, 0)

        Dim resultBox As New TextBox With {
            .IsReadOnly = True,
            .AcceptsReturn = True,
            .TextWrapping = TextWrapping.NoWrap,
            .VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            .HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            .FontFamily = New FontFamily("Consolas"),
            .FontSize = 13,
            .Background = New SolidColorBrush(Color.FromRgb(247, 249, 251)),
            .Margin = New Thickness(10, 0, 0, 0)
        }
        mid.Children.Add(resultBox)
        Grid.SetColumn(resultBox, 1)
        grid.Children.Add(mid)
        Grid.SetRow(mid, 1)

        Dim weapons = ModStarsector.ParseWeapons(ModMain.StarsectorPath)
        Dim manual = New Dictionary(Of Integer, ModStarsector.WeaponInfo)()

        Dim loadShip As Action(Of ModStarsector.ShipEntry, List(Of ModStarsector.WeaponInfo)) = Sub(entry, assignments)
                                                                                                    shipCanvas.Children.Clear()
                                                                                                    ' 贴图
                                                                                                    Dim spritePath = ModStarsector.FindShipSprite(ModMain.StarsectorPath, entry.Id)
                                                                                                    If spritePath <> "" Then
                                                                                                        Try
                                                                                                            Dim bmp As New BitmapImage()
                                                                                                            bmp.BeginInit()
                                                                                                            bmp.UriSource = New Uri(spritePath, UriKind.Absolute)
                                                                                                            bmp.CacheOption = BitmapCacheOption.OnLoad
                                                                                                            bmp.EndInit()
                                                                                                            Dim img As New Image With {.Source = bmp, .Stretch = Stretch.Fill, .Width = bmp.PixelWidth, .Height = bmp.PixelHeight, .SnapsToDevicePixels = True}
                                                                                                            Canvas.SetLeft(img, (400 - bmp.PixelWidth) / 2)
                                                                                                            Canvas.SetTop(img, (520 - bmp.PixelHeight) / 2)
                                                                                                            shipCanvas.Children.Add(img)
                                                                                                        Catch
                                                                                                        End Try
                                                                                                    End If
                                                                                                    ' 视图信息
                                                                                                    Dim info = ModStarsector.GetShipViewInfo(ModMain.StarsectorPath, entry.Id)
                                                                                                    Dim vw = If(info.w > 0, info.w, 288), vh = If(info.h > 0, info.h, 384)
                                                                                                    Dim imgLeft = (400 - vw) / 2, imgTop = (520 - vh) / 2
                                                                                                    ' 正确映射（devtool 源码：Point2DDeserializer + rotateCenter + rotatePointByCenter）：
                                                                                                    ' center=[c0,c1] → Point2D(x=c0,y=c1)；sprite_x = center[0] - location_Y ； sprite_y = (height - center[1]) - location_X
                                                                                                    Dim slots = ModStarsector.ParseShipSlots(ModMain.StarsectorPath, entry.Id)
                                                                                                    For i = 0 To slots.Count - 1
                                                                                                        Dim s = slots(i)
                                                                                                        Dim mx = imgLeft + Math.Max(0, Math.Min(vw, info.cx - s.Y))
                                                                                                        Dim my = imgTop + Math.Max(0, Math.Min(vh, (info.h - info.cy) - s.X))
                                                                                                        Dim idx = i
                                                                                                        Dim assigned As ModStarsector.WeaponInfo = If(assignments IsNot Nothing AndAlso i < assignments.Count, assignments(i), Nothing)
                                                                                                        ' 有武器 → 覆盖武器贴图（按槽位角度旋转）
                                                                                                        If assigned IsNot Nothing Then
                                                                                                            Dim wsp = ModStarsector.ResolveWeaponSprite(ModMain.StarsectorPath, assigned, s.Mount)
                                                                                                            If wsp <> "" Then
                                                                                                                Try
                                                                                                                    Dim wbmp As New BitmapImage()
                                                                                                                    wbmp.BeginInit()
                                                                                                                    wbmp.UriSource = New Uri(wsp, UriKind.Absolute)
                                                                                                                    wbmp.CacheOption = BitmapCacheOption.OnLoad
                                                                                                                    wbmp.EndInit()
                                                                                                                    Dim wdim = If(s.Size = "LARGE", 38.0, If(s.Size = "MEDIUM", 30.0, 24.0))
                                                                                                                    Dim wimg As New Image With {
                                                                                                                        .Source = wbmp,
                                                                                                                        .Stretch = Stretch.Uniform,
                                                                                                                        .Width = wdim,
                                                                                                                        .Height = wdim,
                                                                                                                        .RenderTransformOrigin = New Point(0.5, 0.5),
                                                                                                                        .RenderTransform = New RotateTransform(s.Angle),
                                                                                                                        .Cursor = Cursors.Hand,
                                                                                                                        .ToolTip = assigned.Name & "（" & assigned.Id & "）" & assigned.OPs & " OP"
                                                                                                                    }
                                                                                                                    Canvas.SetLeft(wimg, mx - wdim / 2)
                                                                                                                    Canvas.SetTop(wimg, my - wdim / 2)
                                                                                                                    shipCanvas.Children.Add(wimg)
                                                                                                                    AddHandler wimg.MouseLeftButtonUp, Sub()
                                                                                                                                                           Dim picked = ShowWeaponPicker(s, weapons)
                                                                                                                                                           If picked IsNot Nothing Then
                                                                                                                                                               manual(idx) = picked
                                                                                                                                                               loadShip(entry, ModStarsector.BuildAssignments(ModMain.StarsectorPath, entry, manual))
                                                                                                                                                               resultBox.Text = ModStarsector.GeneratePreciseLoadout(ModMain.StarsectorPath, entry, manual)
                                                                                                                                                           End If
                                                                                                                                                       End Sub
                                                                                                                    Continue For
                                                                                                                Catch
                                                                                                                End Try
                                                                                                            End If
                                                                                                        End If
                                                                                                        ' 无武器 → 圆点
                                                                                                        Dim marker As New Border With {
                                                                                                            .Width = 12,
                                                                                                            .Height = 12,
                                                                                                            .CornerRadius = New CornerRadius(6),
                                                                                                            .Background = SlotColor(s.Type),
                                                                                                            .BorderBrush = Brushes.White,
                                                                                                            .BorderThickness = New Thickness(1),
                                                                                                            .Cursor = Cursors.Hand,
                                                                                                            .ToolTip = TSizeLabel(s.Size) & " " & TSlotLabel(s.Type) & "（" & TMountLabel(s.Mount) & "）"
                                                                                                        }
                                                                                                        Canvas.SetLeft(marker, mx - 6)
                                                                                                        Canvas.SetTop(marker, my - 6)
                                                                                                        shipCanvas.Children.Add(marker)
                                                                                                        AddHandler marker.MouseLeftButtonUp, Sub()
                                                                                                                                               Dim picked = ShowWeaponPicker(s, weapons)
                                                                                                                                               If picked IsNot Nothing Then
                                                                                                                                                   manual(idx) = picked
                                                                                                                                                   loadShip(entry, ModStarsector.BuildAssignments(ModMain.StarsectorPath, entry, manual))
                                                                                                                                                   resultBox.Text = ModStarsector.GeneratePreciseLoadout(ModMain.StarsectorPath, entry, manual)
                                                                                                                                               End If
                                                                                                                                           End Sub
                                                                                                    Next
                                                                                                End Sub

        AddHandler genBtn.Click, Sub()
                                     Dim entry = TryCast(combo.SelectedItem, ModStarsector.ShipEntry)
                                     If entry Is Nothing Then
                                         resultBox.Text = "请先选择舰船。"
                                         Return
                                     End If
                                     manual.Clear()
                                     Dim a = ModStarsector.BuildAssignments(ModMain.StarsectorPath, entry, manual)
                                     loadShip(entry, a)
                                     Dim r = ModStarsector.GeneratePreciseLoadout(ModMain.StarsectorPath, entry, manual)
                                     If String.IsNullOrWhiteSpace(r) Then r = ModStarsector.GenerateLoadout(ModMain.StarsectorPath, entry)
                                     resultBox.Text = r
                                 End Sub
        AddHandler combo.SelectionChanged, Sub()
                                                If combo.SelectedItem IsNot Nothing Then genBtn.RaiseEvent(New RoutedEventArgs(Button.ClickEvent))
                                            End Sub
        AddHandler copyBtn.Click, Sub()
                                      If Not String.IsNullOrWhiteSpace(resultBox.Text) Then
                                          Try
                                              Clipboard.SetText(resultBox.Text)
                                          Catch
                                          End Try
                                      End If
                                  End Sub
        AddHandler apiBtn.Click, Async Sub()
                                     Dim entry = TryCast(combo.SelectedItem, ModStarsector.ShipEntry)
                                     If entry Is Nothing Then
                                         resultBox.Text = "请先选择舰船。"
                                         Return
                                     End If
                                     Await RunApiLoadout(entry, resultBox)
                                 End Sub

        win.Content = grid
        win.ShowDialog()
    End Sub

    Private Async Function RunApiLoadout(entry As ModStarsector.ShipEntry, resultBox As TextBox) As Task
        Dim url = Settings.Get(Of String)("StarsectorAiApiUrl")
        If String.IsNullOrWhiteSpace(url) Then
            url = InputBox("API 地址（OpenAI 兼容，需含 /chat/completions）：", "API 配装", "https://api.deepseek.com/v1/chat/completions")
            If String.IsNullOrWhiteSpace(url) Then Return
            Settings.Set("StarsectorAiApiUrl", url)
        End If
        Dim key = Settings.Get(Of String)("StarsectorAiApiKey")
        If String.IsNullOrWhiteSpace(key) Then
            key = InputBox("API Key（可选，若不填则匿名）：", "API 配装")
            If key IsNot Nothing Then Settings.Set("StarsectorAiApiKey", key)
        End If
        Dim model = Settings.Get(Of String)("StarsectorAiApiModel")
        resultBox.Text = "正在请求 API 配装……"
        Dim prompt = ModStarsector.BuildLoadoutPrompt(ModMain.StarsectorPath, entry)
        Try
            Dim r = Await ModStarsector.GenerateLoadoutViaApiAsync(prompt, url, key, model)
            resultBox.Text = r
        Catch ex As Exception
            resultBox.Text = "API 配装失败：" & ex.Message
        End Try
    End Function

    Private Function ShowWeaponPicker(slot As ModStarsector.ShipSlot, weapons As List(Of ModStarsector.WeaponInfo)) As ModStarsector.WeaponInfo
        Dim compatible = ModStarsector.GetCompatibleWeapons(slot, weapons)
        If compatible.Count = 0 Then
            MsgBox("该槽位没有兼容武器。", MsgBoxStyle.Information, "选择武器")
            Return Nothing
        End If
        Dim win As New Window With {
            .Title = "选择武器：" & TSizeLabel(slot.Size) & " " & TSlotLabel(slot.Type) & "（" & TMountLabel(slot.Mount) & "）",
            .Width = 420,
            .Height = 150,
            .WindowStartupLocation = WindowStartupLocation.CenterScreen,
            .Background = Brushes.White,
            .FontFamily = New FontFamily("Microsoft YaHei UI"),
            .ResizeMode = ResizeMode.NoResize
        }
        Dim panel As New StackPanel With {.Margin = New Thickness(16)}
        Dim combo As New ComboBox With {.ItemsSource = compatible, .DisplayMemberPath = "Display", .MaxDropDownHeight = 320, .IsEditable = True, .IsTextSearchEnabled = True}
        panel.Children.Add(combo)
        Dim okBtn As New Button With {
            .Content = "确定",
            .Padding = New Thickness(18, 6, 18, 6),
            .Margin = New Thickness(0, 12, 0, 0),
            .HorizontalAlignment = HorizontalAlignment.Center,
            .Cursor = Cursors.Hand
        }
        panel.Children.Add(okBtn)
        Dim result As ModStarsector.WeaponInfo = Nothing
        AddHandler okBtn.Click, Sub()
                                    result = TryCast(combo.SelectedItem, ModStarsector.WeaponInfo)
                                    win.Close()
                                End Sub
        win.Content = panel
        win.ShowDialog()
        Return result
    End Function

    Private Function SlotColor(t As String) As Brush
        Select Case t
            Case "BALLISTIC" : Return New SolidColorBrush(Color.FromRgb(229, 115, 115))
            Case "ENERGY" : Return New SolidColorBrush(Color.FromRgb(100, 181, 246))
            Case "MISSILE" : Return New SolidColorBrush(Color.FromRgb(255, 183, 77))
            Case "UNIVERSAL" : Return New SolidColorBrush(Color.FromRgb(189, 189, 189))
            Case "HYBRID" : Return New SolidColorBrush(Color.FromRgb(149, 117, 205))
            Case "SYNERGY" : Return New SolidColorBrush(Color.FromRgb(77, 182, 172))
            Case "COMPOSITE" : Return New SolidColorBrush(Color.FromRgb(240, 98, 146))
            Case Else : Return Brushes.Gray
        End Select
    End Function

    Private Function TSlotLabel(t As String) As String
        Select Case t
            Case "BALLISTIC" : Return "实弹"
            Case "ENERGY" : Return "能量"
            Case "MISSILE" : Return "导弹"
            Case "UNIVERSAL" : Return "通用"
            Case "HYBRID" : Return "混合"
            Case "SYNERGY" : Return "协同"
            Case "COMPOSITE" : Return "复合"
            Case Else : Return t
        End Select
    End Function

    Private Function TSizeLabel(s As String) As String
        Select Case s
            Case "SMALL" : Return "小型"
            Case "MEDIUM" : Return "中型"
            Case "LARGE" : Return "大型"
            Case Else : Return s
        End Select
    End Function

    Private Function TMountLabel(m As String) As String
        Select Case m
            Case "TURRET" : Return "炮塔"
            Case "HARDPOINT" : Return "硬点"
            Case Else : Return m
        End Select
    End Function

    ' ============ 反馈 ============
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
