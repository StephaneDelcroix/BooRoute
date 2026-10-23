namespace BooRoute.Core.Tests;

public sealed class DemoMapTests
{
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
}
