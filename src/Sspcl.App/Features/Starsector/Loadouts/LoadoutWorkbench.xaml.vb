Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports Sspcl.Core.Loadouts

Public Partial Class LoadoutWorkbench
    Private ReadOnly _gamePath As String
    Private _catalog As LoadoutCatalog
    Private _hull As HullDefinition
    Private _slot As WeaponSlot
    Private _plan As LoadoutPlan
    Private _sync As Boolean
    Private _revision As Integer
    Private _load As CancellationTokenSource
    Private _ai As CancellationTokenSource
    Private ReadOnly _plans As New Dictionary(Of String, LoadoutPlan)(StringComparer.Ordinal)
    Private ReadOnly _undo As New Stack(Of LoadoutPlan)
    Private ReadOnly _redo As New Stack(Of LoadoutPlan)
    Private _apiKey As String = ""
    Private _closed As Boolean

    Public Sub New(gamePath As String)
        InitializeComponent()
        Dim workArea = SystemParameters.WorkArea
        Width = Math.Min(1380, Math.Max(900, workArea.Width - 40))
        Height = Math.Min(940, Math.Max(700, workArea.Height - 40))
        MinWidth = Math.Min(1080, Width) : MinHeight = Math.Min(740, Height)
        If Width < 1200 Then SetHullSidebar(False)
        _gamePath = gamePath
        Try
            Dim protectedKey = Settings.Get(Of String)("StarsectorAiApiKeyProtected")
            If protectedKey <> "" Then
                _apiKey = Text.Encoding.UTF8.GetString(Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(protectedKey), Nothing, Security.Cryptography.DataProtectionScope.CurrentUser))
            Else
                _apiKey = Settings.Get(Of String)("StarsectorAiApiKey")
            End If
        Catch
            _apiKey = ""
        End Try
    End Sub

    Private Async Sub WindowLoaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Await ReloadAsync()
    End Sub
    Private Sub WindowClosed(sender As Object, e As EventArgs) Handles Me.Closed
        _closed = True
        If _load IsNot Nothing Then _load.Cancel()
        If _ai IsNot Nothing Then _ai.Cancel()
    End Sub
    Private Async Sub ReloadClick(sender As Object, e As RoutedEventArgs) Handles BtnReload.Click
        Await ReloadAsync()
    End Sub
    Private Async Function ReloadAsync() As Task
        If _load IsNot Nothing Then _load.Cancel()
        Dim source As New CancellationTokenSource()
        _load = source
        BtnReload.IsEnabled = False
        LabStatus.Text = "读取船体、皮肤、武器和启用的 MOD…"
        Try
            Dim catalog = Await Task.Run(Function() LoadoutCatalogReader.Read(_gamePath, source.Token), source.Token)
            If _closed OrElse source.IsCancellationRequested Then Return
            ApplyCatalog(catalog)
        Catch ex As OperationCanceledException
        Catch ex As Exception
            If Not _closed Then LabStatus.Text = "读取失败：" & ex.Message
        Finally
            If Object.ReferenceEquals(_load, source) Then
                _load = Nothing
                BtnReload.IsEnabled = True
            End If
            source.Dispose()
        End Try
    End Function
    Private Sub ApplyCatalog(catalog As LoadoutCatalog)
        If _ai IsNot Nothing Then _ai.Cancel()
        _catalog = catalog
        _bitmaps.Clear()
        LabCatalog.Text = catalog.Hulls.Count & " 艘舰船 · " & catalog.Weapons.Count & " 种武器 · " & catalog.HullMods.Count & " 种船插"
        LabCatalog.ToolTip = String.Join(vbCrLf, catalog.Warnings)
        FilterHulls()
        If HullList.Items.Count > 0 Then
            Dim initial = catalog.Hulls.FirstOrDefault(Function(h) h.Id = "onslaught")
            HullList.SelectedItem = If(initial, catalog.Hulls.FirstOrDefault(Function(h) h.Slots.Any(Function(s) s.CanEquip)))
            If HullList.SelectedItem Is Nothing Then HullList.SelectedIndex = 0
            HullList.ScrollIntoView(HullList.SelectedItem)
        End If
        LabStatus.Text = "数据已就绪。仅读取当前启用的 MOD；导出不修改存档。" & If(catalog.Warnings.Count = 0, "", " " & catalog.Warnings.Count & " 条读取提示（悬停目录统计查看）。")
    End Sub
    Private Sub HullSearchChanged(sender As Object, e As TextChangedEventArgs) Handles BoxHullSearch.TextChanged
        If _catalog IsNot Nothing Then FilterHulls()
    End Sub
    Private Sub FilterHulls()
        Dim selected = TryCast(HullList.SelectedItem, HullDefinition)
        Dim search = BoxHullSearch.Text.Trim()
        HullList.ItemsSource = _catalog.Hulls.Where(Function(h) (h.Name & " " & h.Id & " " & h.Source).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
        If selected IsNot Nothing AndAlso HullList.Items.Contains(selected) Then HullList.SelectedItem = selected
    End Sub
    Private Sub HullSelected(sender As Object, e As SelectionChangedEventArgs) Handles HullList.SelectionChanged
        Dim selected = TryCast(HullList.SelectedItem, HullDefinition)
        If selected Is Nothing Then Return
        If Object.ReferenceEquals(selected, _hull) AndAlso _plan IsNot Nothing Then Return
        If _ai IsNot Nothing Then _ai.Cancel()
        _hull = selected
        If _plans.ContainsKey(_hull.Id) AndAlso LoadoutRules.Evaluate(_hull, _catalog.Weapons, _plans(_hull.Id), _catalog.HullMods).Valid Then
            _plan = LoadoutRules.Copy(_plans(_hull.Id))
        Else
            _plan = New LoadoutPlan With {.HullId = _hull.Id, .Name = _hull.Name & " SSPCL"}
        End If
        _revision += 1
        _undo.Clear() : _redo.Clear()
        LabHull.Text = _hull.Name
        LabHullDetail.Text = _hull.Id & " · " & _hull.Source & " · " & _hull.Width & " × " & _hull.Height
        BindHullDetails()
        _slot = Nothing
        FitPreview()
        SyncFields()
        RefreshPlan()
        If SlotList.Items.Count > 0 Then SlotList.SelectedIndex = 0
    End Sub
    Private Sub SyncFields()
        _sync = True
        BoxName.Text = _plan.Name
        BoxVents.Text = _plan.Vents.ToString()
        BoxCaps.Text = _plan.Capacitors.ToString()
        _sync = False
    End Sub
    Private Function FieldsValid() As Boolean
        Dim vents, caps As Integer
        Return Integer.TryParse(BoxVents.Text, vents) AndAlso Integer.TryParse(BoxCaps.Text, caps) AndAlso vents >= 0 AndAlso caps >= 0
    End Function
    Private Sub FieldsChanged(sender As Object, e As TextChangedEventArgs) Handles BoxName.TextChanged, BoxVents.TextChanged, BoxCaps.TextChanged
        If _sync OrElse _plan Is Nothing Then Return
        If Not FieldsValid() Then
            LabStatus.Text = "通风与电容请输入非负整数。"
            BtnExport.IsEnabled = False : BtnGenerate.IsEnabled = False
            Return
        End If
        Remember()
        _plan.Name = BoxName.Text
        _plan.Vents = Integer.Parse(BoxVents.Text)
        _plan.Capacitors = Integer.Parse(BoxCaps.Text)
        RefreshPlan()
    End Sub
    Private Sub Remember()
        _undo.Push(LoadoutRules.Copy(_plan))
        _redo.Clear()
        _revision += 1
    End Sub
    Private Sub RefreshPlan()
        If _hull Is Nothing Then Return
        _plans(_hull.Id) = LoadoutRules.Copy(_plan)
        Dim check = LoadoutRules.Evaluate(_hull, _catalog.Weapons, _plan, _catalog.HullMods)
        LabBudget.Text = "OP  " & check.TotalOp & " / " & _hull.OrdnancePoints
        LabBudget.ToolTip = "武器 " & check.WeaponOp & " + 普通船插 " & check.HullModOp & " + 通风/电容 " & (_plan.Vents + _plan.Capacitors)
        OpBar.Maximum = Math.Max(1, _hull.OrdnancePoints)
        OpBar.Value = check.TotalOp
        OpBar.Foreground = If(check.Valid, New SolidColorBrush(Color.FromRgb(40, 102, 219)), Brushes.IndianRed)
        LabFlux.Text = "武器幅能 " & Math.Round(check.WeaponFlux) & " / 散幅 " & Math.Round(_hull.FluxDissipation + _plan.Vents * 10)
        LabFlux.ToolTip = "估算长期射击幅能，考虑弹药再生上限，包含已知内置武器，不含导弹；散幅含通风。不模拟船插和脚本。"
        Dim dissipation = _hull.FluxDissipation + _plan.Vents * 10
        LabFlux.Foreground = If(dissipation > 0 AndAlso check.WeaponFlux > dissipation * 1.3, Brushes.IndianRed, Brushes.SlateGray)
        LabExplanation.Text = If(_plan.Explanation = "", "手工安装会锁定槽位。通风、电容各最多 " & _hull.FluxUpgradeLimit & "。滚轮缩放，中键拖动。", _plan.Explanation)
        LabStatus.Text = If(check.Valid, "装配有效。" & If(_hull.Slots.Any(Function(s) s.Type = "STATION_MODULE"), " 此舰含子模块，当前只可预览，无法导出完整配置。", "导出含普通与内置船插、武器组、通风和电容。船插效果由游戏运行脚本。"), String.Join("；", check.Errors))
        BtnExport.IsEnabled = check.Valid AndAlso FieldsValid() AndAlso Not _hull.Slots.Any(Function(s) s.Type = "STATION_MODULE")
        BtnGenerate.IsEnabled = check.Valid AndAlso FieldsValid()
        BtnUndo.IsEnabled = _undo.Count > 0 : BtnRedo.IsEnabled = _redo.Count > 0
        Dim slotId = If(_slot Is Nothing, "", _slot.Id)
        _sync = True
        SlotList.Items.Clear()
        For Each slot In _hull.Slots.Where(Function(s) s.CanEquip OrElse _hull.BuiltInWeapons.ContainsKey(s.Id))
            Dim weaponId As String = ""
            If Not _hull.BuiltInWeapons.TryGetValue(slot.Id, weaponId) Then _plan.Weapons.TryGetValue(slot.Id, weaponId)
            Dim weapon = _catalog.Weapons.FirstOrDefault(Function(w) w.Id = weaponId)
            SlotList.Items.Add(New ListBoxItem With {.Tag = slot, .Content = slot.Id & " · " & If(weapon Is Nothing, If(weaponId = "", "空槽", weaponId), weapon.Name) & If(slot.Locked, " [内置]", If(_plan.PinnedSlots.Contains(slot.Id), " [锁定]", "")), .ToolTip = slot.Display})
        Next
        For Each row As ListBoxItem In SlotList.Items
            If DirectCast(row.Tag, WeaponSlot).Id = slotId Then SlotList.SelectedItem = row
        Next
        _sync = False
        RefreshSlot()
        RefreshDamageAndDetails()
        RenderShip()
    End Sub
    Private Sub SlotSelected(sender As Object, e As SelectionChangedEventArgs) Handles SlotList.SelectionChanged
        If _sync Then Return
        Dim row = TryCast(SlotList.SelectedItem, ListBoxItem)
        _slot = If(row Is Nothing, Nothing, TryCast(row.Tag, WeaponSlot))
        RefreshSlot()
        RenderShip()
    End Sub
    Private Sub SelectSlot(slot As WeaponSlot)
        For Each row As ListBoxItem In SlotList.Items
            If DirectCast(row.Tag, WeaponSlot).Id = slot.Id Then
                SlotList.SelectedItem = row
                row.BringIntoView()
                Return
            End If
        Next
    End Sub
    Private Sub RefreshSlot()
        _sync = True
        LabSlot.Text = If(_slot Is Nothing, "点击舰船上的圆点选择槽位", _slot.Display & If(_slot.Locked, " · 内置不可更换", ""))
        CheckPin.IsEnabled = _slot IsNot Nothing AndAlso _slot.CanEquip
        CheckPin.IsChecked = _slot IsNot Nothing AndAlso (_slot.Locked OrElse _plan.PinnedSlots.Contains(_slot.Id))
        BtnClear.IsEnabled = CheckPin.IsEnabled
        _sync = False
        FilterWeapons()
        If _slot IsNot Nothing Then
            Dim currentId As String = ""
            If Not _hull.BuiltInWeapons.TryGetValue(_slot.Id, currentId) Then _plan.Weapons.TryGetValue(_slot.Id, currentId)
            Dim current = _catalog.Weapons.FirstOrDefault(Function(w) w.Id = currentId)
            If current IsNot Nothing Then
                BindWeaponDetails(current)
                If WeaponList.Items.Contains(current) Then WeaponList.SelectedItem = current
            End If
        End If
    End Sub
    Private Sub WeaponSearchChanged(sender As Object, e As TextChangedEventArgs) Handles BoxWeaponSearch.TextChanged
        If _catalog IsNot Nothing Then FilterWeapons()
    End Sub
    Private Sub FilterWeapons()
        Dim search = BoxWeaponSearch.Text.Trim()
        If _slot Is Nothing OrElse Not _slot.CanEquip Then
            WeaponList.ItemsSource = Nothing
        Else
            WeaponList.ItemsSource = _catalog.Weapons.Where(Function(w) LoadoutRules.Fits(_slot, w) AndAlso (w.Name & " " & w.Id & " " & w.Source).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).OrderByDescending(Function(w) LoadoutRules.SizeRank(w.Size)).ThenBy(Function(w) w.Name).ToList()
        End If
        BtnInstall.IsEnabled = False
    End Sub
    Private Sub WeaponSelected(sender As Object, e As SelectionChangedEventArgs) Handles WeaponList.SelectionChanged
        Dim weapon = TryCast(WeaponList.SelectedItem, WeaponDefinition)
        BtnInstall.IsEnabled = weapon IsNot Nothing AndAlso _slot IsNot Nothing AndAlso _slot.CanEquip
        LabWeapon.Text = If(weapon Is Nothing, "仅显示兼容当前槽位的武器。", weapon.Id & " · " & weapon.Type & " " & weapon.Size & vbCrLf & "DPS " & Math.Round(weapon.Dps) & " · 幅能 " & Math.Round(weapon.FluxPerSecond) & " · " & weapon.DamageType)
        If weapon IsNot Nothing Then
            LabWeapon.Text &= vbCrLf & "完整属性、说明与参数见「武器详解」。"
            BindWeaponDetails(weapon)
        End If
    End Sub
    Private Sub InstallClick(sender As Object, e As RoutedEventArgs) Handles BtnInstall.Click
        InstallWeapon()
    End Sub
    Private Sub WeaponDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles WeaponList.MouseDoubleClick
        InstallWeapon()
    End Sub
    Private Sub InstallWeapon()
        Dim weapon = TryCast(WeaponList.SelectedItem, WeaponDefinition)
        If weapon Is Nothing OrElse _slot Is Nothing OrElse Not LoadoutRules.Fits(_slot, weapon) Then Return
        Dim proposal = LoadoutRules.Copy(_plan)
        proposal.Weapons(_slot.Id) = weapon.Id
        proposal.PinnedSlots.Add(_slot.Id)
        Dim check = LoadoutRules.Evaluate(_hull, _catalog.Weapons, proposal, _catalog.HullMods)
        If Not check.Valid Then
            LabStatus.Text = String.Join("；", check.Errors) : Return
        End If
        Remember()
        _plan = proposal
        RefreshPlan()
    End Sub
    Private Sub ClearClick(sender As Object, e As RoutedEventArgs) Handles BtnClear.Click
        If _slot Is Nothing OrElse Not _slot.CanEquip Then Return
        Remember()
        _plan.Weapons.Remove(_slot.Id)
        _plan.PinnedSlots.Add(_slot.Id)
        RefreshPlan()
    End Sub
    Private Sub PinChanged(sender As Object, e As RoutedEventArgs) Handles CheckPin.Checked, CheckPin.Unchecked
        If _sync OrElse _slot Is Nothing OrElse Not _slot.CanEquip Then Return
        Remember()
        If CheckPin.IsChecked = True Then _plan.PinnedSlots.Add(_slot.Id) Else _plan.PinnedSlots.Remove(_slot.Id)
        RefreshPlan()
    End Sub
    Private Sub UndoClick(sender As Object, e As RoutedEventArgs) Handles BtnUndo.Click
        If _undo.Count = 0 Then Return
        _redo.Push(LoadoutRules.Copy(_plan))
        _plan = _undo.Pop() : _revision += 1
        SyncFields() : RefreshPlan()
    End Sub
    Private Sub RedoClick(sender As Object, e As RoutedEventArgs) Handles BtnRedo.Click
        If _redo.Count = 0 Then Return
        _undo.Push(LoadoutRules.Copy(_plan))
        _plan = _redo.Pop() : _revision += 1
        SyncFields() : RefreshPlan()
    End Sub
    Private Sub GenerateClick(sender As Object, e As RoutedEventArgs) Handles BtnGenerate.Click
        If _hull Is Nothing OrElse Not FieldsValid() Then Return
        Try
            Dim proposal = LoadoutPlanner.Generate(_hull, _catalog.Weapons, _plan, CType(BoxStyle.SelectedIndex, LoadoutStyle), _catalog.HullMods)
            Remember() : _plan = proposal
            SyncFields() : RefreshPlan()
        Catch ex As Exception
            LabStatus.Text = ex.Message
        End Try
    End Sub
    Private Async Sub AiClick(sender As Object, e As RoutedEventArgs) Handles BtnAi.Click
        If _hull Is Nothing OrElse Not FieldsValid() OrElse _ai IsNot Nothing Then Return
        If String.IsNullOrWhiteSpace(Settings.Get(Of String)("StarsectorAiApiUrl")) Then
            ShowAiSettings() : Return
        End If
        Dim source As New CancellationTokenSource(TimeSpan.FromSeconds(120))
        _ai = source
        Dim revision = _revision
        Dim hull = _hull
        Dim original = LoadoutRules.Copy(_plan)
        BtnAi.IsEnabled = False : BtnCancel.Visibility = Visibility.Visible
        LabStatus.Text = "AI 正在生成建议…可取消；修改装配后此结果会丢弃。"
        Try
            Using client As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(120), .MaxResponseContentBufferSize = 1024 * 1024}
                Dim proposal = Await ChatLoadoutAdvisor.SuggestAsync(client, Settings.Get(Of String)("StarsectorAiApiUrl"), _apiKey, Settings.Get(Of String)("StarsectorAiApiModel"), hull, _catalog.Weapons, original, CType(BoxStyle.SelectedIndex, LoadoutStyle), source.Token, _catalog.HullMods)
                If _closed OrElse source.IsCancellationRequested Then Return
                If revision <> _revision OrElse Not Object.ReferenceEquals(hull, _hull) Then
                    LabStatus.Text = "装配已变化，已丢弃过期的 AI 建议。" : Return
                End If
                Remember() : _plan = proposal
                SyncFields() : RefreshPlan()
            End Using
        Catch ex As OperationCanceledException
            If Not _closed Then LabStatus.Text = "生成已取消或超时，现有装配保留。"
        Catch ex As Exception
            If Not _closed Then LabStatus.Text = "AI 建议未应用：" & ex.Message
        Finally
            _ai = Nothing
            source.Dispose()
            BtnAi.IsEnabled = True : BtnCancel.Visibility = Visibility.Collapsed
        End Try
    End Sub
    Private Sub CancelClick(sender As Object, e As RoutedEventArgs) Handles BtnCancel.Click
        If _ai IsNot Nothing Then _ai.Cancel()
    End Sub
    Private Sub ExportClick(sender As Object, e As RoutedEventArgs) Handles BtnExport.Click
        If _hull Is Nothing OrElse Not FieldsValid() Then Return
        Dim id = _hull.Id & "_sspcl"
        Dim dialog As New Microsoft.Win32.SaveFileDialog With {.Title = "导出游戏武器装配配置", .Filter = "Starsector variant|*.variant", .FileName = id & ".variant", .DefaultExt = ".variant"}
        If dialog.ShowDialog(Me) <> True Then Return
        Try
            id = IO.Path.GetFileNameWithoutExtension(dialog.FileName)
            Dim json = LoadoutVariant.Serialize(_hull, _catalog.Weapons, _plan, id, _catalog.HullMods)
            IO.File.WriteAllText(dialog.FileName, json, New Text.UTF8Encoding(False))
            LabStatus.Text = "已导出 " & dialog.FileName & "。在自建 MOD 的 data/variants 使用，游戏需重新加载该 MOD；不会直接写入存档。"
        Catch ex As Exception
            LabStatus.Text = "导出失败：" & ex.Message
        End Try
    End Sub
    Private Sub ImportClick(sender As Object, e As RoutedEventArgs) Handles BtnImport.Click
        If _hull Is Nothing Then Return
        Dim dialog As New Microsoft.Win32.OpenFileDialog With {.Title = "导入当前舰船的武器配置", .Filter = "Starsector variant|*.variant"}
        If dialog.ShowDialog(Me) <> True Then Return
        Try
            Dim proposal = LoadoutVariant.Read(dialog.FileName, _hull, _catalog.Weapons, _catalog.HullMods)
            Remember() : _plan = proposal
            SyncFields() : RefreshPlan()
        Catch ex As Exception
            LabStatus.Text = "导入未应用：" & ex.Message
        End Try
    End Sub
    Private Sub ApiSettingsClick(sender As Object, e As RoutedEventArgs) Handles BtnApiSettings.Click
        ShowAiSettings()
    End Sub
    Private Sub ShowAiSettings()
        Dim dialog As New Window With {.Title = "AI 服务设置", .Owner = Me, .Width = 520, .Height = 390, .ResizeMode = ResizeMode.NoResize, .WindowStartupLocation = WindowStartupLocation.CenterOwner, .Background = Brushes.White, .FontFamily = FontFamily}
        Dim panel As New StackPanel With {.Margin = New Thickness(22)}
        dialog.Content = panel
        panel.Children.Add(New TextBlock With {.Text = "完整 chat/completions 地址"})
        Dim url As New TextBox With {.Text = Settings.Get(Of String)("StarsectorAiApiUrl"), .Margin = New Thickness(0, 6, 0, 12)}
        panel.Children.Add(url)
        panel.Children.Add(New TextBlock With {.Text = "模型名称"})
        Dim model As New TextBox With {.Text = Settings.Get(Of String)("StarsectorAiApiModel"), .Margin = New Thickness(0, 6, 0, 12)}
        panel.Children.Add(model)
        panel.Children.Add(New TextBlock With {.Text = "API 密钥"})
        Dim password As New PasswordBox With {.Password = _apiKey, .Padding = New Thickness(8), .Margin = New Thickness(0, 6, 0, 12)}
        panel.Children.Add(password)
        Dim rememberKey As New CheckBox With {.Content = "使用当前 Windows 用户加密保存密钥", .IsChecked = Settings.Get(Of String)("StarsectorAiApiKeyProtected") <> ""}
        panel.Children.Add(rememberKey)
        panel.Children.Add(New TextBlock With {.Text = "点击 AI 建议时，会向此服务发送当前船体、兼容武器与装配数据。", .TextWrapping = TextWrapping.Wrap, .Foreground = Brushes.SlateGray, .Margin = New Thickness(0, 12, 0, 12)})
        Dim save As New Button With {.Content = "保存", .Padding = New Thickness(14, 8, 14, 8), .HorizontalAlignment = HorizontalAlignment.Right}
        panel.Children.Add(save)
        AddHandler save.Click, Sub()
                                  Try
                                      _apiKey = password.Password.Trim()
                                      Settings.Set("StarsectorAiApiUrl", url.Text.Trim())
                                      Settings.Set("StarsectorAiApiModel", model.Text.Trim())
                                      Settings.Set("StarsectorAiApiKeyProtected", If(rememberKey.IsChecked = True, Convert.ToBase64String(Security.Cryptography.ProtectedData.Protect(Text.Encoding.UTF8.GetBytes(_apiKey), Nothing, Security.Cryptography.DataProtectionScope.CurrentUser)), ""))
                                      Settings.Set("StarsectorAiApiKey", "")
                                      dialog.Close()
                                  Catch ex As Exception
                                      MessageBox.Show(dialog, ex.Message, "保存失败")
                                  End Try
                              End Sub
        dialog.ShowDialog()
    End Sub
End Class
