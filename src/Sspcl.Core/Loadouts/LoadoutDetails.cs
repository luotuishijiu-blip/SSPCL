using System.Globalization;

namespace Sspcl.Core.Loadouts;

public sealed class DamageSummary
{
    public double RawDps { get; internal set; }
    public double ShieldDps { get; internal set; }
    public double ArmorDps { get; internal set; }
    public double HullDps { get; internal set; }
    public double FiringFlux { get; internal set; }
    public double? FluxPerDamage => RawDps > 0 ? FiringFlux / RawDps : null;
    public int UnknownDamageWeapons { get; internal set; }
    public int MissingWeapons { get; internal set; }
    public int EstimatedWeapons { get; internal set; }
}

public static class LoadoutDetails
{
    // 与游戏附带 API DamageType.java 保持一致；护甲列是类型加权值，不是装甲减伤后的实伤。
    public static (double Shield, double Armor, double Hull) Multipliers(string type) => type.ToUpperInvariant() switch {
        "KINETIC" => (2, .5, 1), "HIGH_EXPLOSIVE" => (.5, 2, 1), "FRAGMENTATION" => (.25, .25, 1), "ENERGY" or "OTHER" => (1, 1, 1), _ => (0, 0, 0)
    };

    public static DamageSummary Damage(HullDefinition hull, IEnumerable<WeaponDefinition> weapons, LoadoutPlan plan)
    {
        var result = new DamageSummary();
        var index = weapons.GroupBy(w => w.Id).ToDictionary(g => g.Key, g => g.First());
        // 槽位为键，同一武器在多个槽位必须多次累计，内置槽位只计一次。
        var assignments = new Dictionary<string, string>(hull.BuiltInWeapons, StringComparer.Ordinal);
        foreach (var pair in plan.Weapons) if (!assignments.ContainsKey(pair.Key)) assignments[pair.Key] = pair.Value;
        foreach (string id in assignments.Values)
        {
            if (!index.TryGetValue(id, out var weapon)) { result.MissingWeapons++; continue; }
            if (weapon.Type == "MISSILE") continue;
            var mult = Multipliers(weapon.DamageType);
            if (mult.Shield == 0 && mult.Armor == 0 && mult.Hull == 0) { result.UnknownDamageWeapons++; continue; }
            double dps = Math.Max(0, weapon.Dps);
            result.RawDps += dps; result.ShieldDps += dps * mult.Shield; result.ArmorDps += dps * mult.Armor; result.HullDps += dps * mult.Hull;
            result.FiringFlux += Math.Max(0, weapon.FluxPerSecond);
            if (weapon.DpsEstimated) result.EstimatedWeapons++;
        }
        return result;
    }

    public static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    public static string Ratio(double? value) => value.HasValue ? Number(value.Value) : "—";
    public static string WeaponOverview(WeaponDefinition w)
    {
        var mult = Multipliers(w.DamageType);
        return $"{w.Type} · {w.Size} · {w.DamageType}\n{w.OrdnancePoints} OP · 射程 {Number(w.Range)}\n"
            + $"单发伤害 {Number(w.DamagePerShot)} · DPS {Number(w.Dps)}{(w.DpsEstimated ? "（按射击周期估算）" : "")}\n"
            + $"对盾 / 对甲 / 对结构 DPS：{Number(w.Dps * mult.Shield)} / {Number(w.Dps * mult.Armor)} / {Number(w.Dps * mult.Hull)}\n"
            + $"连射幅能 {Number(w.FluxPerSecond)}/秒 · 长期幅能 {Number(w.SustainedFluxPerSecond)}/秒\n"
            + $"幅伤比 {Ratio(w.Dps > 0 ? w.FluxPerSecond / w.Dps : null)} · 单发幅能 {Number(w.FluxPerShot)}\n"
            + $"弹药 {(w.AmmoCapacity > 0 ? Number(w.AmmoCapacity) : "无限/未提供")} · 再生 {Number(w.AmmoRegeneration)}/秒\n来源：{w.Source} · ID：{w.Id}";
    }
    public static string HullOverview(HullDefinition hull)
    {
        string Value(string name) => hull.Stats.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : "—";
        string shield = Value("shield type");
        return $"结构 {Value("hitpoints")} · 装甲 {Value("armor rating")} · 速度 {Value("max speed")}\n"
            + $"护盾 {shield} · 盾效 {Value("shield efficiency")} 幅能/伤害 · 盾弧 {Value("shield arc")}°\n"
            + $"幅能容量 {Value("max flux")} · 基础散幅 {Number(hull.FluxDissipation)}/秒 · 部署 {Value("fleet pts")} DP";
    }

    public static List<LoadoutParameter> Parameters(Dictionary<string, string> fields) => fields.Select(pair => new LoadoutParameter {
        Name = (Labels.TryGetValue(pair.Key, out string? label) ? label + " · " : "") + pair.Key,
        Value = string.IsNullOrWhiteSpace(pair.Value) ? "—" : pair.Value
    }).ToList();

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase) {
        ["name"] = "名称", ["id"] = "ID", ["type"] = "伤害类型", ["range"] = "射程", ["damage/second"] = "每秒伤害", ["damage/shot"] = "单发伤害",
        ["emp"] = "单发 EMP", ["impact"] = "冲击力", ["OPs"] = "装配点", ["ammo"] = "弹药", ["ammo/sec"] = "弹药再生/秒", ["reload size"] = "每次回装数量",
        ["energy/shot"] = "单发幅能", ["energy/second"] = "每秒幅能", ["chargeup"] = "蓄能时间", ["chargedown"] = "冷却时间", ["burst size"] = "连射数", ["burst delay"] = "连射间隔",
        ["turn rate"] = "转速", ["min spread"] = "最小散布", ["max spread"] = "最大散布", ["spread/shot"] = "每发散布增加", ["spread decay/sec"] = "散布恢复/秒",
        ["beam speed"] = "光束速度", ["proj speed"] = "弹速", ["launch speed"] = "初速", ["flight time"] = "飞行时间", ["proj hitpoints"] = "弹体结构", ["hints"] = "行为提示", ["tags"] = "标签",
        ["autofireAccBonus"] = "自动开火精度修正", ["extraArcForAI"] = "AI 额外射界", ["groupTag"] = "武器组标签", ["primaryRoleStr"] = "主要用途", ["speedStr"] = "速度描述",
        ["trackingStr"] = "跟踪描述", ["turnRateStr"] = "转速描述", ["accuracyStr"] = "精度描述", ["customPrimary"] = "特性说明", ["customAncillary"] = "附加说明",
        ["tech/manufacturer"] = "技术/制造商", ["tier"] = "层级", ["rarity"] = "稀有度", ["base value"] = "基础价值",
        ["designation"] = "舰型", ["system id"] = "系统", ["fleet pts"] = "部署点", ["hitpoints"] = "结构", ["armor rating"] = "装甲", ["max flux"] = "幅能容量",
        ["flux dissipation"] = "散幅", ["ordnance points"] = "装配点", ["fighter bays"] = "机库", ["max speed"] = "最大速度", ["acceleration"] = "加速度", ["deceleration"] = "减速度",
        ["max turn rate"] = "最大转速", ["turn acceleration"] = "转向加速度", ["mass"] = "质量", ["shield type"] = "护盾类型", ["shield arc"] = "盾弧", ["shield upkeep"] = "护盾维持幅能",
        ["shield efficiency"] = "盾效(幅能/伤害)", ["phase cost"] = "相位启动幅能", ["phase upkeep"] = "相位维持幅能", ["min crew"] = "最低船员", ["max crew"] = "最高船员",
        ["cargo"] = "货舱", ["fuel"] = "燃料舱", ["fuel/ly"] = "燃料/光年", ["max burn"] = "最大航速", ["supplies/rec"] = "部署补给", ["supplies/mo"] = "月补给", ["peak CR sec"] = "峰值作战时间",
        ["cr %/day"] = "每日 CR 恢复", ["CR to deploy"] = "部署 CR", ["CR loss/sec"] = "CR 流失/秒", ["script"] = "效果脚本", ["desc"] = "说明", ["short"] = "简述", ["sModDesc"] = "S 插说明",
        ["cost_frigate"] = "护卫舰 OP", ["cost_dest"] = "驱逐舰 OP", ["cost_cruiser"] = "巡洋舰 OP", ["cost_capital"] = "主力舰 OP", ["hidden"] = "隐藏", ["hiddenEverywhere"] = "全局隐藏", ["uiTags"] = "分类"
    };
}
