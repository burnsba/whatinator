using Whatinator.Core.Rip;

namespace Whatinator.Core.Tests;

public class CdParanoiaLiveOutputFilterTests
{
    [Theory]
    [InlineData("Sending all callback output to stderr for wrapper script")]
    [InlineData("cdparanoia III release 10.2 libcdio 2.2.0 x86_64-pc-linux-gnu")]
    [InlineData("(C) 2001 Monty <monty@xiph.org> and Xiphophorus")]
    [InlineData("(C) 2004, 2005, 2008 Rocky Bernstein <rocky@gnu.org>")]
    [InlineData("(C) 2014 Robert Kausch <robert.kausch@freac.org>")]
    [InlineData("Report bugs to bug-libcdio@gnu.org")]
    [InlineData("outputting to /home/bethany/Music/rip/whatinator-abc123-test.wav")]
    [InlineData("Done.")]
    [InlineData("")]
    public void Process_SuppressesKnownBoilerplateLines(string line)
    {
        Assert.Null(CdParanoiaLiveOutputFilter.Process(line));
    }

    [Theory]
    [InlineData("##: 0 [read] @ 57624")]
    [InlineData("Unable to open device: permission denied")]
    [InlineData("Warning: read offset 700 exceeds 587 samples -- cd-paranoia may misreport file sizes (known upstream bug).")]
    [InlineData("Ripping from sector      32 (track  1 [0:00.00])")]
    [InlineData("\t  to sector   16746 (track  1 [3:42.64])")]
    public void Process_PassesUnrecognizedLinesThroughUnchanged(string line)
    {
        Assert.Equal(line, CdParanoiaLiveOutputFilter.Process(line));
    }
}
