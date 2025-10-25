using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Fellowship_overlay.Core;

public sealed record BuffDefinition(int SpellId, string Name, string IconResourceKey, string Category);

public sealed record BuffPreset(string Name, string Description, IReadOnlyList<int> SpellIds);

public static class BuffCatalog
{
    private static readonly BuffDefinition[] _buffs =
    {
        new(21562, "Power Word: Fortitude", "Icon.Buff.Fortitude", "Raid Buff"),
        new(6673, "Battle Shout", "Icon.Buff.BattleShout", "Raid Buff"),
        new(1459, "Arcane Intellect", "Icon.Buff.ArcaneIntellect", "Raid Buff"),
        new(97462, "Rallying Cry", "Icon.Buff.RallyingCry", "Raid Cooldown"),
        new(31821, "Devotion Aura", "Icon.Buff.DevotionAura", "Raid Cooldown"),
        new(62618, "Power Word: Barrier", "Icon.Buff.PowerWordBarrier", "Raid Cooldown"),
        new(1022, "Blessing of Protection", "Icon.Buff.BlessingOfProtection", "External Cooldown"),
        new(6940, "Blessing of Sacrifice", "Icon.Buff.BlessingOfSacrifice", "External Cooldown"),
        new(33206, "Pain Suppression", "Icon.Buff.PainSuppression", "External Cooldown"),
        new(47788, "Guardian Spirit", "Icon.Buff.GuardianSpirit", "External Cooldown"),
        new(102342, "Ironbark", "Icon.Buff.Ironbark", "External Cooldown"),
        new(116849, "Life Cocoon", "Icon.Buff.LifeCocoon", "External Cooldown"),
    };

    private static readonly IReadOnlyDictionary<int, BuffDefinition> _byId =
        new ReadOnlyDictionary<int, BuffDefinition>(_buffs.ToDictionary(b => b.SpellId));

    private static readonly BuffPreset[] _presets =
    {
        new("Raid Buffs", "Fortitude, Battle Shout, and Arcane Intellect", new [] { 21562, 6673, 1459 }),
        new("External Cooldowns", "Defensive externals you can receive", new [] { 1022, 6940, 33206, 47788, 102342, 116849 }),
        new("Raid CDs", "Teamwide raid cooldowns", new [] { 97462, 31821, 62618 }),
    };

    public static IReadOnlyList<BuffDefinition> Buffs => _buffs;

    public static IReadOnlyList<BuffPreset> Presets => _presets;

    public static bool TryGet(int spellId, out BuffDefinition? definition)
        => _byId.TryGetValue(spellId, out definition);
}