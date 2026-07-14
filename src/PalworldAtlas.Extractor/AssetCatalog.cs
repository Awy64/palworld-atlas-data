namespace PalworldAtlas.Extractor;

internal sealed record TableSpec(string Name, bool Required, params string[] PackagePaths);

internal static class AssetCatalog
{
    public static readonly TableSpec Pals = new("pals", true,
        "Pal/Content/Pal/DataTable/Character/DT_PalMonsterParameter",
        "Pal/Content/Pal/DataTable/Character/DT_PalMonsterParameter_Common");
    public static readonly TableSpec Breeding = new("breeding", true,
        "Pal/Content/Pal/DataTable/Character/DT_PalCombiUnique",
        "Pal/Content/Pal/DataTable/Character/DT_PalCombiUnique_Common");
    public static readonly TableSpec Items = new("items", true,
        "Pal/Content/Pal/DataTable/Item/DT_ItemDataTable",
        "Pal/Content/Pal/DataTable/Item/DT_ItemDataTable_Common");
    public static readonly TableSpec WildSpawners = new("wild-spawners", true,
        "Pal/Content/Pal/DataTable/Spawner/DT_PalWildSpawner");
    public static readonly TableSpec Placements = new("spawner-placements", true,
        "Pal/Content/Pal/DataTable/Spawner/DT_PalSpawnerPlacement");
    public static readonly TableSpec Alphas = new("alpha-spawners", true,
        "Pal/Content/Pal/DataTable/UI/DT_BossSpawnerLoactionData");
    public static readonly TableSpec MapMetadata = new("map-metadata", false,
        "Pal/Content/Pal/DataTable/WorldMapUIData/DT_WorldMapUIData");
    public static readonly TableSpec PalNames = new("pal-names", false,
        "Pal/Content/L10N/en/Pal/DataTable/Text/DT_PalNameText_Common");
    public static readonly TableSpec PalDescriptions = new("pal-descriptions", false,
        "Pal/Content/L10N/en/Pal/DataTable/Text/DT_PalLongDescriptionText_Common");
    public static readonly TableSpec ItemNames = new("item-names", false,
        "Pal/Content/L10N/en/Pal/DataTable/Text/DT_ItemNameText_Common");
    public static readonly TableSpec ItemDescriptions = new("item-descriptions", false,
        "Pal/Content/L10N/en/Pal/DataTable/Text/DT_ItemDescriptionText_Common");

    public static readonly TableSpec[] All =
    [
        Pals, Breeding, Items, WildSpawners, Placements, Alphas, MapMetadata,
        PalNames, PalDescriptions, ItemNames, ItemDescriptions
    ];
}

