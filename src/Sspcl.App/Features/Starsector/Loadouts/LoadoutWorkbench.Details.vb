Imports Sspcl.Core.Loadouts
Imports System.Windows.Controls.Primitives

Public Partial Class LoadoutWorkbench
    Private _sidebarWidth As Double = 225

    Private Sub ToggleHulls(sender As Object, e As RoutedEventArgs) Handles BtnToggleHulls.Click
        SetHullSidebar(HullSidebar.Visibility <> Visibility.Visible)
    End Sub
    Private Sub SetHullSidebar(show As Boolean)
        If show Then
            HullColumn.MinWidth = 170
            HullColumn.Width = New GridLength(Math.Max(170, Math.Min(420, _sidebarWidth)))
            HullSplitColumn.Width = New GridLength(12)
            HullSidebar.Visibility = Visibility.Visible
            HullSplitter.Visibility = Visibility.Visible
            BtnToggleHulls.Content = "收起目录"
        Else
            If HullColumn.ActualWidth > 175 Then _sidebarWidth = HullColumn.ActualWidth
            HullColumn.MinWidth = 0
            HullColumn.Width = New GridLength(0)
            HullSplitColumn.Width = New GridLength(0)
            HullSidebar.Visibility = Visibility.Collapsed
            HullSplitter.Visibility = Visibility.Collapsed
            BtnToggleHulls.Content = "展开目录"
        End If
    End Sub
    Private Sub HullDragCompleted(sender As Object, e As DragCompletedEventArgs) Handles HullSplitter.DragCompleted
        If HullColumn.ActualWidth <= 175 Then
            SetHullSidebar(False)
        Else
            _sidebarWidth = HullColumn.ActualWidth
        End If
    End Sub

    Private Sub RefreshDamageAndDetails()
        Dim damage = LoadoutDetails.Damage(_hull, _catalog.Weapons, _plan)
        LabShieldDps.Text = LoadoutDetails.Number(damage.ShieldDps)
        LabArmorDps.Text = LoadoutDetails.Number(damage.ArmorDps)
        LabHullDps.Text = LoadoutDetails.Number(damage.HullDps)
        LabDamageBasis.Text = "非导弹连射火力（含内置）· 幅伤比 " & LoadoutDetails.Ratio(damage.FluxPerDamage) & " 幅能/伤害"
        LabDamageBasis.ToolTip = "按游戏伤害类型倍率加权；不含目标盾效、装甲减伤、命中率和技能/船插脚本。DPS 来自表值或单发、射速估算；有限弹药为耗尽前连射值。"
        If damage.UnknownDamageWeapons + damage.MissingWeapons > 0 Then LabDamageBasis.Text &= " · " & (damage.UnknownDamageWeapons + damage.MissingWeapons) & " 项数据未知未计入"
        If damage.EstimatedWeapons > 0 Then LabDamageBasis.Text &= " · " & damage.EstimatedWeapons & " 项按射速估算"
        LabHullStats.Text = "基础舰船数据" & vbCrLf & LoadoutDetails.HullOverview(_hull)
        RefreshInstalledHullMods()
    End Sub
    Private Sub BindHullDetails()
        HullParameterList.ItemsSource = LoadoutDetails.Parameters(_hull.Stats)
        LabHullDescription.Text = If(_hull.Description = "", "数据源未提供舰船描述。", _hull.Description)
        BoxHullSpec.Text = PrettySpec(_hull.RawSpec)
        FilterHullMods()
    End Sub
    Private Sub BindWeaponDetails(weapon As WeaponDefinition)
        If weapon Is Nothing Then Return
        LabWeaponTitle.Text = weapon.Name
        LabWeaponOverview.Text = LoadoutDetails.WeaponOverview(weapon)
        BoxWeaponDescription.Text = If(weapon.Description = "", "数据源未提供描述。", weapon.Description)
        WeaponParameterList.ItemsSource = LoadoutDetails.Parameters(weapon.Stats)
        BoxWeaponSpec.Text = PrettySpec(weapon.RawSpec)
        Try
            BoxProjectileSpec.Text = If(IO.File.Exists(weapon.ProjectileSpecPath), IO.File.ReadAllText(weapon.ProjectileSpecPath), "数据源未提供独立弹体规格（光束等武器可直接使用武器规格）。")
        Catch ex As Exception
            BoxProjectileSpec.Text = "弹体规格读取失败：" & ex.Message
        End Try
    End Sub
    Private Function PrettySpec(raw As String) As String
        Try
            Return Newtonsoft.Json.Linq.JToken.Parse(raw).ToString(Newtonsoft.Json.Formatting.Indented)
        Catch
            Return raw
        End Try
    End Function

    Private Sub HullModSearchChanged(sender As Object, e As TextChangedEventArgs) Handles BoxHullModSearch.TextChanged
        If _catalog IsNot Nothing Then FilterHullMods()
    End Sub
    Private Sub FilterHullMods()
        Dim selected = TryCast(HullModList.SelectedItem, HullModDefinition)
        Dim search = BoxHullModSearch.Text.Trim()
        HullModList.ItemsSource = _catalog.HullMods.Where(Function(m) (m.Name & " " & m.Id & " " & m.Source).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
        If selected IsNot Nothing AndAlso HullModList.Items.Contains(selected) Then HullModList.SelectedItem = selected
        UpdateHullModActions()
    End Sub
    Private Sub HullModSelected(sender As Object, e As SelectionChangedEventArgs) Handles HullModList.SelectionChanged
        Dim selected = TryCast(HullModList.SelectedItem, HullModDefinition)
        If selected IsNot Nothing AndAlso _hull IsNot Nothing Then
            LabHullModDescription.Text = selected.Name & " · 普通 " & selected.Cost(_hull.HullSize) & " OP · 内置 0 OP" & vbCrLf & selected.Description & vbCrLf & "S 插说明：" & If(selected.SModDescription = "", "数据源未提供。", selected.SModDescription)
            If LabHullModDescription.Text.Contains("{%s}") Then LabHullModDescription.Text &= vbCrLf & "{%s} 为游戏脚本动态参数，保留原模板。"
        End If
        UpdateHullModActions()
    End Sub
    Private Sub InstalledHullModSelected(sender As Object, e As SelectionChangedEventArgs) Handles InstalledHullModList.SelectionChanged
        UpdateHullModActions()
    End Sub
    Private Sub UpdateHullModActions()
        Dim selected = TryCast(HullModList.SelectedItem, HullModDefinition)
        BtnHullModNormal.IsEnabled = selected IsNot Nothing AndAlso _hull IsNot Nothing AndAlso Not _hull.BuiltInHullMods.Contains(selected.Id)
        BtnHullModBuiltIn.IsEnabled = selected IsNot Nothing AndAlso _hull IsNot Nothing
        Dim installed = TryCast(InstalledHullModList.SelectedItem, ListBoxItem)
        BtnHullModRemove.IsEnabled = installed IsNot Nothing AndAlso _hull IsNot Nothing AndAlso (Not _hull.BuiltInHullMods.Contains(CStr(installed.Tag)) OrElse _plan.SModdedBuiltIns.Contains(CStr(installed.Tag)))
    End Sub
    Private Sub RefreshInstalledHullMods()
        Dim selected = TryCast(InstalledHullModList.SelectedItem, ListBoxItem)
        Dim id = If(selected Is Nothing, "", CStr(selected.Tag))
        InstalledHullModList.Items.Clear()
        For Each modId In _hull.BuiltInHullMods.Concat(_plan.HullMods).Concat(_plan.PermaMods).Distinct().OrderBy(Function(s) s)
            Dim definition = _catalog.HullMods.FirstOrDefault(Function(m) m.Id = modId)
            Dim native = _hull.BuiltInHullMods.Contains(modId)
            Dim mode = If(native, If(_plan.SModdedBuiltIns.Contains(modId), "船体原生 · S 强化", "船体原生"), If(_plan.PermaMods.Contains(modId), If(_plan.SMods.Contains(modId), "内置/S 插", "永久内置"), "普通"))
            Dim row As New ListBoxItem With {.Tag = modId, .Content = "[" & mode & "] " & If(definition Is Nothing, modId, definition.Name), .ToolTip = modId}
            InstalledHullModList.Items.Add(row)
            If modId = id Then InstalledHullModList.SelectedItem = row
        Next
        LabHullModCount.Text = "普通 " & _plan.HullMods.Count & " · 新增内置 " & _plan.PermaMods.Count & "（无上限）· 原生 " & _hull.BuiltInHullMods.Count & vbCrLf & "原生船插由船体定义保留，可添加或移除其 S 强化。"
        UpdateHullModActions()
    End Sub
    Private Sub InstallHullModNormal(sender As Object, e As RoutedEventArgs) Handles BtnHullModNormal.Click
        ChangeHullMod(False)
    End Sub
    Private Sub InstallHullModBuiltIn(sender As Object, e As RoutedEventArgs) Handles BtnHullModBuiltIn.Click
        ChangeHullMod(True)
    End Sub
    Private Sub ChangeHullMod(builtIn As Boolean)
        Dim selected = TryCast(HullModList.SelectedItem, HullModDefinition)
        If selected Is Nothing OrElse _hull Is Nothing Then Return
        Dim proposal = LoadoutRules.Copy(_plan)
        If _hull.BuiltInHullMods.Contains(selected.Id) Then
            If Not builtIn Then Return
            proposal.SModdedBuiltIns.Add(selected.Id)
        Else
            proposal.HullMods.Remove(selected.Id) : proposal.PermaMods.Remove(selected.Id) : proposal.SMods.Remove(selected.Id)
            If builtIn Then
                proposal.PermaMods.Add(selected.Id) : proposal.SMods.Add(selected.Id)
            Else
                proposal.HullMods.Add(selected.Id)
            End If
        End If
        Dim evaluation = LoadoutRules.Evaluate(_hull, _catalog.Weapons, proposal, _catalog.HullMods)
        If Not evaluation.Valid Then
            LabStatus.Text = String.Join("；", evaluation.Errors) : Return
        End If
        Remember() : _plan = proposal
        RefreshPlan()
    End Sub
    Private Sub RemoveHullMod(sender As Object, e As RoutedEventArgs) Handles BtnHullModRemove.Click
        Dim selected = TryCast(InstalledHullModList.SelectedItem, ListBoxItem)
        If selected Is Nothing Then Return
        Dim id = CStr(selected.Tag)
        If _hull.BuiltInHullMods.Contains(id) AndAlso Not _plan.SModdedBuiltIns.Contains(id) Then Return
        Remember()
        _plan.HullMods.Remove(id) : _plan.PermaMods.Remove(id) : _plan.SMods.Remove(id) : _plan.SModdedBuiltIns.Remove(id)
        RefreshPlan()
    End Sub
End Class
