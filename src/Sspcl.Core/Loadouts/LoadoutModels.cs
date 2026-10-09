using System.Text.Json.Serialization;

namespace Sspcl.Core.Loadouts;

public sealed class LoadoutCatalog
{
    public string GamePath { get; set; } = "";
    public List<HullDefinition> Hulls { get; } = new();
    public List<WeaponDefinition> Weapons { get; } = new();
    public List<string> Warnings { get; } = new();
}

public sealed class HullDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string DefinitionPath { get; set; } = "";
    public string SpritePath { get; set; } = "";
    public string BaseHullId { get; set; } = "";
    public string HullSize { get; set; } = "";
    public string Designation { get; set; } = "";
    public double Width { get; set; }
    public double Height { get; set; }
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public int OrdnancePoints { get; set; }
    public double FluxDissipation { get; set; }
    public List<WeaponSlot> Slots { get; set; } = new();
    public Dictionary<string, string> BuiltInWeapons { get; set; } = new(StringComparer.Ordinal);
    public List<string> BuiltInHullMods { get; set; } = new();
    public string Display => Name + " · " + Id;
    public int FluxUpgradeLimit => HullSize == "CAPITAL_SHIP" ? 50 : HullSize == "CRUISER" ? 30 : HullSize == "DESTROYER" ? 20 : 10;
}

public sealed class WeaponSlot
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Size { get; set; } = "";
    public string Mount { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Angle { get; set; }
    public double Arc { get; set; }
    public bool Locked { get; set; }
    public bool CanEquip => !Locked && Mount != "HIDDEN" &&
        new[] { "BALLISTIC", "ENERGY", "MISSILE", "UNIVERSAL", "HYBRID", "COMPOSITE", "SYNERGY" }.Contains(Type);
    public string Display => Id + " · " + Size + " " + Type;
}

public sealed class WeaponDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string Type { get; set; } = "";
    public string Size { get; set; } = "";
    public string DamageType { get; set; } = "";
    public int OrdnancePoints { get; set; }
    public double Range { get; set; }
    public double Dps { get; set; }
    public double FluxPerSecond { get; set; }
    public bool PointDefense { get; set; }
    public bool Restricted { get; set; }
    public bool Recommendable { get; set; } = true;
    public string TurretSprite { get; set; } = "";
    public string HardpointSprite { get; set; } = "";
    public string TurretGunSprite { get; set; } = "";
    public string HardpointGunSprite { get; set; } = "";
    public bool BarrelBelow { get; set; }
    public int AnimationFrames { get; set; } = 1;
    public bool AnimateHorizontal { get; set; } = true;
    public string Display => Name + " · " + OrdnancePoints + " OP · " + (int)Range + " 射程";
}

public sealed class LoadoutPlan
{
    public string HullId { get; set; } = "";
    public string Name { get; set; } = "SSPCL 配装";
    public Dictionary<string, string> Weapons { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> PinnedSlots { get; set; } = new(StringComparer.Ordinal);
    public int Vents { get; set; }
    public int Capacitors { get; set; }
    public string Explanation { get; set; } = "";
}

public readonly struct SpritePoint
{
    public double X { get; }
    public double Y { get; }
    public SpritePoint(double x, double y) { X = x; Y = y; }
}

public static class ShipGeometry
{
    // .ship 的 +X 为船首方向、+Y 为左舷；sprite 的原点为左上角。
    public static SpritePoint SlotToSprite(HullDefinition hull, WeaponSlot slot) =>
        new(hull.CenterX - slot.Y, hull.Height - hull.CenterY - slot.X);
    public static double WeaponRotation(WeaponSlot slot) => -slot.Angle;
    public static SpritePoint WeaponPivot(double width, double height, string mount) =>
        new(width / 2, height * (mount == "HARDPOINT" ? 0.75 : 0.5));
}
