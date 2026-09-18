using TaskDock.Services;

namespace TaskDock.Tests;

public sealed class ResourceLauncherTests
{
    [Fact]
    public void FindWebUri_ExtractsFirstAddressAndTrimsChinesePunctuation()
    {
        var uri = ResourceLauncher.FindWebUri("相关说明：https://example.com/docs/test）。后续文字");

        Assert.NotNull(uri);
        Assert.Equal("https://example.com/docs/test", uri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("example.com/path", "https://example.com/path")]
    [InlineData("http://example.com", "http://example.com/")]
    public void NormalizeWebUri_AcceptsCommonWebAddresses(string input, string expected)
    {
        Assert.Equal(expected, ResourceLauncher.NormalizeWebUri(input)?.AbsoluteUri);
    }

    [Theory]
    [InlineData(0, 500, "Left")]
    [InlineData(1000, 0, "Top")]
    [InlineData(1000, 1079, "Bottom")]
    [InlineData(1919, 500, "Right")]
    public void IsAtEdge_DetectsConfiguredScreenEdge(int x, int y, string edge)
    {
        Assert.True(ScreenEdgeDetector.IsAtEdge(x, y, 0, 0, 1920, 1080, edge));
    }

    [Fact]
    public void IsAtEdge_DoesNotMatchDifferentEdge()
    {
        Assert.False(ScreenEdgeDetector.IsAtEdge(1919, 500, 0, 0, 1920, 1080, "Left"));
    }
}
