Imports System.Diagnostics
Imports System.Text.RegularExpressions
Imports Sspcl.Core.Install
Imports Sspcl.Core.Launch
Imports Sspcl.Core.Mods
Imports Sspcl.Core.Saves
Imports Sspcl.Core.Store


Partial Module ModStarsector

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

End Module
