using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Fellowship_overlay.Core;

public sealed record BuffDefinition(int SpellId, string Name, string IconResourceKey, string Category);

public sealed record BuffPreset(string Name, string Description, IReadOnlyList<int> SpellIds);

public static class BuffCatalog
{
    private static readonly Lazy<CatalogData> _data = new(LoadCatalog);

    public static IReadOnlyList<BuffDefinition> Buffs => _data.Value.Buffs;

    public static IReadOnlyList<BuffPreset> Presets => _data.Value.Presets;

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
                        .Select(b => new BuffDefinition(b.SpellId, b.Name.Trim(), b.Icon ?? string.Empty, b.Category ?? string.Empty))
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
        new(1512, "Resonance of Earth", "Icon.Buff.ResonanceOfEarth", "Earthsong Harmonies"),
        new(1574, "Stone Shield", "Icon.Buff.StoneShield", "Earthsong Harmonies"),
        new(2164, "Hidden Power", "Icon.Buff.HiddenPower", "Empowerments"),
    };

    private static readonly BuffPreset[] DefaultPresets =
    {
        new("Earthwarden Core", "Stone Shield and Resonance of Earth from the Earthwarden toolkit.", new [] { 1574, 1512 }),
        new("Empowerments", "Offensive empowerments that define burst windows.", new [] { 2164 })
    };

    private sealed record CatalogData(
        IReadOnlyList<BuffDefinition> Buffs,
        IReadOnlyList<BuffPreset> Presets,
        IReadOnlyDictionary<int, BuffDefinition> ById)
    {
        public static CatalogData From(IReadOnlyList<BuffDefinition> buffs, IReadOnlyList<BuffPreset> presets)
        {
            var dict = new ReadOnlyDictionary<int, BuffDefinition>(buffs.ToDictionary(b => b.SpellId));
            return new CatalogData(buffs, presets, dict);
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
    }

    private sealed class PresetDocument
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public List<int>? SpellIds { get; set; }
    }
}