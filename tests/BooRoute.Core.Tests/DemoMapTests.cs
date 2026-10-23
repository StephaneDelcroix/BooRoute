namespace BooRoute.Core.Tests;

public sealed class DemoMapTests
{
    [Fact]
    public void EmptyPositionsStayEmpty() =>
        Assert.Empty(DemoMap.SeparateDuplicates([]));

    [Fact]
    public void UniquePositionsAreUnchanged()
    {
        (double, double)[] positions = [(50.93, 6.96), (50.92, 6.97)];
        Assert.Equal(positions, DemoMap.SeparateDuplicates(positions));
    }

    [Fact]
    public void SharedPositionsAreNudgedApartButStayClose()
    {
        (double, double)[] positions = [(50.9334, 6.9548), (50.9334, 6.9548), (50.9334, 6.9548)];
        var separated = DemoMap.SeparateDuplicates(positions);

        Assert.Equal(positions[0], separated[0]);
        Assert.Equal(3, separated.Distinct().Count());
        Assert.All(separated, position =>
        {
            Assert.InRange(position.Latitude, 50.9334 - 0.001, 50.9334 + 0.001);
            Assert.InRange(position.Longitude, 6.9548 - 0.001, 6.9548 + 0.001);
        });
    }

    [Fact]
    public void NonconsecutiveDuplicatesDoNotMoveOtherPositions()
    {
        (double, double)[] positions = [(50.93, 6.96), (50.92, 6.97), (50.93, 6.96)];

        var separated = DemoMap.SeparateDuplicates(positions);

        Assert.Equal(positions[0], separated[0]);
        Assert.Equal(positions[1], separated[1]);
        Assert.NotEqual(positions[2], separated[2]);
        Assert.Equal(3, separated.Distinct().Count());
    }

    [Fact]
    public void PositionsWithinSixDecimalRoundingAreSeparated()
    {
        (double, double)[] positions = [(50.9334001, 6.9548001), (50.9334002, 6.9548002)];

        var separated = DemoMap.SeparateDuplicates(positions);

        Assert.Equal(positions[0], separated[0]);
        Assert.NotEqual(positions[1], separated[1]);
        Assert.Equal(2, separated.Distinct().Count());
    }
}
