namespace Sspcl.Core.Loadouts;

public sealed class LoadoutEvaluation
{
    public int WeaponOp { get; internal set; }
    public int TotalOp { get; internal set; }
    public double WeaponFlux { get; internal set; }
    public List<string> Errors { get; } = new();
    public bool Valid => Errors.Count == 0;
}

public static class LoadoutRules
{
    public static bool Fits(WeaponSlot slot, WeaponDefinition weapon)
    {
        if (!slot.CanEquip || weapon.Restricted || SizeRank(weapon.Size) == 0 || SizeRank(weapon.Size) > SizeRank(slot.Size)) return false;
        return slot.Type == weapon.Type || slot.Type == "UNIVERSAL" && new[] { "BALLISTIC", "ENERGY", "MISSILE" }.Contains(weapon.Type) ||
            slot.Type == "HYBRID" && (weapon.Type == "BALLISTIC" || weapon.Type == "ENERGY") ||
            slot.Type == "COMPOSITE" && (weapon.Type == "BALLISTIC" || weapon.Type == "MISSILE") ||
            slot.Type == "SYNERGY" && (weapon.Type == "ENERGY" || weapon.Type == "MISSILE");
    }
    public static int SizeRank(string size) => size == "SMALL" ? 1 : size == "MEDIUM" ? 2 : size == "LARGE" ? 3 : 0;
    public static LoadoutEvaluation Evaluate(HullDefinition hull, IEnumerable<WeaponDefinition> weapons, LoadoutPlan plan)
    {
        var result = new LoadoutEvaluation();
        var index = weapons.GroupBy(w => w.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (string id in hull.BuiltInWeapons.Values)
            if (index.TryGetValue(id, out var builtin) && builtin.Type != "MISSILE") result.WeaponFlux += builtin.SustainedFluxPerSecond;
        if (plan.HullId != hull.Id) result.Errors.Add("方案与舰船不匹配。");
        foreach (var assignment in plan.Weapons)
        {
            var slot = hull.Slots.FirstOrDefault(s => s.Id == assignment.Key);
            if (slot == null) { result.Errors.Add("未知槽位：" + assignment.Key); continue; }
            if (!index.TryGetValue(assignment.Value, out var weapon)) { result.Errors.Add("未知武器：" + assignment.Value); continue; }
            if (!Fits(slot, weapon) || weapon.OrdnancePoints < 0) { result.Errors.Add(slot.Id + " 无法安装 " + weapon.Name); continue; }
            result.WeaponOp += weapon.OrdnancePoints;
            if (weapon.Type != "MISSILE") result.WeaponFlux += weapon.SustainedFluxPerSecond;
        }
        if (plan.Vents < 0 || plan.Capacitors < 0 || plan.Vents > hull.FluxUpgradeLimit || plan.Capacitors > hull.FluxUpgradeLimit)
            result.Errors.Add("电容或通风超过该舰船的上限。");
        result.TotalOp = result.WeaponOp + plan.Vents + plan.Capacitors;
        if (result.TotalOp > hull.OrdnancePoints) result.Errors.Add("超出 OP 预算：" + result.TotalOp + " / " + hull.OrdnancePoints);
        return result;
    }
    public static LoadoutPlan Copy(LoadoutPlan plan) => new() {
        HullId = plan.HullId, Name = plan.Name, Vents = plan.Vents, Capacitors = plan.Capacitors, Explanation = plan.Explanation,
        Weapons = new Dictionary<string, string>(plan.Weapons, StringComparer.Ordinal), PinnedSlots = new HashSet<string>(plan.PinnedSlots, StringComparer.Ordinal)
    };
}
