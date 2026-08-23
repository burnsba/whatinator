namespace Whatinator.Core.Tests;

public class WhatinatorUserAgentTests
{
    [Fact]
    public void Default_MatchesExpectedShape_WithCurrentVersionAndContactEmail()
    {
        Assert.Equal(
            $"whatinator/{WhatinatorVersion.Current} ( {WhatinatorUserAgent.DefaultContactEmail} )",
            WhatinatorUserAgent.Default);
    }
}
