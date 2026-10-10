Imports Sspcl.Core.Loadouts
Imports System.Windows.Controls.Primitives
Imports System.Windows.Threading

Public Partial Class LoadoutWorkbench
    Private _filterSync As Boolean
    Private _compareSync As Boolean
    Private _compareLeft As WeaponDefinition
    Private _compareRight As WeaponDefinition
    Private _hoverWeapon As WeaponDefinition
    Private _hoverTarget As FrameworkElement
    Private ReadOnly _hoverDelay As New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(350)}
    Private ReadOnly _hoverClose As New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(180)}

    Private Sub InitializeInspection()
        _filterSync = True
        FillFilter(FilterDamage, {"全部伤害", "动能", "高爆", "能量", "破片"}, {"", "KINETIC", "HIGH_EXPLOSIVE", "ENERGY", "FRAGMENTATION"})
        FillFilter(FilterSize, {"全部尺寸", "小型", "中型", "大型"}, {"", "SMALL", "MEDIUM", "LARGE"})
        FillFilter(FilterType, {"全部类型", "弹道", "能量", "导弹"}, {"", "BALLISTIC", "ENERGY", "MISSILE"})
        FillFilter(FilterFeature, {"全部特性", "光束", "投射物", "防空", "有限弹药", "无限弹药"}, {"", "BEAM", "PROJECTILE", "PD", "FINITE", "UNLIMITED"})
        FillFilter(FilterSource, {"全部来源"}, {""})
        _filterSync = False
        AddHandler _hoverDelay.Tick, Sub(sender, e)
                                         _hoverDelay.Stop()
                                         If _hoverWeapon IsNot Nothing AndAlso _hoverTarget IsNot Nothing AndAlso _hoverTarget.IsMouseOver Then ShowWeaponPopup(_hoverWeapon, _hoverTarget)
                                     End Sub
        AddHandler _hoverClose.Tick, Sub(sender, e)
                                         _hoverClose.Stop()
                                         If Not WeaponPopupFrame.IsMouseOver AndAlso (_hoverTarget Is Nothing OrElse Not _hoverTarget.IsMouseOver) Then WeaponPopup.IsOpen = False
                                     End Sub
        DataBody.AddHandler(Expander.ExpandedEvent, New RoutedEventHandler(AddressOf DataExpanded))
        DataBody.AddHandler(Expander.CollapsedEvent, New RoutedEventHandler(AddressOf DataExpanded))
        WeaponList.AddHandler(ScrollViewer.ScrollChangedEvent, New ScrollChangedEventHandler(AddressOf WeaponScrollChanged))
    End Sub
    Private Sub WeaponScrollChanged(sender As Object, e As ScrollChangedEventArgs)
        If e.VerticalChange <> 0 Then CloseWeaponDetails()
    End Sub
    Private Sub DataExpanded(sender As Object, e As RoutedEventArgs)
        UpdateOverlaySizes()
    End Sub
    Private Sub FillFilter(box As ComboBox, labels As String(), values As String())
        box.Items.Clear()
        For i = 0 To labels.Length - 1
            box.Items.Add(New ComboBoxItem With {.Content = labels(i), .Tag = values(i)})
        Next
        box.SelectedIndex = 0
    End Sub
    Private Function FilterValue(box As ComboBox) As String
        Dim item = TryCast(box.SelectedItem, ComboBoxItem)
        Return If(item Is Nothing, "", CStr(item.Tag))
    End Function
    Private Function CurrentWeaponFilter() As WeaponFilter
        Dim filter As New WeaponFilter With {.Search = BoxWeaponSearch.Text.Trim(), .DamageType = FilterValue(FilterDamage), .Source = FilterValue(FilterSource), .Size = FilterValue(FilterSize), .Type = FilterValue(FilterType), .Feature = FilterValue(FilterFeature)}
        Dim number As Double
        If FilterMaxOp.Text.Trim() <> "" Then
            filter.MaxOp = If(Double.TryParse(FilterMaxOp.Text, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, number) AndAlso number >= 0 AndAlso Not Double.IsNaN(number) AndAlso Not Double.IsInfinity(number), number, -1)
        End If
        If FilterMinRange.Text.Trim() <> "" Then
            filter.MinRange = If(Double.TryParse(FilterMinRange.Text, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, number) AndAlso number >= 0 AndAlso Not Double.IsNaN(number) AndAlso Not Double.IsInfinity(number), number, Double.PositiveInfinity)
        End If
        Return filter
    End Function
    Private Sub FiltersChanged(sender As Object, e As SelectionChangedEventArgs) Handles FilterDamage.SelectionChanged, FilterSize.SelectionChanged, FilterType.SelectionChanged, FilterSource.SelectionChanged, FilterFeature.SelectionChanged
        ApplyWeaponFilters()
    End Sub
    Private Sub FilterNumbersChanged(sender As Object, e As TextChangedEventArgs) Handles FilterMaxOp.TextChanged, FilterMinRange.TextChanged
        ApplyWeaponFilters()
    End Sub
    Private Sub ApplyWeaponFilters()
        If _filterSync OrElse _catalog Is Nothing Then Return
        CloseWeaponDetails()
        FilterWeapons()
        RefreshComparisonCandidates()
    End Sub
    Private Sub ResetFilters(sender As Object, e As RoutedEventArgs) Handles BtnResetFilters.Click
        _filterSync = True
        For Each box In {FilterDamage, FilterSize, FilterType, FilterSource, FilterFeature}
            box.SelectedIndex = 0
        Next
        FilterMaxOp.Clear() : FilterMinRange.Clear() : BoxWeaponSearch.Clear()
        _filterSync = False
        ApplyWeaponFilters()
    End Sub
    Private Sub RefreshInspectionSources()
        _filterSync = True
        Dim sources = _catalog.Weapons.Select(Function(w) w.Source).Distinct().OrderBy(Function(s) s).ToArray()
        FillFilter(FilterSource, {"全部来源"}.Concat(sources).ToArray(), {""}.Concat(sources).ToArray())
        _filterSync = False
        If _compareLeft IsNot Nothing Then _compareLeft = _catalog.Weapons.FirstOrDefault(Function(w) w.Id = _compareLeft.Id)
        If _compareRight IsNot Nothing Then _compareRight = _catalog.Weapons.FirstOrDefault(Function(w) w.Id = _compareRight.Id)
        RefreshComparisonCandidates()
    End Sub
    Private Sub RefreshComparisonCandidates()
        Dim filter = CurrentWeaponFilter()
        If BoxCompareSearch.Text.Trim() <> "" Then filter.Search = BoxCompareSearch.Text.Trim()
        Dim candidates = _catalog.Weapons.Where(Function(w) filter.Matches(w)).ToList()
        For Each selected In {_compareLeft, _compareRight}
            If selected IsNot Nothing AndAlso Not candidates.Contains(selected) Then candidates.Add(selected)
        Next
        candidates = candidates.OrderBy(Function(w) w.Name).ToList()
        _compareSync = True
        CompareA.ItemsSource = candidates : CompareB.ItemsSource = candidates
        CompareA.SelectedItem = _compareLeft : CompareB.SelectedItem = _compareRight
        _compareSync = False
        RefreshComparison()
    End Sub
    Private Sub CompareSearchChanged(sender As Object, e As TextChangedEventArgs) Handles BoxCompareSearch.TextChanged
        If _catalog IsNot Nothing Then RefreshComparisonCandidates()
    End Sub
    Private Sub CompareSelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles CompareA.SelectionChanged, CompareB.SelectionChanged
        If _compareSync Then Return
        _compareLeft = TryCast(CompareA.SelectedItem, WeaponDefinition)
        _compareRight = TryCast(CompareB.SelectedItem, WeaponDefinition)
        RefreshComparison()
    End Sub
    Private Sub AddCompareA(sender As Object, e As RoutedEventArgs) Handles BtnCompareA.Click
        Dim weapon = TryCast(WeaponList.SelectedItem, WeaponDefinition)
        If weapon Is Nothing Then Return
        CloseWeaponDetails()
        _compareLeft = weapon : RefreshComparisonCandidates() : DetailTabs.SelectedIndex = 1
    End Sub
    Private Sub AddCompareB(sender As Object, e As RoutedEventArgs) Handles BtnCompareB.Click
        Dim weapon = TryCast(WeaponList.SelectedItem, WeaponDefinition)
        If weapon Is Nothing Then Return
        CloseWeaponDetails()
        _compareRight = weapon : RefreshComparisonCandidates() : DetailTabs.SelectedIndex = 1
    End Sub
    Private Sub SwapComparison(sender As Object, e As RoutedEventArgs) Handles BtnCompareSwap.Click
        Dim previous = _compareLeft : _compareLeft = _compareRight : _compareRight = previous
        RefreshComparisonCandidates()
    End Sub
    Private Sub ClearComparison(sender As Object, e As RoutedEventArgs) Handles BtnCompareClear.Click
        _compareLeft = Nothing : _compareRight = Nothing : RefreshComparisonCandidates()
    End Sub
    Private Sub RefreshComparison()
        CompareRows.ItemsSource = LoadoutInspection.Compare(_compareLeft, _compareRight)
        Dim status = Function(w As WeaponDefinition) If(w Is Nothing, "未选择", w.Name & If(_slot Is Nothing, "", If(LoadoutRules.Fits(_slot, w), " · 适配当前槽位", " · 不适配当前槽位")))
        LabCompareFit.Text = "A：" & status(_compareLeft) & vbCrLf & "B：" & status(_compareRight) & vbCrLf & "类型加权理论值；B−A 为差值。筛选也作用于候选，已选武器保留。"
        Dim describe = Function(w As WeaponDefinition) If(w Is Nothing, "未选择", w.Name & " · " & w.Id & vbCrLf & w.Description & vbCrLf & String.Join(vbCrLf, LoadoutDetails.Parameters(w.Stats).Select(Function(p) p.Name & "：" & p.Value)))
        BoxCompareDescriptions.Text = "A" & vbCrLf & describe(_compareLeft) & vbCrLf & vbCrLf & "B" & vbCrLf & describe(_compareRight)
    End Sub
    Private Sub WeaponHovered(sender As Object, e As MouseEventArgs)
        Dim row = TryCast(sender, ListBoxItem)
        If row Is Nothing Then Return
        BeginWeaponHover(TryCast(row.DataContext, WeaponDefinition), row)
    End Sub
    Private Sub BeginWeaponHover(weapon As WeaponDefinition, target As FrameworkElement)
        If weapon Is Nothing Then Return
        _hoverClose.Stop() : _hoverDelay.Stop()
        _hoverWeapon = weapon : _hoverTarget = target
        _hoverDelay.Start()
    End Sub
    Private Sub WeaponLeft(sender As Object, e As MouseEventArgs)
        _hoverDelay.Stop() : _hoverClose.Start()
    End Sub
    Private Sub WeaponRightClicked(sender As Object, e As MouseButtonEventArgs)
        Dim row = TryCast(sender, ListBoxItem)
        If row IsNot Nothing Then row.IsSelected = True
    End Sub
    Private Sub ShowWeaponPopup(weapon As WeaponDefinition, target As FrameworkElement)
        BindWeaponDetails(weapon)
        _hoverTarget = target
        WeaponPopup.PlacementTarget = target
        WeaponPopupFrame.MaxHeight = Math.Max(200, Math.Min(620, SystemParameters.WorkArea.Height - 80))
        WeaponPopup.IsOpen = True
    End Sub
    Private Sub PopupEntered(sender As Object, e As MouseEventArgs)
        _hoverClose.Stop()
    End Sub
    Private Sub PopupLeft(sender As Object, e As MouseEventArgs)
        _hoverClose.Start()
    End Sub
    Private Sub CloseWeaponDetails()
        _hoverDelay.Stop() : _hoverClose.Stop() : WeaponPopup.IsOpen = False
        _hoverTarget = Nothing : _hoverWeapon = Nothing
    End Sub
    Private Sub CloseWeaponPopup(sender As Object, e As RoutedEventArgs) Handles BtnCloseWeaponPopup.Click
        CloseWeaponDetails()
    End Sub
    Private Sub InspectionTabChanged(sender As Object, e As SelectionChangedEventArgs) Handles DetailTabs.SelectionChanged
        If Object.ReferenceEquals(e.OriginalSource, DetailTabs) Then CloseWeaponDetails()
    End Sub
    Private Sub EscapeWeaponPopup(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown
        If e.Key = Key.Escape AndAlso WeaponPopup.IsOpen Then CloseWeaponDetails() : e.Handled = True
    End Sub
    Private Sub SkillPreviewChanged(sender As Object, e As RoutedEventArgs) Handles CheckDerelict.Checked, CheckDerelict.Unchecked
        If _hull IsNot Nothing Then RefreshDeployment()
    End Sub
    Private Sub RefreshDeployment()
        Dim count = LoadoutInspection.DModCount(_hull, _plan, _catalog.HullMods)
        Dim original = LoadoutInspection.BaseDeployment(_hull)
        Dim current = LoadoutInspection.Deployment(_hull, _plan, _catalog.HullMods, CheckDerelict.IsChecked = True)
        LabDeployment.Text = "部署 " & LoadoutDetails.Ratio(current) & " DP · 原始 " & LoadoutDetails.Ratio(original) & " · D 插 " & count & If(CheckDerelict.IsChecked = True, "（技能预览）", "（无技能减免）")
        LabDeployment.ToolTip = "基础值来自 supplies/rec。废船行动每项 D 插减免6%，最多5项。不模拟其他技能或 MOD 脚本；该开关不写入 variant 或角色存档。"
    End Sub
End Class
