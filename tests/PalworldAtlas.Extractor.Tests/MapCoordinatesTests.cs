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

    [Fact]
    public void DetectsWorldTreeCoordinatesFromServerMapBounds()
    {
        Assert.True(MapCoordinates.IsWithinWorldExtent(
            539955, -583230,
            347351.5, -818197,
            689148.5, -476400));
        Assert.False(MapCoordinates.IsWithinWorldExtent(
            -167230, 96430,
            347351.5, -818197,
            689148.5, -476400));
    }
}
