namespace Sspcl.Core.Loadouts;

public enum LoadoutStyle { Balanced, LongRange, Assault, Defense }

/// <summary>规则生成器与外部 AI 共用相同的槽位 ID、约束和状态；手工锁定的安装不会被覆盖。</summary>
public static class LoadoutPlanner
{
    public static LoadoutPlan Generate(HullDefinition hull, IReadOnlyList<WeaponDefinition> weapons, LoadoutPlan original, LoadoutStyle style)
    {
        var plan = LoadoutRules.Copy(original);
        plan.Weapons = plan.Weapons.Where(p => plan.PinnedSlots.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var evaluation = LoadoutRules.Evaluate(hull, weapons, plan);
        if (!evaluation.Valid) throw new InvalidOperationException(string.Join("\n", evaluation.Errors));
        int remaining = hull.OrdnancePoints - evaluation.TotalOp;
        double flux = evaluation.WeaponFlux;
        int kinetic = plan.Weapons.Values.Count(id => weapons.Any(w => w.Id == id && w.DamageType == "KINETIC"));
        int explosive = plan.Weapons.Values.Count(id => weapons.Any(w => w.Id == id && w.DamageType == "HIGH_EXPLOSIVE"));
        int pd = plan.Weapons.Values.Count(id => weapons.Any(w => w.Id == id && w.PointDefense));
        foreach (var slot in hull.Slots.Where(s => s.CanEquip && !plan.PinnedSlots.Contains(s.Id)).OrderByDescending(s => LoadoutRules.SizeRank(s.Size)))
        {
            bool Sustainable(WeaponDefinition weapon)
            {
                if (hull.FluxDissipation <= 0) return true; // 未知散幅不作假设。
                int affordableVents = Math.Min(hull.FluxUpgradeLimit, plan.Vents + Math.Max(0, remaining - weapon.OrdnancePoints));
                double capacity = hull.FluxDissipation + affordableVents * 10;
                double projected = flux + (weapon.Type == "MISSILE" ? 0 : weapon.FluxPerSecond);
                return projected <= capacity * (style == LoadoutStyle.Assault ? 1.3 : 1.05);
            }
            var candidates = weapons.Where(w => w.Recommendable && LoadoutRules.Fits(slot, w) && w.OrdnancePoints > 0 && w.OrdnancePoints <= remaining && Sustainable(w)).ToList();
            double Score(WeaponDefinition weapon)
            {
                double score = Math.Log(1 + Math.Max(0, weapon.Dps)) / Math.Sqrt(Math.Max(1, weapon.OrdnancePoints));
                score += LoadoutRules.SizeRank(weapon.Size) == LoadoutRules.SizeRank(slot.Size) ? 3 : 0;
                score += Math.Min(weapon.Range, 1500) / (style == LoadoutStyle.LongRange ? 180 : style == LoadoutStyle.Assault ? 1200 : 600);
                double preferredRange = weapon.PointDefense ? 300 : style == LoadoutStyle.Assault ? 400 : style == LoadoutStyle.LongRange ? 900 : hull.HullSize == "CAPITAL_SHIP" || hull.HullSize == "CRUISER" ? 650 : 500;
                if (weapon.Range < preferredRange) score -= 5 * (1 - weapon.Range / preferredRange);
                if (weapon.DamageType == "KINETIC" && kinetic <= explosive) score += 2;
                if (weapon.DamageType == "HIGH_EXPLOSIVE" && explosive < kinetic) score += 2;
                if (weapon.PointDefense) score += pd < (style == LoadoutStyle.Defense ? 4 : 2) && slot.Size == "SMALL" ? 4 : -3;
                if (weapon.Type != "MISSILE" && hull.FluxDissipation > 0 && flux + weapon.FluxPerSecond > (hull.FluxDissipation + plan.Vents * 10) * 1.25) score -= 5;
                if (weapon.Type == "MISSILE" && style == LoadoutStyle.Assault) score += 2;
                return score;
            }
            var selected = candidates.OrderByDescending(Score).ThenBy(w => w.Id, StringComparer.Ordinal).FirstOrDefault();
            if (selected == null) continue;
            plan.Weapons[slot.Id] = selected.Id;
            remaining -= selected.OrdnancePoints;
            if (selected.Type != "MISSILE") flux += selected.FluxPerSecond;
            if (selected.DamageType == "KINETIC") kinetic++;
            if (selected.DamageType == "HIGH_EXPLOSIVE") explosive++;
            if (selected.PointDefense) pd++;
        }
        int neededVents = hull.FluxDissipation > 0 ? Math.Max(0, (int)Math.Ceiling((flux - hull.FluxDissipation) / 10) - plan.Vents) : 0;
        plan.Vents += Math.Min(remaining, Math.Min(Math.Max(0, hull.FluxUpgradeLimit - plan.Vents), neededVents));
        plan.Explanation = "已按槽位、OP、射程、反盾/反甲、防空和武器幅能生成；保留锁定槽位，剩余 OP 优先补足通风，不推荐机载炸弹与仅供特殊用途的武器。实际战斗效果需在游戏中测试。";
        return plan;
    }
}
