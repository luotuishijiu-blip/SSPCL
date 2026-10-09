using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Sspcl.Core.Loadouts;

static class LoadoutChecks
{
    public static async Task Run(string root)
    {
        string game = Path.Combine(root, "loadout-game");
        void Put(string relative, string text) { string path = Path.Combine(game, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); }
        Put("starsector-core/data/hulls/ship_data.csv", "name,id,ordnance points,flux dissipation\n\"Ship, quoted\",test,30,300\n");
        Put("starsector-core/data/hulls/test.ship", """
        {"hullId":"test","hullSize":"CRUISER","width":288,"height":384,"center":[144,140],"spriteName":"graphics/test.png",
        "weaponSlots":[{"id":"WS 001","type":"BALLISTIC","size":"MEDIUM","mount":"TURRET","locations":[158,71],"angle":70,"arc":150},
        {"id":"WS 002","type":"ENERGY","size":"SMALL","mount":"HARDPOINT","locations":[158,-71],"angle":-70,"arc":30},
        {"id":"WS 003","type":"BALLISTIC","size":"SMALL","mount":"TURRET","locations":[0,0]}],"builtInWeapons":{"WS 003":"a"}}
        """);
        Put("starsector-core/data/hulls/test.skin", """
        {"skinHullId":"skin","baseHullId":"test","hullName":"Skin","ordnancePoints":+040,"baseValueMult":.8f,"removeBuiltInWeapons":["WS 003"],
        "weaponSlotChanges":{"WS 001":{"type":"HYBRID","locations":[[187,0]],"angle":0}}}
        """);
        Put("starsector-core/data/weapons/weapon_data.csv", "name,id,OPs,range,damage/second,type,energy/second\n\"Gun, A\",a,5,700,100,KINETIC,60\nEnergy,b,4,600,90,ENERGY,80\nLarge,c,50,900,300,HIGH_EXPLOSIVE,200\n");
        foreach (var (id, type, size) in new[] { ("a", "BALLISTIC", "SMALL"), ("b", "ENERGY", "SMALL"), ("c", "BALLISTIC", "LARGE") })
            Put("starsector-core/data/weapons/" + id + ".wpn", "{\"id\":\"" + id + "\",\"type\":\"" + type + "\",\"size\":\"" + size + "\";\"renderHints\":[RENDER_BARREL_BELOW],\"offset\":1e-3}");
        Put("mods/enabled/mod_info.json", "{\"id\":\"enabled\",\"name\":\"Enabled\",\"version\":\"1\"}");
        Put("mods/enabled/data/weapons/weapon_data.csv", "name,id,OPs\nOverride,a,6\n");
        Put("mods/disabled/mod_info.json", "{\"id\":\"disabled\",\"name\":\"Disabled\",\"version\":\"1\"}");
        Put("mods/disabled/data/weapons/weapon_data.csv", "name,id,OPs\nWrong,a,100\n");
        Put("mods/enabled_mods.json", "{\"enabledMods\":[\"enabled\"]}");
        var catalog = LoadoutCatalogReader.Read(game);
        Check(catalog.Warnings.Count == 0, "Bare enums/scientific literal parse");
        var hull = catalog.Hulls.Single(h => h.Id == "test");
        var skin = catalog.Hulls.Single(h => h.Id == "skin");
        Check(hull.Name == "Ship, quoted", "Quoted CSV");
        Check(catalog.Weapons.Single(w => w.Id == "a").OrdnancePoints == 6 && catalog.Weapons.Single(w => w.Id == "a").Range == 700, "Enabled MOD stats override, inherited blank columns");
        Check(hull.Slots[2].Locked && !skin.Slots[2].Locked && skin.OrdnancePoints == 40 && skin.Slots[0].Type == "HYBRID", "Skin inheritance/builtin removal");
        var position = ShipGeometry.SlotToSprite(hull, hull.Slots[0]);
        Check(position.X == 73 && position.Y == 86, "Left slot coordinate");
        position = ShipGeometry.SlotToSprite(hull, hull.Slots[1]);
        Check(position.X == 215 && position.Y == 86 && ShipGeometry.WeaponRotation(hull.Slots[1]) == 70, "Mirrored slot and angle");
        Check(ShipGeometry.WeaponPivot(20, 40, "HARDPOINT").Y == 30 && ShipGeometry.WeaponPivot(20, 40, "TURRET").Y == 20, "Mount pivot");
        foreach (string type in new[] { "BALLISTIC", "ENERGY", "MISSILE", "UNIVERSAL", "HYBRID", "COMPOSITE", "SYNERGY" })
            foreach (string weaponType in new[] { "BALLISTIC", "ENERGY", "MISSILE" })
            {
                var slot = new WeaponSlot { Type = type, Size = "MEDIUM", Mount = "TURRET" };
                bool expected = type == weaponType || type == "UNIVERSAL" || type == "HYBRID" && weaponType != "MISSILE" || type == "COMPOSITE" && weaponType != "ENERGY" || type == "SYNERGY" && weaponType != "BALLISTIC";
                Check(LoadoutRules.Fits(slot, new WeaponDefinition { Type = weaponType, Size = "SMALL" }) == expected, "Type compatibility " + type + "/" + weaponType);
                Check(!LoadoutRules.Fits(slot, new WeaponDefinition { Type = weaponType, Size = "LARGE" }), "Oversize rejection");
            }
        var plan = new LoadoutPlan { HullId = hull.Id, Vents = 3, Capacitors = 2 };
        plan.Weapons.Add("WS 001", "a"); plan.PinnedSlots.Add("WS 001");
        Check(LoadoutRules.Evaluate(hull, catalog.Weapons, plan).WeaponFlux == 120, "Built-in weapons contribute flux without OP cost");
        plan.PinnedSlots.Add("WS 002"); // Locked empty stays empty.
        var generated = LoadoutPlanner.Generate(hull, catalog.Weapons, plan, LoadoutStyle.Balanced);
        Check(generated.Weapons.Count == 1 && generated.Weapons["WS 001"] == "a" && LoadoutRules.Evaluate(hull, catalog.Weapons, generated).Valid, "Pinned full/empty preservation");
        var hidden = new WeaponDefinition { Id = "hidden", Type = "BALLISTIC", Size = "MEDIUM", OrdnancePoints = 1, Dps = 100000, Recommendable = false };
        var ordinary = new WeaponDefinition { Id = "ordinary", Type = "BALLISTIC", Size = "MEDIUM", OrdnancePoints = 5, Range = 700, Dps = 100, FluxPerSecond = 420 };
        var usable = LoadoutPlanner.Generate(hull, new[] { hidden, ordinary }, new LoadoutPlan { HullId = hull.Id }, LoadoutStyle.Balanced);
        Check(usable.Weapons["WS 001"] == "ordinary" && usable.Vents == 12 && LoadoutRules.Evaluate(hull, new[] { hidden, ordinary }, usable).Valid, "Special weapons excluded and spare OP allocates vents");
        var overload = new WeaponDefinition { Id = "overload", Type = "BALLISTIC", Size = "MEDIUM", OrdnancePoints = 1, Range = 1200, Dps = 100000, FluxPerSecond = 10000 };
        var sustainable = LoadoutPlanner.Generate(hull, new[] { overload, ordinary }, new LoadoutPlan { HullId = hull.Id }, LoadoutStyle.Balanced);
        Check(sustainable.Weapons["WS 001"] == "ordinary", "High DPS cannot defeat sustained flux budget");
        var regenerating = new WeaponDefinition { FluxPerSecond = 750, FluxPerShot = 150, AmmoCapacity = 20, AmmoRegeneration = 1 };
        Check(regenerating.SustainedFluxPerSecond == 150, "Ammo regeneration bounds sustained builtin flux");
        string json = LoadoutVariant.Serialize(hull, catalog.Weapons, generated, "test_sspcl");
        var data = JsonNode.Parse(json)!;
        Check(data["weaponGroups"]!.AsArray().SelectMany(g => g!["weapons"]!.AsObject()).Any(p => p.Key == "WS 003" && p.Value!.ToString() == "a"), "Built-in export group");
        Put("roundtrip.variant", json);
        var imported = LoadoutVariant.Read(Path.Combine(game, "roundtrip.variant"), hull, catalog.Weapons);
        Check(imported.Weapons.Count == 1 && imported.Weapons["WS 001"] == "a" && imported.Vents == 3, "Stable slot ID roundtrip");
        Put("extra.variant", json.Replace("\"quality\": 1", "\"sMods\": [\"extra\"], \"quality\": 1"));
        Throws(() => LoadoutVariant.Read(Path.Combine(game, "extra.variant"), hull, catalog.Weapons));
        Throws(() => LoadoutVariant.Serialize(hull, catalog.Weapons, plan, "../invalid"));
        var excessive = LoadoutRules.Copy(plan); excessive.Vents = 31;
        Check(!LoadoutRules.Evaluate(hull, catalog.Weapons, excessive).Valid, "Flux/OP constraints");
        string good = "{\"weapons\":{\"WS 001\":\"a\"},\"vents\":3,\"capacitors\":2,\"explanation\":\"OK\"}";
        Check(ChatLoadoutAdvisor.ParseSuggestion("```json\n" + good + "\n```", hull, catalog.Weapons, plan).Weapons["WS 001"] == "a", "Structured suggestion");
        foreach (string bad in new[] { good.Replace("\"a\"", "\"b\""), good.Replace("WS 001", "Unknown"), good.Replace("\"vents\":3", "\"vents\":99"), good.Replace("\"vents\":3", "\"vents\":3.5") })
            Throws(() => ChatLoadoutAdvisor.ParseSuggestion(bad, hull, catalog.Weapons, plan));
        Check(plan.Weapons["WS 001"] == "a" && plan.Vents == 3, "Rejected suggestions preserve input");
        using var client = new HttpClient(new AdvisorHandler(good));
        var result = await ChatLoadoutAdvisor.SuggestAsync(client, "https://ai.test/chat/completions", "secret", "test-model", hull, catalog.Weapons, plan, LoadoutStyle.Balanced, default);
        Check(result.Weapons["WS 001"] == "a", "Advice HTTP integration");
        using var cts = new CancellationTokenSource(); cts.Cancel();
        using var delayed = new HttpClient(new CancelHandler());
        try { await ChatLoadoutAdvisor.SuggestAsync(delayed, "https://ai.test/chat/completions", "", "test", hull, catalog.Weapons, plan, LoadoutStyle.Balanced, cts.Token); throw new Exception("Cancellation failed"); }
        catch (OperationCanceledException) { }
        Put("mods/enabled_mods.json", "{\"enabledMods\":[]}");
        Check(LoadoutCatalogReader.Read(game).Weapons.Single(w => w.Id == "a").Name == "Gun, A", "Empty enabled list excludes all MODs");
        Console.WriteLine("Loadout checks passed: CSV/enums, enabled sources, skins, geometry/pivots, compatibility, budgets, pins, variant roundtrip, validated AI and cancellation.");
    }

    public static void Live(string game)
    {
        var catalog = LoadoutCatalogReader.Read(game);
        Console.WriteLine($"Live catalog (read only): {catalog.Hulls.Count} hulls, {catalog.Weapons.Count} weapons, {catalog.Warnings.Count} warnings.");
        foreach (string warning in catalog.Warnings.Take(8)) Console.WriteLine(warning);
        var onslaught = catalog.Hulls.Single(h => h.Id == "onslaught");
        var slot = onslaught.Slots.Single(s => s.Id == "WS 001");
        var point = ShipGeometry.SlotToSprite(onslaught, slot);
        Check(point.X == 73 && point.Y == 86 && File.Exists(onslaught.SpritePath), "Actual Onslaught geometry/assets");
        Check(catalog.Hulls.Single(h => h.Id == "onslaught_xiv").OrdnancePoints == 370, "Actual XIV skin");
        foreach (var hull in catalog.Hulls.Where(h => h.Slots.Any(s => s.CanEquip)).Take(100))
            Check(LoadoutRules.Evaluate(hull, catalog.Weapons, LoadoutPlanner.Generate(hull, catalog.Weapons, new LoadoutPlan { HullId = hull.Id }, LoadoutStyle.Balanced)).Valid, "Live planner " + hull.Id);
        Console.WriteLine("Live loadout checks passed, no game files written.");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Throws(Action action) { try { action(); } catch (Exception ex) when (ex is InvalidOperationException || ex is InvalidDataException || ex is ArgumentException) { return; } throw new Exception("Expected rejection"); }
    private sealed class AdvisorHandler(string content) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string body = await request.Content!.ReadAsStringAsync(token);
            Check(!body.Contains("secret") && !body.Contains("loadout-game") && request.Headers.Authorization!.Parameter == "secret", "Prompt excludes secrets/paths");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new JsonObject { ["choices"] = new JsonArray(new JsonObject { ["message"] = new JsonObject { ["content"] = content } }) }.ToJsonString(), Encoding.UTF8, "application/json") };
        }
    }
    private sealed class CancelHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { await Task.Delay(Timeout.Infinite, token); throw new Exception("Unreachable"); }
    }
}
