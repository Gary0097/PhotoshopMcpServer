using System.Text.Json;
using FluentAssertions;
using PhotoshopMcpServer.Services;
using PhotoshopMcpServer.Tools;
using Xunit;

namespace PhotoshopMcpServer.Tests;

public class DuanxingWorkflowToolsTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "duanxing-workflow-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SimplePrepareTask_UsesSafeChineseDefaults()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹原图.png");
        File.WriteAllText(source, "sample");
        var tools = new DuanxingWorkflowTools(new TaskWorkspaceService());

        var json = tools.极简开始作图(source, 200, 100, 2540, "张三");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("成功").GetBoolean().Should().BeTrue();
        root.GetProperty("目标像素").GetString().Should().Be("20000 × 10000");
        var taskDirectory = root.GetProperty("任务目录").GetString();
        taskDirectory.Should().Contain("端行作图输出");
        File.Exists(Path.Combine(taskDirectory, "task.json")).Should().BeTrue();
    }

    [Fact]
    public void SimplePrepareTask_MissingProductionParameter_ReturnsChineseError()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹原图.png");
        File.WriteAllText(source, "sample");
        var tools = new DuanxingWorkflowTools(new TaskWorkspaceService());

        var json = tools.极简开始作图(source, 0, 100, 2540, "张三");

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("提示").GetString().Should().Contain("必须大于 0");
    }

    [Fact]
    public void ContinueLatestTask_ReturnsChineseSummaryWithoutTaskPathInput()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹原图.png");
        File.WriteAllText(source, "sample");
        var tools = new DuanxingWorkflowTools(new TaskWorkspaceService());
        tools.极简开始作图(source, 200, 100, 2540, "张三", "1/2错位", "PSD");

        var json = tools.继续这张图上次的任务(source);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("成功").GetBoolean().Should().BeTrue();
        root.GetProperty("成品规格").GetString().Should().Be("200 × 100 mm，2540 DPI");
        root.GetProperty("拼接方式").GetString().Should().Be("1/2错位");
        root.GetProperty("下一步").GetString().Should().Contain("直接做检查版");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }
}
