using System.Text.Json;
using Xunit;

namespace PalworldAtlas.Extractor.Tests;

public sealed class ContractFixtureTests
{
    [Fact]
    public void FixtureCoversRequiredRecordKinds()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "normalized-build.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var pals = root.GetProperty("pals").EnumerateArray().ToArray();
        var spawns = root.GetProperty("spawns").EnumerateArray().ToArray();

        Assert.Contains(pals, pal => pal.GetProperty("id").GetString() == "SheepBall");
        Assert.Contains(pals, pal => pal.GetProperty("id").GetString()!.EndsWith("_B"));
        Assert.Contains(spawns, spawn => spawn.GetProperty("availability").GetString() == "day");
        Assert.Contains(spawns, spawn => spawn.GetProperty("availability").GetString() == "night");
        Assert.Contains(spawns, spawn => spawn.GetProperty("kind").GetString() == "alpha");
        Assert.Contains(spawns, spawn => spawn.GetProperty("region").GetString() == "tree");
        Assert.NotEmpty(root.GetProperty("uniquePairs").EnumerateArray());
        Assert.NotEmpty(root.GetProperty("items").EnumerateArray());
    }
}
