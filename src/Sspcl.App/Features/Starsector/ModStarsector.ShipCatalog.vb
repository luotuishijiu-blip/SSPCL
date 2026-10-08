Imports System.Diagnostics
Imports System.Text.RegularExpressions
Imports Sspcl.Core.Install
Imports Sspcl.Core.Launch
Imports Sspcl.Core.Mods
Imports Sspcl.Core.Saves
Imports Sspcl.Core.Store


Partial Module ModStarsector

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

End Module
