Imports System.Diagnostics
Imports System.Text.RegularExpressions
Imports Sspcl.Core.Install
Imports Sspcl.Core.Launch
Imports Sspcl.Core.Mods
Imports Sspcl.Core.Saves
Imports Sspcl.Core.Store


Partial Module ModStarsector

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

End Module
