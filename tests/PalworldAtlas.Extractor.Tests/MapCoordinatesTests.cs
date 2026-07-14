using PalworldAtlas.Extractor;
using Xunit;

namespace PalworldAtlas.Extractor.Tests;

public sealed class MapCoordinatesTests
{
    [Fact]
    public void ConvertsKnownAnubisWorldCoordinate()
    {
        var result = MapCoordinates.ToPalpagos(-167230, 96430);
        Assert.Equal(-134, Math.Round(result.X));
        Assert.Equal(-94, Math.Round(result.Y));
    }

    [Fact]
    public void PalpagosExtentContainsKnownCoordinate()
    {
        var result = MapCoordinates.ToPalpagos(-167230, 96430);
        Assert.InRange(result.X, MapCoordinates.PalpagosExtent[0], MapCoordinates.PalpagosExtent[2]);
        Assert.InRange(result.Y, MapCoordinates.PalpagosExtent[1], MapCoordinates.PalpagosExtent[3]);
    }
}
