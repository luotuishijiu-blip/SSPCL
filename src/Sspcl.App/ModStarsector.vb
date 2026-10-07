' =====================================================================
' 远行星号（Starsector）功能封装 —— 桥接 sspcl.Core 与 PCL2
' 本模块把 sspcl.Core 的 .NET Standard 2.0 程序集封装成 VB.NET 函数，
' 供 PCL2 的界面直接调用。
' =====================================================================

Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Text.RegularExpressions
Imports Sspcl.Core.Install
Imports Sspcl.Core.Launch
Imports Sspcl.Core.Mods
Imports Sspcl.Core.Saves
Imports Sspcl.Core.Store

Module ModStarsector

    ''' <summary>检测远行星号安装目录（版本/汉化/是否有效）。</summary>
    Function Detect(installPath As String) As Installation
        Return InstallationDetector.Detect(installPath)
    End Function

    ''' <summary>mod 目录路径。</summary>
    Function ModsDir(installPath As String) As String
        Return IO.Path.Combine(installPath, "mods")
    End Function

    ''' <summary>扫描 mod 目录，返回所有 mod。</summary>
    Function ScanMods(modsDir As String) As IReadOnlyList(Of ModSpec)
        Return ModScanner.Scan(modsDir)
    End Function

    ''' <summary>读取已启用 mod id 列表。</summary>
    Function ReadEnabled(installPath As String) As List(Of String)
        Return EnabledModsFile.Read(installPath)
    End Function

    ''' <summary>写入已启用 mod id 列表（原子写 + 自动备份）。</summary>
    Sub WriteEnabled(installPath As String, ids As IEnumerable(Of String))
        EnabledModsFile.Write(installPath, ids)
    End Sub

    ''' <summary>计算启用闭包（自动补齐前置依赖）。</summary>
    Function Resolve(ids As IEnumerable(Of String), mods As IReadOnlyList(Of ModSpec)) As ISet(Of String)
        Return DependencyResolver.Closure(ids, mods)
    End Function

    ''' <summary>读取 vmparams 里的内存设置（MB）。</summary>
    Function ReadMemory(vmparamsText As String) As (XmxMb As Integer, XmsMb As Integer)
        Return Launcher.ReadMemoryMb(vmparamsText)
    End Function

    ''' <summary>直接按安装目录读取内存设置（MB）。</summary>
    Function ReadMemoryOf(installPath As String) As (XmxMb As Integer, XmsMb As Integer)
        Return Launcher.ReadMemoryMb(IO.File.ReadAllText(Launcher.VmparamsPath(installPath)))
    End Function

    ''' <summary>启动游戏（skipLauncher=True 跳过自带启动器直接进主菜单）。</summary>
    Function Launch(installPath As String, xmxMb As Integer, xmsMb As Integer, skipLauncher As Boolean,
                    Optional resolution As String = "1920x1080",
                    Optional priority As ProcessPriorityClass = ProcessPriorityClass.Normal,
                    Optional vmArgs As String = "") As Process
        ' 热启动优化：AppCDS（类数据共享）。Java 17 不支持 AutoCreateSharedArchive，改用两段式：
        ' 首次启动用 ArchiveClassesAtExit 在退出时生成归档；之后用 SharedArchiveFile 加载归档加速类加载。
        If Settings.Get(Of Boolean)("StarsectorWarmStart") Then
            Dim jsa = IO.Path.Combine(installPath, "starsector.jsa")
            If IO.File.Exists(jsa) Then
                vmArgs = "-XX:SharedArchiveFile=""" & jsa & """ " & vmArgs
            Else
                vmArgs = "-XX:ArchiveClassesAtExit=""" & jsa & """ " & vmArgs
            End If
        End If
        Dim extra As IReadOnlyList(Of String) = Nothing
        If Not String.IsNullOrWhiteSpace(vmArgs) Then extra = Launcher.Tokenize(vmArgs)
        Return Launcher.Launch(installPath, xmxMb, xmsMb, skipLauncher, resolution, False, True, priority, extra)
    End Function

    ''' <summary>扫描存档列表。</summary>
    Function ScanSaves(installPath As String) As IReadOnlyList(Of SaveInfo)
        Return SaveScanner.Scan(IO.Path.Combine(installPath, "saves"))
    End Function

    ''' <summary>安装一个 mod 压缩包（.zip/.7z/.rar）。</summary>
    Function InstallArchive(archivePath As String, modsDir As String) As InstallResult
        Return ModInstaller.InstallArchive(archivePath, modsDir)
    End Function

    ''' <summary>删除一个 mod 目录（进回收站）。</summary>
    Sub RecycleDir(path As String)
        ModInstaller.RecycleDir(path)
    End Sub

    ''' <summary>写入 vmparams 里的内存设置（MB）。</summary>
    Sub WriteMemory(installPath As String, xmxMb As Integer, xmsMb As Integer)
        Dim p = Launcher.VmparamsPath(installPath)
        Dim text = IO.File.ReadAllText(p)
        text = Regex.Replace(text, "-Xmx\d+[kKmMgG]?", "-Xmx" & xmxMb & "m")
        text = Regex.Replace(text, "-Xms\d+[kKmMgG]?", "-Xms" & xmsMb & "m")
        IO.File.WriteAllText(p, text)
    End Sub

    ''' <summary>拉取 Mod 商店索引。</summary>
    Function FetchStore() As Task(Of List(Of StoreItem))
        Return ModRepoClient.FetchAsync()
    End Function

    ''' <summary>扫描舰船名列表。</summary>
    Class ShipEntry
        Public Name As String
        Public Id As String
        Public Designation As String
        Public OP As Integer
        Public ReadOnly Property Display As String
            Get
                Return Name & "（" & Id & "）"
            End Get
        End Property
    End Class

    ''' <summary>读取当前启用的 mod 目录列表（依据 enabled_mods.json，无该文件则全部启用）。</summary>
    Function GetEnabledModDirs(installPath As String) As List(Of String)
        If _enabledModDirsCache.ContainsKey(installPath) Then Return _enabledModDirsCache(installPath)
        Dim list = New List(Of String)()
        Dim modsDir = IO.Path.Combine(installPath, "mods")
        If Not IO.Directory.Exists(modsDir) Then
            _enabledModDirsCache(installPath) = list
            Return list
        End If
        Dim enabledIds = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim en = IO.Path.Combine(modsDir, "enabled_mods.json")
        If IO.File.Exists(en) Then
            Try
                Dim obj = Newtonsoft.Json.Linq.JObject.Parse(IO.File.ReadAllText(en))
                Dim arr = obj("enabledMods")
                If arr IsNot Nothing Then
                    For Each s In arr
                        enabledIds.Add(s.ToString())
                    Next
                End If
            Catch
            End Try
        End If
        For Each m In IO.Directory.GetDirectories(modsDir)
            Dim mi = IO.Path.Combine(m, "mod_info.json")
            If Not IO.File.Exists(mi) Then Continue For
            Dim enabled = enabledIds.Count = 0
            If Not enabled Then
                Try
                    Dim obj = Newtonsoft.Json.Linq.JObject.Parse(IO.File.ReadAllText(mi))
                    Dim id = If(obj("id") IsNot Nothing, obj("id").ToString(), "")
                    If id <> "" AndAlso enabledIds.Contains(id) Then enabled = True
                Catch
                End Try
            End If
            If enabled Then list.Add(m)
        Next
        _enabledModDirsCache(installPath) = list
        Return list
    End Function

    ''' <summary>扫描舰船（原版 + 当前启用 mod 的 ship_data.csv，只保留有吨位分类的可玩舰船）。带缓存。</summary>
    Function ScanShips(installPath As String) As List(Of ShipEntry)
        If _shipsCache.ContainsKey(installPath) Then Return _shipsCache(installPath)
        Dim list = New List(Of ShipEntry)()
        Dim roots As New List(Of String) From {IO.Path.Combine(installPath, "starsector-core\data\hulls")}
        For Each m In GetEnabledModDirs(installPath)
            roots.Add(IO.Path.Combine(m, "data\hulls"))
        Next
        Dim seen = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each r In roots
            Dim csv = IO.Path.Combine(r, "ship_data.csv")
            If Not IO.File.Exists(csv) Then Continue For
            For Each line In IO.File.ReadAllLines(csv)
                Dim t = line.Trim()
                If t = "" OrElse t.StartsWith("#") Then Continue For
                Dim cols = t.Split(","c)
                If cols.Length < 12 Then Continue For
                Dim name = cols(0).Trim()
                Dim id = cols(1).Trim()
                Dim designation = cols(2).Trim()
                If id = "" OrElse designation = "" Then Continue For
                If name = "name" OrElse id = "id" Then Continue For
                If Not seen.Add(id) Then Continue For
                Dim op As Integer = 0
                Integer.TryParse(cols(11).Trim(), op)
                list.Add(New ShipEntry With {.Name = name, .Id = id, .Designation = designation, .OP = op})
            Next
        Next
        _shipsCache(installPath) = list
        Return list
    End Function

    ''' <summary>根据舰船吨位生成 AI 配装建议（启发式，中文）。</summary>
    Function GenerateLoadout(installPath As String, entry As ShipEntry) As String
        Dim des = entry.Designation.ToLower()
        Dim sb As New Text.StringBuilder()
        sb.AppendLine("【" & entry.Name & " " & entry.Id & "】" & TDesignation(entry.Designation))
        If des.Contains("carrier") Then
            sb.AppendLine("【定位】航母 —— 舰载机核心，点防护航")
            sb.AppendLine()
            sb.AppendLine("船体插件：")
            sb.AppendLine("· 扩展飞行甲板（加快舰载机整备）")
            sb.AppendLine("· 自动修复单元")
            sb.AppendLine("· 若带能量武器 → 先进光学组件")
            sb.AppendLine()
            sb.AppendLine("武器：")
            sb.AppendLine("· 小/中槽全点防（PD 激光 / 高射炮）")
            sb.AppendLine("· 舰载机：护航战斗机或轰炸机")
        ElseIf des.Contains("capital") Then
            sb.AppendLine("【定位】主力舰 —— 重火力 + 点防")
            sb.AppendLine()
            sb.AppendLine("船体插件：")
            sb.AppendLine("· 强化舱壁 / 重型装甲（抗线）")
            sb.AppendLine("· 自动修复单元")
            sb.AppendLine("· 先进目标定位（远程压制）")
            sb.AppendLine()
            sb.AppendLine("武器：")
            sb.AppendLine("· 大槽：远程重炮（高斯炮 / 自动炮）")
            sb.AppendLine("· 中槽：重型突击炮")
            sb.AppendLine("· 小槽：点防御")
        ElseIf des.Contains("cruiser") Then
            sb.AppendLine("【定位】巡洋舰 —— 中程均衡火力")
            sb.AppendLine()
            sb.AppendLine("船体插件：")
            sb.AppendLine("· 护盾 / 装甲增强（按护盾类型选）")
            sb.AppendLine("· 先进光学组件（若能量舰）")
            sb.AppendLine()
            sb.AppendLine("武器：")
            sb.AppendLine("· 中槽：中型突击炮 / 光束")
            sb.AppendLine("· 小槽：点防 + 轻型炮")
        ElseIf des.Contains("destroyer") Then
            sb.AppendLine("【定位】驱逐舰 —— 快速打击 / 护航")
            sb.AppendLine()
            sb.AppendLine("船体插件：")
            sb.AppendLine("· 机动增强（辅助推进器）")
            sb.AppendLine("· 点防扩展")
            sb.AppendLine()
            sb.AppendLine("武器：")
            sb.AppendLine("· 中槽：突击炮")
            sb.AppendLine("· 小槽：点防 / 轻型炮")
        ElseIf des.Contains("frigate") Then
            sb.AppendLine("【定位】护卫舰 —— 高机动，骚扰 / 点防")
            sb.AppendLine()
            sb.AppendLine("船体插件：")
            sb.AppendLine("· 辅助推进器（机动）")
            sb.AppendLine("· 强化护盾")
            sb.AppendLine()
            sb.AppendLine("武器：")
            sb.AppendLine("· 小槽：轻型炮 + 点防")
            sb.AppendLine("· 战术：侧舷机动 + 撤退")
        Else
            sb.AppendLine("【定位】未识别吨位（" & If(des = "", "无数据", des) & "）—— 通用均衡配置")
            sb.AppendLine()
            sb.AppendLine("船体插件：")
            sb.AppendLine("· 自动修复单元")
            sb.AppendLine("· 按护盾/装甲类型补强")
            sb.AppendLine()
            sb.AppendLine("武器：")
            sb.AppendLine("· 按槽位均衡配置（远程压制 + 点防）")
        End If
        Return sb.ToString().TrimEnd()
    End Function

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

    ''' <summary>找到舰船贴图（优先用 .ship 的 spriteName，兜底 graphics/ships/<id>/<id>_base.png）。</summary>
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

    ''' <summary>精确配装：武器按槽位（类型+尺寸）匹配，DPS/OP 优先，受 OP 上限约束。中文对照。overrides 为手动指定（槽位下标 → 武器）。</summary>
    Function GeneratePreciseLoadout(installPath As String, entry As ShipEntry, Optional manual As Dictionary(Of Integer, WeaponInfo) = Nothing) As String
        Dim slots = ParseShipSlots(installPath, entry.Id)
        Dim weapons = ParseWeapons(installPath)
        Dim sb As New Text.StringBuilder()
        sb.AppendLine("【" & entry.Name & " " & entry.Id & "】" & TDesignation(entry.Designation) & " · 武器槽 " & slots.Count & " 个 · 武器 " & weapons.Count & " 个")
        sb.AppendLine()
        Dim opUsed = 0
        For i = 0 To slots.Count - 1
            Dim slot = slots(i)
            Dim best As WeaponInfo = Nothing
            If manual IsNot Nothing AndAlso manual.ContainsKey(i) Then
                best = manual(i)
            Else
                Dim candidates = GetCompatibleWeapons(slot, weapons)
                best = candidates.Where(Function(w) w.OPs <= (entry.OP - opUsed)).OrderByDescending(Function(w) If(w.OPs > 0, w.DPS / w.OPs, 0)).FirstOrDefault()
            End If
            If best Is Nothing Then
                Dim candidates = GetCompatibleWeapons(slot, weapons)
                If candidates.Count = 0 Then
                    sb.AppendLine(TSize(slot.Size) & " " & TSlot(slot.Type) & "（" & TMount(slot.Mount) & "）槽：无匹配武器")
                Else
                    sb.AppendLine(TSize(slot.Size) & " " & TSlot(slot.Type) & "（" & TMount(slot.Mount) & "）槽：OP 不足")
                End If
            Else
                sb.AppendLine(TSize(slot.Size) & " " & TSlot(slot.Type) & "（" & TMount(slot.Mount) & "）槽 → " & best.Name & " " & best.Id & "【" & TDamage(best.DamageType) & "】" & best.OPs & " OP · " & best.DPS.ToString("0") & " DPS")
                opUsed += best.OPs
            End If
        Next
        If entry.OP > 0 Then sb.AppendLine().AppendLine("OP 使用：" & opUsed & " / " & entry.OP)
        Return sb.ToString().TrimEnd()
    End Function

    ''' <summary>生成每个槽位的武器分配（下标对应槽位，Nothing = 未分配）。</summary>
    Function BuildAssignments(installPath As String, entry As ShipEntry, manual As Dictionary(Of Integer, WeaponInfo)) As List(Of WeaponInfo)
        Dim slots = ParseShipSlots(installPath, entry.Id)
        Dim weapons = ParseWeapons(installPath)
        Dim result = New List(Of WeaponInfo)()
        Dim opUsed = 0
        For i = 0 To slots.Count - 1
            Dim slot = slots(i)
            Dim best As WeaponInfo = Nothing
            If manual IsNot Nothing AndAlso manual.ContainsKey(i) Then
                best = manual(i)
            Else
                Dim candidates = GetCompatibleWeapons(slot, weapons)
                best = candidates.Where(Function(w) w.OPs <= (entry.OP - opUsed)).OrderByDescending(Function(w) If(w.OPs > 0, w.DPS / w.OPs, 0)).FirstOrDefault()
            End If
            If best IsNot Nothing Then opUsed += best.OPs
            result.Add(best)
        Next
        Return result
    End Function

    ''' <summary>解析武器贴图文件路径（turret/hardpoint）。</summary>
    Function ResolveWeaponSprite(installPath As String, weapon As WeaponInfo, mount As String) As String
        If weapon Is Nothing Then Return ""
        Dim rel = If(mount = "HARDPOINT", weapon.HardpointSprite, weapon.TurretSprite)
        If rel = "" Then rel = If(mount = "HARDPOINT", weapon.TurretSprite, weapon.HardpointSprite)
        If rel = "" Then
            rel = "graphics\weapons\" & weapon.Id & "_" & If(mount = "HARDPOINT", "hardpoint", "turret") & "_base.png"
        End If
        Dim roots As New List(Of String) From {IO.Path.Combine(installPath, "starsector-core")}
        roots.AddRange(GetEnabledModDirs(installPath))
        For Each r In roots
            Dim p = IO.Path.Combine(r, rel)
            If IO.File.Exists(p) Then Return p
        Next
        Return ""
    End Function

    ''' <summary>构建配装提示词（舰船槽位 + 每个槽位可选武器）。</summary>
    Function BuildLoadoutPrompt(installPath As String, entry As ShipEntry) As String
        Dim slots = ParseShipSlots(installPath, entry.Id)
        Dim weapons = ParseWeapons(installPath)
        Dim sb As New Text.StringBuilder()
        sb.AppendLine("舰船：" & entry.Name & "（" & entry.Id & "），吨位：" & TDesignation(entry.Designation) & "，OP 上限 " & entry.OP & "。")
        sb.AppendLine("武器槽共 " & slots.Count & " 个：")
        For i = 0 To slots.Count - 1
            Dim s = slots(i)
            Dim comp = GetCompatibleWeapons(s, weapons).Take(8)
            sb.AppendLine("  " & (i + 1) & ". " & TSize(s.Size) & " " & TSlot(s.Type) & "（" & TMount(s.Mount) & "）可选：" & String.Join(" / ", comp.Select(Function(w) w.Name & "(" & w.Id & "," & w.OPs & "OP)")))
        Next
        sb.AppendLine("请为每个槽位选择一件武器（受 OP 上限约束），按「序号. 武器名」输出，并简要说明配装思路。")
        Return sb.ToString()
    End Function

    ''' <summary>调用 OpenAI 兼容 API 生成配装建议。</summary>
    Async Function GenerateLoadoutViaApiAsync(prompt As String, url As String, key As String, model As String) As Task(Of String)
        Using client As New Net.Http.HttpClient()
            client.Timeout = TimeSpan.FromSeconds(120)
            Dim payload = Newtonsoft.Json.JsonConvert.SerializeObject(New With {
                .model = model,
                .messages = New Object() {
                    New With {.role = "system", .content = "你是远行星号（Starsector）的舰船配装专家，根据武器槽与可用武器给出合理配装。"},
                    New With {.role = "user", .content = prompt}
                }
            })
            Using body = New Net.Http.StringContent(payload, Text.Encoding.UTF8, "application/json")
                If key <> "" Then client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " & key)
                Dim resp = Await client.PostAsync(url, body)
                Dim text = Await resp.Content.ReadAsStringAsync()
                If Not resp.IsSuccessStatusCode Then Return "API 请求失败（" & CInt(resp.StatusCode) & "）：" & text.Substring(0, Math.Min(200, text.Length))
                Dim obj = Newtonsoft.Json.Linq.JObject.Parse(text)
                Dim choices = obj("choices")
                If choices Is Nothing OrElse choices.Count = 0 Then Return "API 返回异常：" & text.Substring(0, Math.Min(200, text.Length))
                Return choices(0)("message")("content").ToString().Trim()
            End Using
        End Using
    End Function

    ''' <summary>下载并安装一个 mod（直链），返回错误信息（空串 = 成功）。</summary>
    Async Function InstallFromUrl(url As String, modsDir As String) As Task(Of String)
        Try
            Dim tmp = Await ModRepoClient.DownloadToTempAsync(url)
            Dim r = ModInstaller.InstallArchive(tmp, modsDir)
            Return If(r.Success, "", r.Error)
        Catch ex As Exception
            Return ex.Message
        End Try
    End Function

End Module
