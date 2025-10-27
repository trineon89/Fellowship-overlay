using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Fellowship_overlay.Core;

public sealed record BuffDefinition(int SpellId, string Name, string IconResourceKey, string Category, IReadOnlyList<string> Classes);

public sealed record BuffPreset(string Name, string Description, IReadOnlyList<int> SpellIds);

public static class BuffCatalog
{
    private static readonly Lazy<CatalogData> _data = new(LoadCatalog);

    public static IReadOnlyList<BuffDefinition> Buffs => _data.Value.Buffs;

    public static IReadOnlyList<BuffPreset> Presets => _data.Value.Presets;
	
	public static IReadOnlyList<string> Classes => _data.Value.Classes;

    public static bool TryGet(int spellId, out BuffDefinition? definition)
        => _data.Value.ById.TryGetValue(spellId, out definition);

    private static CatalogData LoadCatalog()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "FellowshipBuffs.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var doc = JsonSerializer.Deserialize<BuffCatalogDocument>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (doc != null && doc.Buffs?.Any() == true)
                {
                    var buffs = doc.Buffs
                        .Where(b => b.SpellId > 0 && !string.IsNullOrWhiteSpace(b.Name))
                        .Select(b => new BuffDefinition(
                            b.SpellId,
                            b.Name.Trim(),
                            b.Icon ?? string.Empty,
                            b.Category ?? string.Empty,
                            (b.Classes ?? new List<string>())
                                .Select(c => c?.Trim())
                                .Where(c => !string.IsNullOrWhiteSpace(c))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Select(c => c!)
                                .ToArray()))
                        .ToArray();

                    var presets = doc.Presets?.Select(p => new BuffPreset(
                        p.Name?.Trim() ?? string.Empty,
                        p.Description?.Trim() ?? string.Empty,
                        (p.SpellIds ?? new List<int>()).Where(id => id > 0).Distinct().ToArray()))
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                    .ToArray() ?? Array.Empty<BuffPreset>();

                    if (buffs.Length > 0)
                    {
                        return CatalogData.From(buffs, presets.Length > 0 ? presets : DefaultPresets);
                    }
                }
            }
        }
        catch
        {
            // fall back to defaults
        }

        return CatalogData.From(DefaultBuffs, DefaultPresets);
    }

    private static readonly BuffDefinition[] DefaultBuffs =
    {
        new(1512, "Resonance of Earth", "Icon.Buff.ResonanceOfEarth", "Earthsong Harmonies", new [] { "Meiko" }),
        new(1574, "Stone Shield", "Icon.Buff.StoneShield", "Earthsong Harmonies", new [] { "Meiko" }),
        new(2164, "Hidden Power", "Icon.Buff.HiddenPower", "Empowerments", new [] { "Meiko" }),
        new(1534, "Spirited Strikes", "Icon.Buff.SpiritedStrikes", "Spirited Techniques", new [] { "Meiko" }),
        new(1570, "Spirited Vortex", "Icon.Buff.SpiritedVortex", "Spirited Techniques", new [] { "Meiko" }),
        new(2447, "Warden of the Temple", "Icon.Buff.WardenOfTheTemple", "Spirited Techniques", new [] { "Meiko" }),
        new(3501, "Shadow Lord's Orb Collected", "Icon.Buff.ShadowLordsOrbs", "Shadow Lord's Trial", new [] { "Challenger" }),
    };

    private static readonly BuffPreset[] DefaultPresets =
    {
        new("Earthwarden Core", "Stone Shield and Resonance of Earth from the Earthwarden toolkit.", new [] { 1574, 1512 }),
        new("Empowerments", "Offensive empowerments that define burst windows.", new [] { 2164 }),
        new("Spirited Synergy", "Spirited Vortex, Spirited Strikes, and the Warden of the Temple stacks.", new [] { 1570, 1534, 2447 }),
        new("Shadow Lord's Orbs", "Counts the orb stacks needed to summon the Shadow Lord.", new [] { 3501 })
    };

    private sealed record CatalogData(
        IReadOnlyList<BuffDefinition> Buffs,
        IReadOnlyList<BuffPreset> Presets,
        IReadOnlyDictionary<int, BuffDefinition> ById,
        IReadOnlyList<string> Classes)
    {
        public static CatalogData From(IReadOnlyList<BuffDefinition> buffs, IReadOnlyList<BuffPreset> presets)
        {
            var dict = new ReadOnlyDictionary<int, BuffDefinition>(buffs.ToDictionary(b => b.SpellId));
            var classes = buffs
                .SelectMany(b => b.Classes)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new CatalogData(buffs, presets, dict, classes);
        }
    }

    private sealed class BuffCatalogDocument
    {
        public List<BuffDocument>? Buffs { get; set; }
        public List<PresetDocument>? Presets { get; set; }
    }

    private sealed class BuffDocument
    {
        public int SpellId { get; set; }
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public string? Category { get; set; }
		public List<string>? Classes { get; set; }
    }

    private sealed class PresetDocument
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public List<int>? SpellIds { get; set; }
    }
}