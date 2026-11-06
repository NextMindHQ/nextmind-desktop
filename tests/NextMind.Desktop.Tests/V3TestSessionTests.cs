using NextMind.Desktop.App;

namespace NextMind.Desktop.Tests;

public sealed class V3TestSessionTests
{
    [Fact]
    public void OpenControlCenter_IsEnabledOnlyForExplicitRedirectedV3Test()
    {
        Assert.True(V3TestSession.ShouldOpenControlCenter("C:\\isolated", "1"));
        Assert.False(V3TestSession.ShouldOpenControlCenter(null, "1"));
        Assert.False(V3TestSession.ShouldOpenControlCenter("C:\\isolated", null));
        Assert.False(V3TestSession.ShouldOpenControlCenter("C:\\isolated", "0"));
    }

    [Fact]
    public void RuntimeMarker_IdentifiesLivePreviewBuild()
    {
        Assert.Equal("NEXTMIND-DESKTOP-V3-LIVE-PREVIEW-2", V3TestSession.RuntimeMarker);
    }

    [Fact]
    public void InstanceMutex_IsIsolatedOnlyForExplicitRedirectedV3Test()
    {
        Assert.Equal(
            @"Local\NextMind.Desktop.SingleInstance.v1.V3Test",
            V3TestSession.SelectMutexName("C:\\isolated", "1"));
        Assert.Null(V3TestSession.SelectMutexName(null, "1"));
        Assert.Null(V3TestSession.SelectMutexName("C:\\isolated", null));
    }

    [Fact]
    public void IsolationSuffix_SeparatesV3TestMutexAndActivationMessageFromDevAndProduction()
    {
        Assert.Equal(".V3Test", V3TestSession.SelectIsolationSuffix("C:\\isolated", "1"));
        Assert.Equal(".dev", V3TestSession.SelectIsolationSuffix("C:\\isolated", null));
        Assert.Equal(string.Empty, V3TestSession.SelectIsolationSuffix(null, "1"));
    }
}
