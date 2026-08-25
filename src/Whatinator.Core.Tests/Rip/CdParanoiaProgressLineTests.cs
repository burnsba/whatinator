using Whatinator.Core.Rip;

namespace Whatinator.Core.Tests;

public class CdParanoiaProgressLineTests
{
    [Theory]
    [InlineData("##: 0 [read] @ 57624", "read", 57624)]
    [InlineData("##: 14 [wrote] @ 298703", "wrote", 298703)]
    [InlineData("##: 15 [finished] @ 303407", "finished", 303407)]
    public void TryParse_ParsesRealCapturedLines(string line, string expectedFunction, int expectedWordOffset)
    {
        var parsed = CdParanoiaProgressLine.TryParse(line, out var function, out var wordOffset);

        Assert.True(parsed);
        Assert.Equal(expectedFunction, function);
        Assert.Equal(expectedWordOffset, wordOffset);
    }

    [Theory]
    [InlineData("this is not a progress line")]
    [InlineData("Ripping from sector      32 (track  1 [0:00.00])")]
    [InlineData("")]
    public void TryParse_ReturnsFalse_ForNonProgressLines(string line)
    {
        var parsed = CdParanoiaProgressLine.TryParse(line, out var function, out var wordOffset);

        Assert.False(parsed);
        Assert.Equal(string.Empty, function);
        Assert.Equal(0, wordOffset);
    }

    [Fact]
    public void TryParse_ReturnsFalse_WhenOffsetExceedsIntMaxValue()
    {
        var parsed = CdParanoiaProgressLine.TryParse("##: 0 [read] @ 99999999999999999999", out var function, out var wordOffset);

        Assert.False(parsed);
        Assert.Equal(string.Empty, function);
        Assert.Equal(0, wordOffset);
    }

    [Fact]
    public void TryParse_ReturnsFalse_ForTruncatedLine()
    {
        // Simulates a line cut off mid-write, as can happen when
        // cd-paranoia's progress output interleaves with other stderr text
        // (see root CLAUDE.md § Gotchas) -- no offset digits present at all.
        var parsed = CdParanoiaProgressLine.TryParse("##: 0 [read] @ ", out var function, out var wordOffset);

        Assert.False(parsed);
        Assert.Equal(string.Empty, function);
        Assert.Equal(0, wordOffset);
    }
}
