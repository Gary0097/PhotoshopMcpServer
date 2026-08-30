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

    private TaskWorkspaceService CreateService()
        => new(Path.Combine(_testRoot, "最近任务.json"));

    [Fact]
    public void SimplePrepareTask_UsesSafeChineseDefaults()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹原图.png");
        File.WriteAllText(source, "sample");
        var tools = new DuanxingWorkflowTools(CreateService());

        var json = tools.极简开始作图(source, 200, 100, 2540, "张三");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("成功").GetBoolean().Should().BeTrue();
        root.GetProperty("目标像素").GetString().Should().Be("20000 × 10000");
        root.TryGetProperty("Warnings", out _).Should().BeFalse();
        root.TryGetProperty("注意事项", out _).Should().BeTrue();
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
        var tools = new DuanxingWorkflowTools(CreateService());

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
        var tools = new DuanxingWorkflowTools(CreateService());
        tools.极简开始作图(source, 200, 100, 2540, "张三", "1/2错位", "PSD");

        var json = tools.继续这张图上次的任务(source);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("成功").GetBoolean().Should().BeTrue();
        root.GetProperty("成品规格").GetString()
            .Should().Be("宽 200 毫米 × 高 100 毫米，印刷精度 2540");
        root.GetProperty("拼接方式").GetString().Should().Be("1/2错位");
        root.GetProperty("下一步").GetString().Should().Contain("直接做检查版");
    }

    [Fact]
    public void ContinueMostRecentTask_NeedsNoFileOrTaskPath()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹原图.png");
        File.WriteAllText(source, "sample");
        var tools = new DuanxingWorkflowTools(CreateService());
        tools.极简开始作图(source, 200, 100, 2540, "张三");

        var json = tools.继续最近任务();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("成功").GetBoolean().Should().BeTrue();
        root.GetProperty("任务名称").GetString().Should().Contain("木纹原图");
        root.GetProperty("当前进度").GetString().Should().Be("任务已建立，等待处理");
        root.GetProperty("下一步").GetString().Should().Contain("直接做检查版");
        root.TryGetProperty("TaskId", out _).Should().BeFalse();
        root.TryGetProperty("Reviewer", out _).Should().BeFalse();
        root.GetProperty("任务编号").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("复核人").GetString().Should().Be("张三");
    }

    [Fact]
    public void PrepareLikeRecent_ReusesProductionSpecificationsForNewImage()
    {
        Directory.CreateDirectory(_testRoot);
        var firstSource = Path.Combine(_testRoot, "第一张.png");
        var newSource = Path.Combine(_testRoot, "新图.png");
        File.WriteAllText(firstSource, "first");
        File.WriteAllText(newSource, "new");
        var service = CreateService();
        var tools = new DuanxingWorkflowTools(service);
        tools.极简开始作图(firstSource, 320, 180, 5080, "张三", "二分之一错位", "PSB");

        var json = tools.照上次规格开始作图(newSource, "李四");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("成功").GetBoolean().Should().BeTrue();
        var taskDirectory = root.GetProperty("任务目录").GetString();
        var task = service.LoadTask(taskDirectory);
        task.SourceFile.Should().Be(Path.GetFullPath(newSource));
        task.WidthMillimeters.Should().Be(320);
        task.HeightMillimeters.Should().Be(180);
        task.Dpi.Should().Be(5080);
        task.TilingMode.Should().Be("1/2错位");
        task.OutputFormat.Should().Be("PSB");
        task.Reviewer.Should().Be("李四");
    }

    [Fact]
    public void PrepareLikeRecent_WithoutHistory_DoesNotGuessSpecifications()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "新图.png");
        File.WriteAllText(source, "new");
        var tools = new DuanxingWorkflowTools(CreateService());

        var json = tools.照上次规格开始作图(source);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("提示").GetString().Should().Contain("还没有最近任务");
    }

    [Fact]
    public void RestorePreviousResult_ReturnsSimpleChineseNextStep()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹.png");
        File.WriteAllText(source, "original");
        var service = CreateService();
        var tools = new DuanxingWorkflowTools(service);
        var prepareJson = tools.极简开始作图(source, 100, 100, 1270, "张三");
        using var prepareDocument = JsonDocument.Parse(prepareJson);
        var taskDirectory = prepareDocument.RootElement.GetProperty("任务目录").GetString();
        var task = service.LoadTask(taskDirectory);
        File.WriteAllText(Path.Combine(task.OutputDirectory, "第一版.png"), "first");
        File.SetLastWriteTimeUtc(
            Path.Combine(task.OutputDirectory, "第一版.png"),
            DateTime.UtcNow.AddMinutes(-1));
        File.WriteAllText(Path.Combine(task.OutputDirectory, "第二版.png"), "second");

        var json = tools.回到上一版(taskDirectory);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("提示").GetString().Should().Contain("原来的文件都保留");
        document.RootElement.GetProperty("提示").GetString().Should().Contain("重新复核");
    }

    [Fact]
    public void ReviewCard_WithoutResult_TellsCustomerWhatToDo()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹原图.png");
        File.WriteAllText(source, "sample");
        var tools = new DuanxingWorkflowTools(CreateService());
        var prepareJson = tools.极简开始作图(source, 200, 100, 2540, "张三");
        using var prepareDocument = JsonDocument.Parse(prepareJson);
        var taskDirectory = prepareDocument.RootElement.GetProperty("任务目录").GetString();

        var json = tools.生成中文复核单(taskDirectory);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("成功").GetBoolean().Should().BeTrue();
        root.GetProperty("结果文件").GetString().Should().Be("尚未生成结果");
        root.GetProperty("请回答").GetString().Should().Contain("暂时不能复核");
    }

    [Fact]
    public void ChineseHelp_ReturnsOnlyFourDailyActionsWithoutTechnicalTerms()
    {
        var tools = new DuanxingWorkflowTools(CreateService());

        var help = tools.获取中文作图口令();

        help.Should().Contain("1.【开始】");
        help.Should().Contain("2.【继续】");
        help.Should().Contain("3.【复核】");
        help.Should().Contain("4.【导出】");
        help.Should().NotContain("task.json");
        help.Should().NotContain("MCP");
        help.Should().NotContain("文件路径");
        help.Should().NotContain("DPI");
        help.Should().NotContain("mm");
        help.Should().Contain("精度 2540");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }
}
