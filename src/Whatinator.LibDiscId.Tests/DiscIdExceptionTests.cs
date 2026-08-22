namespace Whatinator.LibDiscId.Tests;

public class DiscIdExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var exception = new DiscIdException("no disc in drive");

        Assert.Equal("no disc in drive", exception.Message);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsBoth()
    {
        var inner = new InvalidOperationException("native failure");

        var exception = new DiscIdException("wrapped", inner);

        Assert.Equal("wrapped", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
