using FluentAssertions;
using Moq;
using PhotoshopMcpServer.Services;
using PhotoshopMcpServer.Tools;
using Xunit;

namespace PhotoshopMcpServer.Tests;

public class IllustratorToolsTests
{
    private readonly Mock<IIllustratorService> _illustratorService = new();

    [Fact]
    public void IsIllustratorRunning_WhenRunning_ReturnsTrue()
    {
        _illustratorService.Setup(service => service.IsIllustratorRunning()).Returns(true);
        var tools = new IllustratorTools(_illustratorService.Object);

        tools.IsIllustratorRunning().Should().Be("true");
    }

    [Fact]
    public void LaunchIllustrator_WhenUnavailable_ReturnsUsefulError()
    {
        _illustratorService.Setup(service => service.LaunchIllustrator())
            .Throws(new InvalidOperationException("Illustrator not found"));
        var tools = new IllustratorTools(_illustratorService.Object);

        tools.LaunchIllustrator().Should().Contain("Illustrator not found");
    }
}
