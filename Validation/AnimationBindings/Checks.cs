using System;
using System.IO;
using System.Linq;
using System.Text.Json;
class Checks
{
    static int count;
    static void Assert(bool value, string name) { if (!value) throw new Exception(name); count++; }
    static CharacterAnimation Actor() => new() { unityJson = "{\"animations\":[{\"name\":\"Default\"},{\"name\":\"Idle\"},{\"name\":\"Run\"},{\"name\":\"Attack\"},{\"name\":\"Skill\"},{\"name\":\"Hurt\"},{\"name\":\"Die\"},{\"name\":\"Stun\"},{\"name\":\"ChargeSlash\"},{\"name\":\"Fireball\"}]}" };
    static void Main(string[] args)
    {
        var a = Actor(); string original = a.unityJson;
        foreach (string name in new[] { "Default", "Idle", "Run", "Attack", "Skill", "Hurt", "Die", "Stun" })
            Assert(!a.SetSkillAnimationName(name, "Renamed", out _), "locked " + name);
        Assert(!a.SetSkillAnimationName("missing", "Valid", out _), "missing source rejected");
        Assert(!a.SetSkillAnimationName("ChargeSlash", " ", out _), "empty rejected");
        Assert(!a.SetSkillAnimationName("ChargeSlash", "attack", out _), "reserved alias rejected");
        Assert(!a.SetSkillAnimationName("ChargeSlash", "fireBALL", out _), "source collision rejected");
        Assert(a.SetSkillAnimationName("ChargeSlash", " Skill_Player_1 ", out _), "custom rename accepted");
        Assert(a.GetAnimationNameDefinitions().Single(x => x.sourceName == "ChargeSlash").animationName == "Skill_Player_1", "Inspector shows trimmed alias");
        Assert(a.HasAnimation("Skill_Player_1") && a.HasAnimation("skill_player_1"), "alias resolves before Awake");
        Assert(!a.SetSkillAnimationName("Fireball", "Skill_Player_1", out _), "alias collision rejected");
        Assert(a.unityJson == original, "source JSON untouched");
        a.unityJson = "{\"animations\":[{\"name\":\"Skill_Player_1\"}]}";
        Assert(a.HasAnimation("Skill_Player_1"), "stale binding does not shadow a new JSON animation");
        a.unityJson = original;
        a.LoadRuntime(); Assert(a.HasAnimation("Skill_Player_1"), "runtime alias resolves");
        Assert(a.ResolveBasicAttackAnimationName("BasicSlash", null) == "Attack", "BasicSlash uses exported Attack");
        Assert(a.ResolveBasicAttackAnimationName("Skill_Player_1", null) == "Skill_Player_1", "custom animation wins over fallback");
        Assert(a.ResolveBasicAttackAnimationName("BasicSlash", " Fireball ") == "Fireball", "explicit context name takes priority");
        Assert(a.SetSkillAnimationName("ChargeSlash", "ChargeSlash", out _) && !a.HasAnimation("Skill_Player_1"), "reset restores source name");
        var noAttack = new CharacterAnimation { unityJson = "{\"animations\":[{\"name\":\"Idle\"}]}" };
        Assert(noAttack.ResolveBasicAttackAnimationName("BasicSlash", null) == "BasicSlash", "absent Attack does not invent animation");
        a.Groups = new[] { new CharacterAnimationGroupInfo { name = "Wepon", variants = new[] { "Dao Găm Gỉ", "Sword" } }, new CharacterAnimationGroupInfo { name = "Outfit", variants = new[] { "Coat" } } };
        a.Selected["Outfit"] = "Coat";
        string[] known = { "Dao Găm Gỉ", "Sword" };
        Assert(a.SyncEquippedWeapon("", known) && a.Selected["Wepon"] == "", "startup unarmed clears JSON default");
        Assert(a.SyncEquippedWeapon("dao găm gỉ", known) && a.Selected["Wepon"] == "Dao Găm Gỉ", "equip matches variant with original group spelling");
        Assert(a.SyncEquippedWeapon("Sword", known) && a.Selected["Wepon"] == "Sword", "switch weapon");
        Assert(a.SyncEquippedWeapon(null, known) && a.Selected["Wepon"] == "", "unequip sends empty variant");
        Assert(a.Selected["Outfit"] == "Coat", "outfit unaffected");
        a.WeaponMapping("Item_Sword", "Sword");
        Assert(a.SyncEquippedWeapon("Item_Sword", known) && a.Selected["Wepon"] == "Sword", "Inspector item-to-variant mapping");
        Assert(!a.SyncEquippedWeapon("Unknown", known) && a.Selected["Wepon"] == "", "unsupported weapon clears previous visual");
        a.Groups = a.Groups.Concat(new[] { new CharacterAnimationGroupInfo { name = "Second", variants = new[] { "Sword" } } }).ToArray();
        Assert(!a.SyncEquippedWeapon("Sword", known) && a.Warnings.Last() == "weapon-group-ambiguous", "ambiguous group requires explicit choice");
        a.WeaponGroup("Wepon");
        Assert(a.SyncEquippedWeapon("Sword", known) && a.Selected["Wepon"] == "Sword", "explicit group resolves ambiguity");
        a.Groups = Array.Empty<CharacterAnimationGroupInfo>();
        Assert(!a.SyncEquippedWeapon("Sword", known), "legacy JSON without groups is supported");
        if (args.Length > 0)
        {
            var real = new CharacterAnimation { unityJson = File.ReadAllText(args[0]) };
            using var doc = JsonDocument.Parse(real.unityJson);
            real.Groups = doc.RootElement.GetProperty("groupBlocks").EnumerateArray().Select(g => new CharacterAnimationGroupInfo { name = g.GetProperty("name").GetString(), variants = g.GetProperty("variants").EnumerateArray().Select(v => v.GetProperty("name").GetString()).ToArray() }).ToArray();
            Assert(real.ResolveBasicAttackAnimationName("BasicSlash", null) == "Attack", "actual player JSON attack fallback");
            string variant = real.Groups[0].variants[0]; string group = real.Groups[0].name;
            Assert(real.SyncEquippedWeapon(variant, new[] { variant }) && real.Selected[group] == variant, "actual player JSON equip");
            Assert(real.SyncEquippedWeapon("", new[] { variant }) && real.Selected[group] == "", "actual player JSON unequip");
        }
        Console.WriteLine($"PASS: {count} animation binding/equipment checks against production bindings (Unity API doubles; no Play Mode claim).");
    }
}
