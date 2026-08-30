using System.Text.Json;
using FluentAssertions;
using Moq;
using PhotoshopMcpServer.Models;
using PhotoshopMcpServer.Services;
using PhotoshopMcpServer.Tools;
using Xunit;

namespace PhotoshopMcpServer.Tests;

public class DuanxingQuickActionToolsTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "duanxing-quick-action-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ContinueWithoutHistory_ReturnsOneChineseStartInstruction()
    {
        var service = CreateService();
        var photoshop = new Mock<IPhotoshopService>();
        var tools = new DuanxingQuickActionTools(service, photoshop.Object);

        var json = tools.继续并执行下一步();

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("下一步").GetString().Should().Contain("拖入一张原图");
        photoshop.VerifyNoOtherCalls();
    }

    [Fact]
    public void ContinueApprovedTask_ExportsWithoutAskingForPathAgain()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹.png");
        File.WriteAllText(source, "original");
        var service = CreateService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source,
            Path.Combine(_testRoot, "输出"),
            "木纹生产",
            100,
            100,
            1270,
            "平铺",
            "TIFF",
            "张三"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName;
        var resultFile = Path.Combine(task.OutputDirectory, "已确认结果.psd");
        File.WriteAllText(resultFile, "approved");
        service.SaveReview(taskDirectory, "张三", true, "通过");
        var photoshop = new Mock<IPhotoshopService>();
        photoshop
            .Setup(item => item.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
        var tools = new DuanxingQuickActionTools(service, photoshop.Object);

        var json = tools.继续并执行下一步();

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("已完成").GetString().Should().Be("生产版已经导出");
        document.RootElement.GetProperty("下一步").GetString().Should().Contain("生成中文交付报告");
        photoshop.Verify(
            item => item.ExecuteJavaScriptWithResult(It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void ContinueRejectedTask_WaitsForHumanInstruction()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹.png");
        File.WriteAllText(source, "original");
        var service = CreateService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source,
            Path.Combine(_testRoot, "输出"),
            "木纹修改",
            100,
            100,
            1270,
            "平铺",
            "TIFF",
            "张三"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName;
        File.WriteAllText(Path.Combine(task.OutputDirectory, "待修改.psd"), "result");
        service.SaveReview(taskDirectory, "张三", false, "中间接缝太明显");
        var photoshop = new Mock<IPhotoshopService>();
        var tools = new DuanxingQuickActionTools(service, photoshop.Object);

        var json = tools.继续并执行下一步();

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("已完成").GetString().Should().Be("等待修改要求");
        document.RootElement.GetProperty("下一步").GetString().Should().Contain("说明要修改的位置");
        photoshop.VerifyNoOtherCalls();
    }

    private TaskWorkspaceService CreateService()
        => new(Path.Combine(_testRoot, "最近任务.json"));

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }
}
