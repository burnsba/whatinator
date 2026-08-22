using Whatinator.Core.Naming;

namespace Whatinator.Core.Tests;

public class FileNameSanitizerTests
{
    [Theory]
    [InlineData("A/B", "A_B")]
    [InlineData("A\\B", "A_B")]
    [InlineData("A:B", "A_B")]
    [InlineData("A*B", "A_B")]
    [InlineData("A?B", "A_B")]
    [InlineData("A\"B", "A_B")]
    [InlineData("A<B", "A_B")]
    [InlineData("A>B", "A_B")]
    [InlineData("A|B", "A_B")]
    public void Sanitize_ReplacesForbiddenCharacters(string input, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_ReplacesControlCharacters()
    {
        Assert.Equal("A_B", FileNameSanitizer.Sanitize("A\tB"));
    }

    [Fact]
    public void Sanitize_LeavesSafeCharactersUnchanged()
    {
        const string input = "Tori Amos - To Venus and Back (Disc 1 of 2)! [flac 1999]";

        Assert.Equal(input, FileNameSanitizer.Sanitize(input));
    }
}
