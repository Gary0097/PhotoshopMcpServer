using FluentAssertions;
using PhotoshopMcpServer.Tools;
using Xunit;

namespace PhotoshopMcpServer.Tests;

public class DuanxingToolCatalogTests
{
    [Fact]
    public void ProductionMode_OnlyShowsChineseBusinessWorkflowTools()
    {
        var toolTypes = DuanxingToolCatalog.GetToolTypes(includeDeveloperTools: false);

        toolTypes.Should().Contain(typeof(EnvironmentTools));
        toolTypes.Should().Contain(typeof(DuanxingWorkflowTools));
        toolTypes.Should().Contain(typeof(DuanxingQuickActionTools));
        toolTypes.Should().Contain(typeof(PhotoshopProductionTools));
        toolTypes.Should().Contain(typeof(IllustratorProductionTools));
        toolTypes.Should().NotContain(typeof(PhotoshopTools));
        toolTypes.Should().NotContain(typeof(IllustratorTools));
    }

    [Fact]
    public void DeveloperMode_AddsLowLevelAdobeTools()
    {
        var toolTypes = DuanxingToolCatalog.GetToolTypes(includeDeveloperTools: true);

        toolTypes.Should().Contain(typeof(PhotoshopTools));
        toolTypes.Should().Contain(typeof(IllustratorTools));
    }
}
