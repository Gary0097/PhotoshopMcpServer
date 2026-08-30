using FluentAssertions;
using ModelContextProtocol.Server;
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

    [Fact]
    public void CustomerMode_DoesNotExposeIncompleteLegacyStartTools()
    {
        var exposedNames = typeof(DuanxingWorkflowTools)
            .GetMethods()
            .Select(method => method.GetCustomAttributes(
                typeof(McpServerToolAttribute),
                inherit: true).SingleOrDefault())
            .OfType<McpServerToolAttribute>()
            .Select(attribute => attribute.Name);

        exposedNames.Should().NotContain("duanxing_prepare_task_simple");
        exposedNames.Should().NotContain("duanxing_prepare_like_recent");
        exposedNames.Should().Contain("duanxing_prepare_task");
    }
}
