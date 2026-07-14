using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace PalworldAtlas.Extractor;

internal sealed class AtlasPublisher(PakWorkspace workspace)
{
    private sealed record SpawnDefinition(string SourceId, string PalId, string Availability, int MinLevel, int MaxLevel, double Weight);
    private sealed record Placement(string Id, string SpawnerId, MapRegion Region, double WorldX, double WorldY);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static string StripEnum(string value) => value.Split("::").LastOrDefault() ?? value;
    private static string NormalizePalId(string value)
    {
        var result = StripEnum(value);
        foreach (var prefix in new[] { "BOSS_", "Boss_", "RAID_", "Raid_", "GYM_", "Gym_" })
            if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) result = result[prefix.Length..];
        return result;
    }

    private static Dictionary<string, string> ReadText(PakWorkspace workspace, TableSpec spec, params string[] prefixes)
    {
        var table = workspace.LoadFirst(spec);
        if (table is null) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.RowMap)
        {
            var key = row.Key.Text;
            foreach (var prefix in prefixes)
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) key = key[prefix.Length..];
            var text = new RowReader(row.Value).String("", "TextData", "Text", "Value").Trim();
            if (text.Length > 0) result[key] = text;
        }
        return result;
    }

    public void Publish(string outputDirectory, string buildId, string? gameVersion, string? previousManifestPath)
    {
        var statuses = AssetCatalog.All.Select(workspace.Probe).ToArray();
        var requiredNames = AssetCatalog.All.Where(spec => spec.Required).Select(spec => spec.Name).ToHashSet();
        var failed = statuses.Where(status => requiredNames.Contains(status.Name) && (!status.Parsed || status.RowCount == 0)).ToArray();
        if (failed.Length > 0)
            throw new InvalidOperationException($"Production gate failed: {string.Join(", ", failed.Select(status => status.Name))}");

        var palNames = ReadText(workspace, AssetCatalog.PalNames, "PAL_NAME_");
        var palDescriptions = ReadText(workspace, AssetCatalog.PalDescriptions, "PAL_LONG_DESCRIPTION_", "PAL_LONG_DESC_", "PAL_DESCRIPTION_");
        var itemNames = ReadText(workspace, AssetCatalog.ItemNames, "ITEM_NAME_");
        var itemDescriptions = ReadText(workspace, AssetCatalog.ItemDescriptions, "ITEM_DESCRIPTION_", "ITEM_DESC_");

        var pals = ReadPals(palNames, palDescriptions);
        var items = ReadItems(itemNames, itemDescriptions);
        var breeding = ReadBreeding();
        var mapDefinitions = ReadMapDefinitions();
        var spawns = ReadSpawns(pals, mapDefinitions);

        Validate(pals, items, breeding, spawns, previousManifestPath);

        var buildDirectory = Path.Combine(outputDirectory, "v1", "builds", buildId);
        Directory.CreateDirectory(Path.Combine(buildDirectory, "pals"));
        Directory.CreateDirectory(Path.Combine(buildDirectory, "items"));
        Directory.CreateDirectory(Path.Combine(buildDirectory, "maps", "palpagos"));
        Directory.CreateDirectory(Path.Combine(buildDirectory, "maps", "tree"));

        var checksums = new Dictionary<string, string>();
        Write(Path.Combine(buildDirectory, "pals", "index.json"), new { records = pals }, checksums, "pals");
        foreach (var pal in pals)
            Write(Path.Combine(buildDirectory, "pals", $"{SafeName(pal.Id)}.json"), pal, null, null);
        Write(Path.Combine(buildDirectory, "items", "index.json"), new { records = items }, checksums, "items");
        Write(Path.Combine(buildDirectory, "breeding.json"), breeding, checksums, "breeding");

        foreach (var region in Enum.GetValues<MapRegion>())
        {
            var definition = mapDefinitions.GetValueOrDefault(region) ?? DefaultMapDefinition(region);
            var collection = new SpawnCollection(region, "pal-map", definition.Extent,
                spawns.Where(spawn => spawn.Region == region).OrderBy(spawn => spawn.PalId).ThenBy(spawn => spawn.Id).ToArray());
            var key = region.ToString().ToLowerInvariant();
            Write(Path.Combine(buildDirectory, "maps", key, "spawns.json"), collection, checksums, $"map-{key}");
        }

        var counts = new Dictionary<string, int>
        {
            ["pals"] = pals.Count,
            ["items"] = items.Count,
            ["uniqueBreedingPairs"] = breeding.UniquePairs.Count,
            ["wildSpawns"] = spawns.Count(spawn => spawn.Kind == "wild"),
            ["alphaSpawns"] = spawns.Count(spawn => spawn.Kind == "alpha"),
            ["palpagosSpawns"] = spawns.Count(spawn => spawn.Region == MapRegion.Palpagos),
            ["treeSpawns"] = spawns.Count(spawn => spawn.Region == MapRegion.Tree),
        };
        var compatibility = new Dictionary<string, bool>
        {
            ["serverTables"] = true,
            ["exactSpawns"] = counts["wildSpawns"] > 0,
            ["alphaSpawns"] = counts["alphaSpawns"] > 0,
            ["treeMap"] = mapDefinitions.ContainsKey(MapRegion.Tree) && counts["treeSpawns"] > 0,
            ["nativePaldexHabitat"] = statuses.Single(status => status.Name == "map-metadata").Parsed,
        };
        var generatedAt = DateTimeOffset.UtcNow;
        var manifest = new BuildManifest(1, buildId, generatedAt, counts, compatibility, statuses, checksums);
        Write(Path.Combine(buildDirectory, "manifest.json"), manifest, null, null);

        var latestDirectory = Path.Combine(outputDirectory, "v1");
        Directory.CreateDirectory(latestDirectory);
        Write(Path.Combine(latestDirectory, "latest.json"),
            new AtlasLatest(1, buildId, generatedAt, gameVersion, $"builds/{buildId}"), null, null);
    }

    private List<AtlasPal> ReadPals(Dictionary<string, string> names, Dictionary<string, string> descriptions)
    {
        var result = new Dictionary<string, AtlasPal>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in workspace.LoadAll(AssetCatalog.Pals))
        foreach (var row in table.RowMap)
        {
            var sourceId = row.Key.Text;
            var reader = new RowReader(row.Value);
            if (!reader.Bool(false, "IsPal") || reader.Bool(false, "IsBoss") || reader.Bool(false, "IsTowerBoss") || reader.Bool(false, "IsRaidBoss")) continue;
            if (sourceId.Contains("Quest", StringComparison.OrdinalIgnoreCase) ||
                sourceId.Contains("PREDATOR", StringComparison.OrdinalIgnoreCase) ||
                sourceId.StartsWith("SUMMON_", StringComparison.OrdinalIgnoreCase) ||
                sourceId.Contains("_Oilrig", StringComparison.OrdinalIgnoreCase)) continue;
            var paldex = reader.Int(-1, "ZukanIndex", "PalDexNum");
            var rarity = reader.Int(0, "Rarity");
            var breedRank = reader.Int(0, "CombiRank", "BreedingRank");
            if (paldex < 0 || rarity <= 0 || breedRank <= 0) continue;

            var tribe = StripEnum(reader.String(sourceId, "Tribe"));
            var nameId = reader.String(sourceId, "OverrideNameTextId").Replace("PAL_NAME_", "", StringComparison.OrdinalIgnoreCase);
            var name = names.GetValueOrDefault(sourceId) ?? names.GetValueOrDefault(nameId) ?? names.GetValueOrDefault(tribe) ?? sourceId;
            var elements = new[]
            {
                StripEnum(reader.String("None", "ElementType1", "Element1")),
                StripEnum(reader.String("None", "ElementType2", "Element2")),
            }.Where(element => !element.Equals("None", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (elements.Length == 0) elements = ["Normal"];
            var work = new Dictionary<string, int>();
            foreach (var key in new[] { "EmitFlame", "Watering", "Seeding", "GenerateElectricity", "Handcraft", "Collection", "Deforest", "Mining", "OilExtraction", "ProductMedicine", "Cool", "Transport", "MonsterFarm" })
            {
                var value = reader.Int(0, $"WorkSuitability_{key}");
                if (value > 0) work[key] = value;
            }
            result[sourceId] = new AtlasPal(
                sourceId, tribe, name, descriptions.GetValueOrDefault(sourceId) ?? descriptions.GetValueOrDefault(nameId),
                paldex, rarity, elements,
                reader.Int(0, "Hp", "HP"), reader.Int(0, "ShotAttack", "Attack"), reader.Int(0, "Defense"),
                reader.Int(0, "RunSpeed"), reader.Int(0, "Stamina"), reader.Int(0, "FoodAmount"),
                breedRank, reader.Bool(false, "Nocturnal"), work);
        }
        return result.Values.OrderBy(pal => pal.PaldexNumber).ThenBy(pal => pal.Name).ToList();
    }

    private List<AtlasItem> ReadItems(Dictionary<string, string> names, Dictionary<string, string> descriptions)
    {
        var result = new Dictionary<string, AtlasItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in workspace.LoadAll(AssetCatalog.Items))
        foreach (var row in table.RowMap)
        {
            var id = row.Key.Text;
            var reader = new RowReader(row.Value);
            if (reader.Bool(false, "Disabled", "bDisabled")) continue;
            if (!reader.Bool(true, "bLegalInGame")) continue;
            var nameOverride = reader.String("", "OverrideNameTextId", "NameTextId").Replace("ITEM_NAME_", "", StringComparison.OrdinalIgnoreCase);
            var descriptionOverride = reader.String("", "OverrideDescription", "DescriptionTextId").Replace("ITEM_DESC_", "", StringComparison.OrdinalIgnoreCase);
            result[id] = new AtlasItem(
                id,
                names.GetValueOrDefault(id) ?? names.GetValueOrDefault(nameOverride) ?? id.Replace('_', ' '),
                descriptions.GetValueOrDefault(id) ?? descriptions.GetValueOrDefault(descriptionOverride),
                StripEnum(reader.String("Other", "TypeA", "Group", "ItemGroup")),
                StripEnum(reader.String("Other", "TypeB", "Subcategory")),
                reader.Int(0, "Rarity"), reader.Int(0, "Rank"), reader.Int(1, "MaxStackCount", "MaxStack"),
                reader.Number(0, "Weight"), reader.Int(0, "Price"), reader.String("", "Icon", "IconName"));
        }
        return result.Values.OrderBy(item => item.Category).ThenBy(item => item.Name).ToList();
    }

    private BreedingContract ReadBreeding()
    {
        var pairs = new List<UniqueBreedingPair>();
        foreach (var table in workspace.LoadAll(AssetCatalog.Breeding))
        foreach (var row in table.RowMap.Values)
        {
            var reader = new RowReader(row);
            var parentA = StripEnum(reader.String("", "ParentTribeA", "ParentA"));
            var parentB = StripEnum(reader.String("", "ParentTribeB", "ParentB"));
            var child = NormalizePalId(reader.String("", "ChildCharacterID", "Child"));
            if (parentA.Length == 0 || parentB.Length == 0 || child.Length == 0) continue;
            static string? Gender(string value)
            {
                var normalized = StripEnum(value).ToLowerInvariant();
                return normalized is "male" or "female" ? normalized : null;
            }
            pairs.Add(new UniqueBreedingPair(parentA, parentB, child,
                Gender(reader.String("", "ParentGenderA")), Gender(reader.String("", "ParentGenderB"))));
        }
        return new BreedingContract(true, pairs
            .DistinctBy(pair => $"{pair.ParentAId}|{pair.ParentBId}|{pair.ChildId}|{pair.ParentAGender}|{pair.ParentBGender}")
            .ToArray());
    }

    private Dictionary<MapRegion, MapDefinition> ReadMapDefinitions()
    {
        var result = new Dictionary<MapRegion, MapDefinition>();
        var table = workspace.LoadFirst(AssetCatalog.MapMetadata);
        if (table is null) return result;
        foreach (var row in table.RowMap)
        {
            var reader = new RowReader(row.Value);
            var min = reader.Coordinates("landScapeRealPositionMin", "LandscapeRealPositionMin");
            var max = reader.Coordinates("landScapeRealPositionMax", "LandscapeRealPositionMax");
            if (min is null || max is null) continue;
            var region = row.Key.Text.Contains("Tree", StringComparison.OrdinalIgnoreCase) ? MapRegion.Tree : MapRegion.Palpagos;
            result[region] = new MapDefinition(region, min.Value.X, min.Value.Y, max.Value.X, max.Value.Y);
        }
        return result;
    }

    private List<AtlasSpawn> ReadSpawns(IReadOnlyList<AtlasPal> pals, Dictionary<MapRegion, MapDefinition> mapDefinitions)
    {
        var palById = pals.SelectMany(pal => new[] { pal.Id, pal.Tribe }.Select(id => (id: NormalizePalId(id), pal)))
            .GroupBy(entry => entry.id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().pal, StringComparer.OrdinalIgnoreCase);
        var definitions = new Dictionary<string, List<SpawnDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in workspace.LoadAll(AssetCatalog.WildSpawners))
        foreach (var row in table.RowMap)
        {
            var reader = new RowReader(row.Value);
            var availability = StripEnum(reader.String("Undefined", "OnlyTime", "TimeType")) switch
            {
                var value when value.Contains("Night", StringComparison.OrdinalIgnoreCase) => "night",
                var value when value.Contains("Day", StringComparison.OrdinalIgnoreCase) => "day",
                _ => "both",
            };
            var groupWeight = reader.Number(1, "SpawnWeight");
            var keys = new[] { row.Key.Text, reader.String(row.Key.Text, "SpawnerName", "SpawnerID") }
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            for (var index = 1; index <= 8; index++)
            {
                var rawPalId = reader.String("None", $"Pal_{index}", $"Pal{index}", $"CharacterID_{index}");
                if (rawPalId.Equals("None", StringComparison.OrdinalIgnoreCase)) continue;
                var definition = new SpawnDefinition(row.Key.Text, NormalizePalId(rawPalId), availability,
                    reader.Int(0, $"LvMin_{index}", $"MinLevel_{index}"),
                    reader.Int(0, $"LvMax_{index}", $"MaxLevel_{index}"),
                    PositiveWeight(reader.NumberAt(index - 1, double.NaN, "Weight"),
                        reader.Number(groupWeight, $"Weight_{index}", $"SpawnWeight_{index}")));
                foreach (var key in keys)
                {
                    if (!definitions.TryGetValue(key, out var list)) definitions[key] = list = [];
                    list.Add(definition);
                }
            }
        }

        var placements = new List<Placement>();
        foreach (var table in workspace.LoadAll(AssetCatalog.Placements))
        foreach (var row in table.RowMap)
        {
            var reader = new RowReader(row.Value);
            var coordinates = reader.Coordinates("Transform", "Location", "WorldLocation", "Position", "SpawnerTransform");
            if (coordinates is null) continue;
            var mapName = reader.String("", "MapName", "WorldName", "LevelName", "Region");
            var treeDefinition = mapDefinitions.GetValueOrDefault(MapRegion.Tree);
            var region = mapName.Contains("Tree", StringComparison.OrdinalIgnoreCase) ||
                         row.Key.Text.Contains("Tree", StringComparison.OrdinalIgnoreCase) ||
                         treeDefinition?.Contains(coordinates.Value.X, coordinates.Value.Y) == true
                ? MapRegion.Tree : MapRegion.Palpagos;
            placements.Add(new Placement(row.Key.Text,
                reader.String(row.Key.Text, "SpawnerID", "SpawnerName", "SpawnerDataID", "WildSpawnerID"),
                region, coordinates.Value.X, coordinates.Value.Y));
        }

        var result = new List<AtlasSpawn>();
        foreach (var placement in placements)
        {
            if (!definitions.TryGetValue(placement.SpawnerId, out var matches) && !definitions.TryGetValue(placement.Id, out matches)) continue;
            foreach (var (definition, index) in matches.Select((value, index) => (value, index)))
            {
                if (!palById.TryGetValue(definition.PalId, out var pal)) continue;
                var map = MapCoordinates.ToMap(placement.Region, placement.WorldX, placement.WorldY, mapDefinitions.GetValueOrDefault(placement.Region));
                result.Add(new AtlasSpawn($"wild-{placement.Id}-{index}-{pal.Id}", pal.Id, pal.Name, placement.Region,
                    "wild", placement.WorldX, placement.WorldY, map.X, map.Y, definition.Availability,
                    definition.MinLevel, definition.MaxLevel, definition.Weight));
            }
        }

        var placementBySpawner = placements
            .GroupBy(placement => placement.SpawnerId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var table in workspace.LoadAll(AssetCatalog.Alphas))
        foreach (var row in table.RowMap)
        {
            var reader = new RowReader(row.Value);
            var palId = NormalizePalId(reader.String("", "CharacterID", "PalID"));
            if (!palById.TryGetValue(palId, out var pal)) continue;
            var spawnerId = reader.String(row.Key.Text, "SpawnerID", "SpawnerName");
            var direct = reader.Coordinates("Transform", "Location", "WorldLocation", "Position");
            var linked = placementBySpawner.GetValueOrDefault(spawnerId);
            if (direct is null && linked is null) continue;
            var worldX = direct?.X ?? linked!.WorldX;
            var worldY = direct?.Y ?? linked!.WorldY;
            var treeDefinition = mapDefinitions.GetValueOrDefault(MapRegion.Tree);
            var region = linked?.Region ??
                (row.Key.Text.Contains("Tree", StringComparison.OrdinalIgnoreCase) || treeDefinition?.Contains(worldX, worldY) == true
                    ? MapRegion.Tree : MapRegion.Palpagos);
            var map = MapCoordinates.ToMap(region, worldX, worldY, mapDefinitions.GetValueOrDefault(region));
            var level = reader.Int(0, "Level", "BossLevel", "Lv");
            result.Add(new AtlasSpawn($"alpha-{row.Key.Text}-{pal.Id}", pal.Id, pal.Name, region, "alpha",
                worldX, worldY, map.X, map.Y, "both", level, level, 1));
        }
        return result.DistinctBy(spawn => spawn.Id).ToList();
    }

    private static double PositiveWeight(double preferred, double fallback) =>
        double.IsFinite(preferred) && preferred > 0
            ? preferred
            : double.IsFinite(fallback) && fallback > 0 ? fallback : 1;

    private static void Validate(
        IReadOnlyList<AtlasPal> pals,
        IReadOnlyList<AtlasItem> items,
        BreedingContract breeding,
        IReadOnlyList<AtlasSpawn> spawns,
        string? previousManifestPath)
    {
        if (pals.Count == 0 || items.Count == 0 || breeding.UniquePairs.Count == 0)
            throw new InvalidDataException("Core Pal, item, or breeding output is empty");
        if (!spawns.Any(spawn => spawn.Kind == "wild") || !spawns.Any(spawn => spawn.Kind == "alpha"))
            throw new InvalidDataException("Exact wild and alpha spawn outputs are required");
        if (spawns.Any(spawn => spawn.Region == MapRegion.Tree) == false)
            throw new InvalidDataException("World Tree metadata is present but no Tree spawn coordinates were produced");
        if (pals.Select(pal => pal.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != pals.Count)
            throw new InvalidDataException("Duplicate Pal IDs detected");
        if (items.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count)
            throw new InvalidDataException("Duplicate item IDs detected");
        var palIds = pals.Select(pal => pal.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = spawns.Where(spawn => !palIds.Contains(spawn.PalId)).Select(spawn => spawn.PalId).Distinct().Take(8).ToArray();
        if (unknown.Length > 0) throw new InvalidDataException($"Spawn records reference unknown Pals: {string.Join(", ", unknown)}");
        if (spawns.Any(spawn => !double.IsFinite(spawn.MapX) || !double.IsFinite(spawn.MapY)))
            throw new InvalidDataException("Invalid spawn coordinates detected");

        if (string.IsNullOrWhiteSpace(previousManifestPath) || !File.Exists(previousManifestPath)) return;
        using var previousDocument = JsonDocument.Parse(File.ReadAllText(previousManifestPath));
        var previousCounts = previousDocument.RootElement.GetProperty("counts");
        var comparisons = new Dictionary<string, int>
        {
            ["pals"] = pals.Count,
            ["items"] = items.Count,
            ["uniqueBreedingPairs"] = breeding.UniquePairs.Count,
            ["wildSpawns"] = spawns.Count(spawn => spawn.Kind == "wild"),
            ["alphaSpawns"] = spawns.Count(spawn => spawn.Kind == "alpha"),
        };
        foreach (var (key, current) in comparisons)
        {
            if (!previousCounts.TryGetProperty(key, out var oldValue)) continue;
            var previous = oldValue.GetInt32();
            if (previous > 0 && current < previous * 0.8)
                throw new InvalidDataException($"{key} dropped unexpectedly from {previous} to {current}");
        }
    }

    private static MapDefinition DefaultMapDefinition(MapRegion region) => region == MapRegion.Palpagos
        ? new MapDefinition(region, -999940, -738920, 447900, 708920)
        : new MapDefinition(region, 0, 0, 2048, 2048);

    private static string SafeName(string value) => string.Concat(value.Select(character =>
        char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));

    private static void Write<T>(string path, T value, IDictionary<string, string>? checksums, string? checksumKey)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        File.WriteAllBytes(path, bytes);
        if (checksums is not null && checksumKey is not null)
            checksums[checksumKey] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
