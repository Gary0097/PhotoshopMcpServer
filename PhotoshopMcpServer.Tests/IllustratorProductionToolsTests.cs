using FluentAssertions;
using Moq;
using PhotoshopMcpServer.Models;
using PhotoshopMcpServer.Services;
using PhotoshopMcpServer.Tools;
using Xunit;

namespace PhotoshopMcpServer.Tests;

public class IllustratorProductionToolsTests
{
    private readonly Mock<IIllustratorService> _illustratorService = new();
    private readonly Mock<ITaskWorkspaceService> _taskWorkspaceService = new();

    [Fact]
    public void CreateStraightLines_GeneratesEditableIllustratorScript()
    {
        _taskWorkspaceService.Setup(service => service.LoadTask("task")).Returns(CreateTask());
        _illustratorService.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new IllustratorScriptResult(true, "ok", string.Empty));
        var tools = new IllustratorProductionTools(
            _illustratorService.Object,
            _taskWorkspaceService.Object);

        var result = tools.生成直线折光线AI("task", 0.2, 2.0, 30);

        result.Should().Contain("直线折光线 AI 已生成");
        _illustratorService.Verify(service => service.ExecuteJavaScriptWithResult(
            It.Is<string>(script =>
                script.Contains("pathItems.add") &&
                script.Contains("setEntirePath") &&
                script.Contains("30*Math.PI/180") &&
                script.Contains("doc.saveAs"))));
    }

    [Fact]
    public void CreateWaveLines_GeneratesSineWaveScript()
    {
        _taskWorkspaceService.Setup(service => service.LoadTask("task")).Returns(CreateTask());
        _illustratorService.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new IllustratorScriptResult(true, "ok", string.Empty));
        var tools = new IllustratorProductionTools(
            _illustratorService.Object,
            _taskWorkspaceService.Object);

        var result = tools.生成S型折光线AI("task", 0.2, 3.0, 1.5, 12, 0);

        result.Should().Contain("S 型折光线 AI 已生成");
        _illustratorService.Verify(service => service.ExecuteJavaScriptWithResult(
            It.Is<string>(script =>
                script.Contains("Math.sin") &&
                script.Contains("wavelength") &&
                script.Contains("setEntirePath(points)"))));
    }

    [Theory]
    [InlineData(0, 2, 0, "线宽")]
    [InlineData(2, 1, 0, "间距")]
    [InlineData(0.2, 2, 181, "角度")]
    public void CreateStraightLines_WithInvalidParameters_ReturnsChineseError(
        double lineWidth,
        double spacing,
        double angle,
        string expected)
    {
        var tools = new IllustratorProductionTools(
            _illustratorService.Object,
            _taskWorkspaceService.Object);

        tools.生成直线折光线AI("task", lineWidth, spacing, angle)
            .Should().Contain(expected);
        _illustratorService.Verify(
            service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()),
            Times.Never);
    }

    private static DuanxingTaskRecord CreateTask()
        => new(
            "DX-test",
            "待处理",
            DateTimeOffset.Now.ToString("O"),
            @"D:\样板\原图.tif",
            "hash",
            @"D:\输出\任务\01_工作副本\原图.tif",
            @"D:\输出\任务\02_处理结果",
            "木纹折光线",
            200,
            100,
            2540,
            20000,
            10000,
            200_000_000,
            "不拼接",
            "AI",
            "张三",
            []);
}
