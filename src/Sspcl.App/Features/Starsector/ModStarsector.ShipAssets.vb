Imports System.Diagnostics
Imports System.Text.RegularExpressions
Imports Sspcl.Core.Install
Imports Sspcl.Core.Launch
Imports Sspcl.Core.Mods
Imports Sspcl.Core.Saves
Imports Sspcl.Core.Store


Partial Module ModStarsector

    Class ShipSlot
        Public Type As String
        Public Size As String
        Public Mount As String
        Public X As Double
        Public Y As Double
        Public Angle As Double
    End Class

    Class WeaponInfo
        Public Id As String
        Public Name As String
        Public DamageType As String
        Public OPs As Integer
        Public Range As Integer
        Public DPS As Double
        Public Size As String
        Public TurretSprite As String
        Public HardpointSprite As String
        Public ReadOnly Property Display As String
            Get
                Return Name & "（" & Id & "） " & OPs & " OP"
            End Get
        End Property
    End Class

    Class WeaponSpec
        Public Size As String
        Public TurretSprite As String
        Public HardpointSprite As String
    End Class

    ''' <summary>去掉 JSONC 的 # 注释与尾逗号。</summary>
    Function StripJsonc(text As String) As String
        Dim sb As New Text.StringBuilder()
        For Each raw In text.Split(New String() {vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
            Dim line = raw
            Dim idx = line.IndexOf("#")
            If idx >= 0 Then line = line.Substring(0, idx)
            sb.AppendLine(line)
        Next
        Return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), ",\s*([\]}])", "$1")
    End Function

    ''' <summary>解析所有武器规格（size/turretSprite/hardpointSprite），来自 .wpn 文件。</summary>
    Function ParseWeaponSpecs(installPath As String) As Dictionary(Of String, WeaponSpec)
        If _weaponSpecsCache.ContainsKey(installPath) Then Return _weaponSpecsCache(installPath)
        Dim dict = New Dictionary(Of String, WeaponSpec)(StringComparer.OrdinalIgnoreCase)
        Dim roots As New List(Of String) From {IO.Path.Combine(installPath, "starsector-core\data\weapons")}
        For Each m In GetEnabledModDirs(installPath)
            roots.Add(IO.Path.Combine(m, "data\weapons"))
        Next
        For Each r In roots
            If Not IO.Directory.Exists(r) Then Continue For
            For Each f In IO.Directory.GetFiles(r, "*.wpn")
                Try
                    Dim obj = Newtonsoft.Json.Linq.JObject.Parse(StripJsonc(IO.File.ReadAllText(f)))
                    Dim id = If(obj("id") IsNot Nothing, obj("id").ToString(), "")
                    If id = "" Then Continue For
                    Dim spec As New WeaponSpec()
                    If obj("size") IsNot Nothing Then spec.Size = obj("size").ToString()
                    If obj("turretSprite") IsNot Nothing Then spec.TurretSprite = obj("turretSprite").ToString().Replace("/", "\")
                    If obj("hardpointSprite") IsNot Nothing Then spec.HardpointSprite = obj("hardpointSprite").ToString().Replace("/", "\")
                    dict(id) = spec
                Catch
                End Try
            Next
        Next
        _weaponSpecsCache(installPath) = dict
        Return dict
    End Function

    ''' <summary>找到舰船贴图（优先用 .ship 的 spriteName，兜底 graphics/ships/&lt;id&gt;/&lt;id&gt;_base.png）。</summary>
    Function FindShipSprite(installPath As String, hullId As String) As String
        Dim spriteName = ""
        Dim sf = FindShipFile(installPath, hullId)
        If sf <> "" Then
            Try
                Dim obj = Newtonsoft.Json.Linq.JObject.Parse(IO.File.ReadAllText(sf))
                If obj("spriteName") IsNot Nothing Then spriteName = obj("spriteName").ToString().Replace("/", "\")
            Catch
            End Try
        End If
        Dim candidates As New List(Of String)()
        If spriteName <> "" Then candidates.Add(spriteName)
        candidates.Add("graphics\ships\" & hullId & "\" & hullId & "_base.png")
        candidates.Add("graphics\ships\" & hullId & "\" & hullId & ".png")
        Dim roots As New List(Of String) From {IO.Path.Combine(installPath, "starsector-core")}
        roots.AddRange(GetEnabledModDirs(installPath))
        For Each r In roots
            For Each c In candidates
                Dim p = IO.Path.Combine(r, c)
                If IO.File.Exists(p) Then Return p
            Next
        Next
        Return ""
    End Function

    ''' <summary>读取舰船视图信息。注意：center 字段为 [Y_from_bottom, X_from_left]（cx=center[0]=Y距底，cy=center[1]=X距左）。</summary>
    Function GetShipViewInfo(installPath As String, hullId As String) As (cx As Double, cy As Double, w As Double, h As Double)
        Dim f = FindShipFile(installPath, hullId)
        If f = "" Then Return (0, 0, 0, 0)
        Try
            Dim obj = Newtonsoft.Json.Linq.JObject.Parse(IO.File.ReadAllText(f))
            Dim c = obj("center")
            Dim cx As Double = 0, cy As Double = 0
            If c IsNot Nothing AndAlso c.Count >= 2 Then
                Double.TryParse(c(0).ToString(), cx)
                Double.TryParse(c(1).ToString(), cy)
            End If
            Dim w As Double = 0, h As Double = 0
            If obj("width") IsNot Nothing Then Double.TryParse(obj("width").ToString(), w)
            If obj("height") IsNot Nothing Then Double.TryParse(obj("height").ToString(), h)
            Return (cx, cy, w, h)
        Catch
            Return (0, 0, 0, 0)
        End Try
    End Function

    Private _weaponSpecsCache As New Dictionary(Of String, Dictionary(Of String, WeaponSpec))(StringComparer.OrdinalIgnoreCase)
    Private _weaponsCache As New Dictionary(Of String, List(Of WeaponInfo))(StringComparer.OrdinalIgnoreCase)
    Private _shipsCache As New Dictionary(Of String, List(Of ShipEntry))(StringComparer.OrdinalIgnoreCase)
    Private _enabledModDirsCache As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)

    ''' <summary>按 hull id 找到 .ship 变体文件路径。</summary>
    Function FindShipFile(installPath As String, hullId As String) As String
        Dim roots As New List(Of String) From {IO.Path.Combine(installPath, "starsector-core\data\hulls")}
        For Each m In GetEnabledModDirs(installPath)
            roots.Add(IO.Path.Combine(m, "data\hulls"))
        Next
        For Each r In roots
            Dim f = IO.Path.Combine(r, hullId & ".ship")
            If IO.File.Exists(f) Then Return f
        Next
        Return ""
    End Function

    ''' <summary>解析舰船的武器槽列表（type/size/mount）。</summary>
    Function ParseShipSlots(installPath As String, hullId As String) As List(Of ShipSlot)
        Dim list = New List(Of ShipSlot)()
        Dim f = FindShipFile(installPath, hullId)
        If f = "" Then Return list
        Try
            Dim obj = Newtonsoft.Json.Linq.JObject.Parse(IO.File.ReadAllText(f))
            Dim arr = obj("weaponSlots")
            If arr IsNot Nothing Then
                For Each s In arr
                    Dim x As Double = 0, y As Double = 0
                    Dim loc = s("locations")
                    If loc IsNot Nothing AndAlso loc.Count >= 2 Then
                        Double.TryParse(loc(0).ToString(), x)
                        Double.TryParse(loc(1).ToString(), y)
                    End If
                    Dim angle As Double = 0
                    If s("angle") IsNot Nothing Then Double.TryParse(s("angle").ToString(), angle)
                    list.Add(New ShipSlot With {
                        .Type = If(s("type") IsNot Nothing, s("type").ToString(), ""),
                        .Size = If(s("size") IsNot Nothing, s("size").ToString(), ""),
                        .Mount = If(s("mount") IsNot Nothing, s("mount").ToString(), ""),
                        .X = x,
                        .Y = y,
                        .Angle = angle
                    })
                Next
            End If
        Catch
        End Try
        Return list
    End Function

    ''' <summary>解析所有武器（id/名称/伤害类型/OP/射程/DPS）。</summary>
    Function ParseWeapons(installPath As String) As List(Of WeaponInfo)
        If _weaponsCache.ContainsKey(installPath) Then Return _weaponsCache(installPath)
        Dim list = New List(Of WeaponInfo)()
        Dim roots As New List(Of String) From {IO.Path.Combine(installPath, "starsector-core\data\weapons")}
        For Each m In GetEnabledModDirs(installPath)
            roots.Add(IO.Path.Combine(m, "data\weapons"))
        Next
        Dim seen = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim specs = ParseWeaponSpecs(installPath)
        For Each r In roots
            Dim csv = IO.Path.Combine(r, "weapon_data.csv")
            If Not IO.File.Exists(csv) Then Continue For
            For Each line In IO.File.ReadAllLines(csv)
                Dim t = line.Trim()
                If t = "" OrElse t.StartsWith("#") Then Continue For
                Dim cols = t.Split(","c)
                If cols.Length < 16 Then Continue For
                Dim name = cols(0).Trim()
                Dim id = cols(1).Trim()
                If name = "name" OrElse id = "id" Then Continue For
                Dim dps As Double = 0
                Double.TryParse(cols(6).Trim(), dps)
                Dim ops As Integer = 0
                Integer.TryParse(cols(11).Trim(), ops)
                Dim range As Integer = 0
                Integer.TryParse(cols(5).Trim(), range)
                If id = "" OrElse name = "" OrElse ops <= 0 Then Continue For
                If Not seen.Add(id) Then Continue For
                Dim spec As WeaponSpec = Nothing
                If specs IsNot Nothing Then specs.TryGetValue(id, spec)
                list.Add(New WeaponInfo With {
                    .Id = id,
                    .Name = name,
                    .DamageType = cols(15).Trim(),
                    .OPs = ops,
                    .Range = range,
                    .DPS = dps,
                    .Size = If(spec IsNot Nothing, spec.Size, ""),
                    .TurretSprite = If(spec IsNot Nothing, spec.TurretSprite, ""),
                    .HardpointSprite = If(spec IsNot Nothing, spec.HardpointSprite, "")
                })
            Next
        Next
        _weaponsCache(installPath) = list
        Return list
    End Function

    Private Function WeaponSize(ops As Integer) As String
        If ops >= 14 Then Return "LARGE"
        If ops >= 7 Then Return "MEDIUM"
        Return "SMALL"
    End Function

    Private Function DamageToMount(damageType As String) As String
        Select Case damageType.ToUpper()
            Case "KINETIC", "HIGH_EXPLOSIVE", "FRAGMENTATION" : Return "BALLISTIC"
            Case "ENERGY" : Return "ENERGY"
            Case "MISSILE" : Return "MISSILE"
            Case Else : Return ""
        End Select
    End Function

    Private Function SlotAccepts(slotType As String, mount As String) As Boolean
        If slotType = "UNIVERSAL" OrElse slotType = "HYBRID" OrElse slotType = "SYNERGY" OrElse slotType = "COMPOSITE" Then Return True
        Return slotType = mount
    End Function

    ''' <summary>获取与槽位兼容的武器列表（类型 + 尺寸匹配，尺寸优先用 .wpn 精确值）。</summary>
    Function GetCompatibleWeapons(slot As ShipSlot, weapons As List(Of WeaponInfo)) As List(Of WeaponInfo)
        Return weapons.Where(Function(w) WeaponMatchSize(w, slot.Size) AndAlso SlotAccepts(slot.Type, DamageToMount(w.DamageType))).ToList()
    End Function

    Private Function WeaponMatchSize(w As WeaponInfo, slotSize As String) As Boolean
        If w.Size <> "" Then Return w.Size = slotSize
        Return WeaponSize(w.OPs) = slotSize
    End Function

    ' ===== 对照汉化 =====
    Private Function TSlot(t As String) As String
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

    Private Function TSize(s As String) As String
        Select Case s
            Case "SMALL" : Return "小型"
            Case "MEDIUM" : Return "中型"
            Case "LARGE" : Return "大型"
            Case Else : Return s
        End Select
    End Function

    Private Function TMount(m As String) As String
        Select Case m
            Case "TURRET" : Return "炮塔"
            Case "HARDPOINT" : Return "硬点"
            Case Else : Return m
        End Select
    End Function

    Private Function TDamage(d As String) As String
        Select Case d
            Case "KINETIC" : Return "动能"
            Case "ENERGY" : Return "能量"
            Case "MISSILE" : Return "导弹"
            Case "HIGH_EXPLOSIVE" : Return "高爆"
            Case "FRAGMENTATION" : Return "破片"
            Case Else : Return d
        End Select
    End Function

    Private Function TDesignation(d As String) As String
        Select Case d.ToLower()
            Case "frigate" : Return "护卫舰"
            Case "destroyer" : Return "驱逐舰"
            Case "cruiser" : Return "巡洋舰"
            Case "capital" : Return "主力舰"
            Case "carrier" : Return "航母"
            Case Else : Return d
        End Select
    End Function

End Module
