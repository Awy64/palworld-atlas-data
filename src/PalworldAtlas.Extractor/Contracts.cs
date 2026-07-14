using System.Text.Json.Serialization;

namespace PalworldAtlas.Extractor;

public sealed record AtlasLatest(
    int SchemaVersion,
    string SteamBuildId,
    DateTimeOffset GeneratedAt,
    string? GameVersion,
    string BuildPath);

public sealed record AtlasPal(
    string Id,
    string Tribe,
    string Name,
    string? Description,
    int PaldexNumber,
    int Rarity,
    IReadOnlyList<string> Elements,
    int Hp,
    int Attack,
    int Defense,
    int RunSpeed,
    int Stamina,
    int Food,
    int BreedingRank,
    bool Nocturnal,
    IReadOnlyDictionary<string, int> WorkSuitability);

public sealed record AtlasItem(
    string Id,
    string Name,
    string? Description,
    string Category,
    string? Subcategory,
    int Rarity,
    int Rank,
    int MaxStack,
    double Weight,
    int Price,
    string? Icon);

public sealed record UniqueBreedingPair(
    string ParentAId,
    string ParentBId,
    string ChildId,
    string? ParentAGender,
    string? ParentBGender);

public sealed record BreedingContract(
    bool SameSpeciesProducesSelf,
    IReadOnlyList<UniqueBreedingPair> UniquePairs);

[JsonConverter(typeof(JsonStringEnumConverter<MapRegion>))]
public enum MapRegion { Palpagos, Tree }

public sealed record AtlasSpawn(
    string Id,
    string PalId,
    string? PalName,
    MapRegion Region,
    string Kind,
    double WorldX,
    double WorldY,
    double MapX,
    double MapY,
    string Availability,
    int MinLevel,
    int MaxLevel,
    double Weight);

public sealed record SpawnCollection(
    MapRegion Region,
    string CoordinateSystem,
    double[] Extent,
    IReadOnlyList<AtlasSpawn> Spawns);

public sealed record SourceTableStatus(
    string Name,
    string? PackagePath,
    bool Present,
    bool Parsed,
    int RowCount,
    IReadOnlyList<string> SampleFields,
    string? Error);

public sealed record BuildManifest(
    int SchemaVersion,
    string SteamBuildId,
    DateTimeOffset GeneratedAt,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyDictionary<string, bool> Compatibility,
    IReadOnlyList<SourceTableStatus> SourceTables,
    IReadOnlyDictionary<string, string> Checksums);

public sealed record ProbeReport(
    string SteamBuildId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long PakBytes,
    long PeakWorkingSetBytes,
    bool MappingsProvided,
    bool ProductionGatePassed,
    IReadOnlyList<SourceTableStatus> Tables,
    IReadOnlyList<string> Notes);

internal sealed record MapDefinition(MapRegion Region, double MinX, double MinY, double MaxX, double MaxY)
{
    public double[] Extent => Region == MapRegion.Palpagos
        ? MapCoordinates.PalpagosExtent
        : [MinY, MinX, MaxY, MaxX];
}

public static class MapCoordinates
{
    public static readonly double[] PalpagosExtent = [-1954.07407407, -1908.61002179, 1200.26143791, 1245.7254902];

    public static (double X, double Y) ToPalpagos(double worldX, double worldY) =>
        ((worldY - 158000d) / 459d, (worldX + 123888d) / 459d);

    internal static (double X, double Y) ToMap(MapRegion region, double worldX, double worldY, MapDefinition? definition) =>
        region == MapRegion.Palpagos ? ToPalpagos(worldX, worldY) : (worldY, worldX);
}

