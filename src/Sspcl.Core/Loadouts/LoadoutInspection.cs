using System.Globalization;

namespace Sspcl.Core.Loadouts;

public sealed class WeaponFilter
{
    public string Search { get; set; } = "";
    public string Size { get; set; } = "";
    public string Type { get; set; } = "";
    public string DamageType { get; set; } = "";
    public string Source { get; set; } = "";
    public string Feature { get; set; } = "";
    public double? MaxOp { get; set; }
    public double? MinRange { get; set; }
    public bool Matches(WeaponDefinition w) =>
        (Search.Length == 0 || (w.Name + " " + w.Id + " " + w.Source).IndexOf(Search, StringComparison.OrdinalIgnoreCase) >= 0) &&
        (Size.Length == 0 || w.Size == Size) && (Type.Length == 0 || w.Type == Type) &&
        (DamageType.Length == 0 || w.DamageType == DamageType) && (Source.Length == 0 || w.Source == Source) &&
        (!MaxOp.HasValue || w.OrdnancePoints <= MaxOp) && (!MinRange.HasValue || w.Range >= MinRange) &&
        (Feature.Length == 0 || Feature == "PD" && w.PointDefense || Feature == "BEAM" && w.SpecClass.IndexOf("beam", StringComparison.OrdinalIgnoreCase) >= 0 ||
         Feature == "PROJECTILE" && w.SpecClass.IndexOf("beam", StringComparison.OrdinalIgnoreCase) < 0 ||
         Feature == "FINITE" && w.AmmoCapacity > 0 || Feature == "UNLIMITED" && w.AmmoCapacity <= 0);
}

public sealed class WeaponComparisonRow
{
    public string Name { get; set; } = "";
    public string Left { get; set; } = "—";
    public string Right { get; set; } = "—";
    public string Difference { get; set; } = "";
}

public static class LoadoutInspection
{
    // 舰船基础部署消耗使用 supplies/rec，fleet pts 是舰队力量评分。
    public static double? BaseDeployment(HullDefinition hull) => hull.Stats.TryGetValue("supplies/rec", out string? value) &&
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) && result >= 0 && !double.IsInfinity(result) && !double.IsNaN(result) ? result : null;
    public static int DModCount(HullDefinition hull, LoadoutPlan plan, IEnumerable<HullModDefinition> mods)
    {
        var ids = new HashSet<string>(mods.Where(m => m.IsDMod).Select(m => m.Id), StringComparer.Ordinal);
        return hull.BuiltInHullMods.Concat(plan.HullMods).Concat(plan.PermaMods).Concat(plan.DMods).Distinct(StringComparer.Ordinal).Count(ids.Contains);
    }
    // 本机 0.98 API DerelictContingent：6%/项，最多5项；技能未启用时不减部署点。
    public static double? Deployment(HullDefinition hull, LoadoutPlan plan, IEnumerable<HullModDefinition> mods, bool derelictOperations) =>
        BaseDeployment(hull) * (derelictOperations ? 1 - .06 * Math.Min(5, DModCount(hull, plan, mods)) : 1);

    public static List<WeaponComparisonRow> Compare(WeaponDefinition? left, WeaponDefinition? right)
    {
        var rows = new List<WeaponComparisonRow>();
        void Text(string name, Func<WeaponDefinition, string> get) => rows.Add(new() { Name = name, Left = left == null ? "—" : get(left), Right = right == null ? "—" : get(right) });
        void Number(string name, Func<WeaponDefinition, double?> get, int preferred = 0)
        {
            double? a = left == null ? null : get(left), b = right == null ? null : get(right);
            string difference = a.HasValue && b.HasValue ? "B−A " + LoadoutDetails.Number(b.Value - a.Value) : "";
            if (a.HasValue && b.HasValue && preferred != 0 && a != b) difference += ((b.Value > a.Value) == (preferred > 0) ? " · B 更优" : " · A 更优");
            rows.Add(new() { Name = name, Left = LoadoutDetails.Ratio(a), Right = LoadoutDetails.Ratio(b), Difference = difference });
        }
        Text("尺寸 / 类型", w => w.Size + " / " + w.Type); Text("伤害类型", w => w.DamageType); Text("来源", w => w.Source);
        Number("OP", w => w.OrdnancePoints, -1); Number("射程", w => w.Range, 1); Number("单发伤害", w => w.DamagePerShot); Number("DPS", w => w.Dps, 1);
        Number("护盾 DPS", w => w.Dps * LoadoutDetails.Multipliers(w.DamageType).Shield, 1);
        Number("装甲 DPS", w => w.Dps * LoadoutDetails.Multipliers(w.DamageType).Armor, 1);
        Number("结构 DPS", w => w.Dps * LoadoutDetails.Multipliers(w.DamageType).Hull, 1);
        Number("连射幅能 / 秒", w => w.FluxPerSecond, -1); Number("长期幅能 / 秒", w => w.SustainedFluxPerSecond, -1);
        Number("幅伤比", w => w.Dps > 0 ? w.FluxPerSecond / w.Dps : null, -1);
        Text("弹药容量", w => w.AmmoCapacity > 0 ? LoadoutDetails.Number(w.AmmoCapacity) : "无限/未提供"); Number("弹药再生 / 秒", w => w.AmmoRegeneration);
        foreach (string field in new[] { "emp", "proj speed", "turn rate", "min spread", "max spread", "chargeup", "burst size", "burst delay" })
            Number(LoadoutDetails.Parameters(new() { [field] = "" })[0].Name, w => w.Stats.TryGetValue(field, out string? raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null);
        return rows;
    }
}
