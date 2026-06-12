using SlideDevPresenter.Core;
using SlideDevPresenter.Infrastructure;

namespace SlideDevPresenter.Tests;

public class SolutionSmokeTests
{
    [Fact]
    public void CoreAndInfrastructureAssemblies_AreLoadable()
    {
        Assert.Equal("SlideDevPresenter.Core", typeof(CoreAssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("SlideDevPresenter.Infrastructure", typeof(InfrastructureAssemblyMarker).Assembly.GetName().Name);
    }
}
