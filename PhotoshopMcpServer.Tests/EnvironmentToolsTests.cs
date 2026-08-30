using FluentAssertions;
using Moq;
using PhotoshopMcpServer.Services;
using PhotoshopMcpServer.Tools;
using System.Text.Json;
using Xunit;

namespace PhotoshopMcpServer.Tests;

public class EnvironmentToolsTests
{
    [Fact]
    public void CustomerEnvironmentCheck_DoesNotExposePathsOrRepeatManualPrerequisites()
    {
        var photoshop = new Mock<IPhotoshopService>();
        var illustrator = new Mock<IIllustratorService>();
        var tools = new EnvironmentTools(photoshop.Object, illustrator.Object);

        var result = tools.CheckDuanxingEnvironment();
        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;

        root.TryGetProperty("自动检查结果", out _).Should().BeTrue();
        root.TryGetProperty("下一步", out _).Should().BeTrue();
        root.GetProperty("Photoshop 2026").TryGetProperty("安装目录", out _).Should().BeFalse();
        root.GetProperty("Illustrator 2026").TryGetProperty("安装目录", out _).Should().BeFalse();
        result.Should().NotContain("K:\\TOOL");
        root.TryGetProperty("需要人工确认", out _).Should().BeFalse();
    }
}
