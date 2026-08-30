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
    public void MakeThisImage_FirstUse_AsksForAllMissingProductionDetailsOnce()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "首次原图.png");
        File.WriteAllText(source, "original");
        var service = CreateService();
        var photoshop = new Mock<IPhotoshopService>();
        var tools = new DuanxingQuickActionTools(service, photoshop.Object);

        var json = tools.做这张图(source);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("已完成").GetString().Should().Be("还缺少首次生产规格");
        document.RootElement.GetProperty("下一步").GetString().Should()
            .Be("请一次告诉我：成品宽多少毫米、高多少毫米、精度是多少、谁负责复核。例：宽200，高200，精度2540，张三复核。");
        photoshop.VerifyNoOtherCalls();
    }

    [Fact]
    public void MakeThisImage_ReusesRecentDetailsAndAllowsOneExplicitChange()
    {
        Directory.CreateDirectory(_testRoot);
        var firstSource = Path.Combine(_testRoot, "上一张.png");
        var newSource = Path.Combine(_testRoot, "新图.png");
        File.WriteAllText(firstSource, "first");
        File.WriteAllText(newSource, "new-original");
        var service = CreateService();
        service.PrepareTask(new DuanxingTaskRequest(
            firstSource,
            Path.Combine(_testRoot, "上一批"),
            "上一张",
            320,
            180,
            2540,
            "1/2错位",
            "PSB",
            "张三"));
        var photoshop = new Mock<IPhotoshopService>();
        photoshop
            .Setup(item => item.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
        var tools = new DuanxingQuickActionTools(service, photoshop.Object);

        var json = tools.做这张图(newSource, 成品宽度毫米: 400);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeTrue();
        var task = service.FindMostRecentTask();
        task.WidthMillimeters.Should().Be(400);
        task.HeightMillimeters.Should().Be(180);
        task.Dpi.Should().Be(2540);
        task.TilingMode.Should().Be("1/2错位");
        task.OutputFormat.Should().Be("PSB");
        task.Reviewer.Should().Be("张三");
        File.ReadAllText(newSource).Should().Be("new-original");
        photoshop.Verify(
            item => item.ExecuteJavaScriptWithResult(It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void StartAndRun_CreatesProtectedTaskAndRunsCheckInOneCall()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹.png");
        File.WriteAllText(source, "original");
        var service = CreateService();
        var photoshop = new Mock<IPhotoshopService>();
        photoshop
            .Setup(item => item.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
        var tools = new DuanxingQuickActionTools(service, photoshop.Object);

        var json = tools.开始并生成检查版(source, 200, 100, 2540, "张三");

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("已完成").GetString()
            .Should().Contain("检查版已经生成");
        var task = service.FindMostRecentTask();
        task.SourceFile.Should().Be(Path.GetFullPath(source));
        task.WidthMillimeters.Should().Be(200);
        task.HeightMillimeters.Should().Be(100);
        task.Dpi.Should().Be(2540);
        task.Reviewer.Should().Be("张三");
        File.ReadAllText(source).Should().Be("original");
        photoshop.Verify(
            item => item.ExecuteJavaScriptWithResult(It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void ShowLatestResult_ReturnsPreviewAndChineseReviewCardTogether()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "待复核.png");
        File.WriteAllText(source, "original");
        var service = CreateService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source,
            Path.Combine(_testRoot, "输出"),
            "待复核样板",
            200,
            100,
            2540,
            "平铺",
            "TIFF",
            "张三"));
        File.WriteAllText(Path.Combine(task.OutputDirectory, "检查版.psd"), "result");
        var photoshop = new Mock<IPhotoshopService>();
        photoshop
            .Setup(item => item.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
        var tools = new DuanxingQuickActionTools(service, photoshop.Object);

        var json = tools.查看最近结果();

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("已完成").GetString()
            .Should().Contain("中文复核单");
        document.RootElement.GetProperty("中文复核单").GetProperty("请回答").GetString()
            .Should().Contain("请只回答");
        photoshop.Verify(
            item => item.ExecuteJavaScriptWithResult(It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void StartAndRun_InvalidSize_ReturnsOnlyChineseCustomerMessage()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹.png");
        File.WriteAllText(source, "original");
        var service = CreateService();
        var photoshop = new Mock<IPhotoshopService>();
        var tools = new DuanxingQuickActionTools(service, photoshop.Object);

        var json = tools.开始并生成检查版(source, 0, 100, 2540, "张三");

        using var document = JsonDocument.Parse(json);
        var nextStep = document.RootElement.GetProperty("下一步").GetString();
        nextStep.Should().Be("成品宽度和高度必须大于 0 毫米。");
        nextStep.Should().NotContain("Parameter");
        nextStep.Should().NotContain("request");
        photoshop.VerifyNoOtherCalls();
    }

    [Fact]
    public void StartLikeRecentAndRun_ReusesSpecificationsAndProtectsNewOriginal()
    {
        Directory.CreateDirectory(_testRoot);
        var firstSource = Path.Combine(_testRoot, "第一张.png");
        var newSource = Path.Combine(_testRoot, "第二张.png");
        File.WriteAllText(firstSource, "first");
        File.WriteAllText(newSource, "second-original");
        var service = CreateService();
        service.PrepareTask(new DuanxingTaskRequest(
            firstSource,
            Path.Combine(_testRoot, "第一批"),
            "第一张",
            320,
            180,
            5080,
            "1/2错位",
            "PSB",
            "张三"));
        var photoshop = new Mock<IPhotoshopService>();
        photoshop
            .Setup(item => item.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
        var tools = new DuanxingQuickActionTools(service, photoshop.Object);

        var json = tools.照上次规格开始并生成(newSource);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeTrue();
        var task = service.FindMostRecentTask();
        task.SourceFile.Should().Be(Path.GetFullPath(newSource));
        task.WidthMillimeters.Should().Be(320);
        task.HeightMillimeters.Should().Be(180);
        task.Dpi.Should().Be(5080);
        task.TilingMode.Should().Be("1/2错位");
        task.OutputFormat.Should().Be("PSB");
        task.Reviewer.Should().Be("张三");
        File.ReadAllText(newSource).Should().Be("second-original");
        photoshop.Verify(
            item => item.ExecuteJavaScriptWithResult(It.IsAny<string>()),
            Times.Once);
    }

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
    public void ApproveLatestResult_NeedsNoTaskPathOrReviewer()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹.png");
        File.WriteAllText(source, "original");
        var service = CreateService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source,
            Path.Combine(_testRoot, "输出"),
            "木纹复核",
            100,
            100,
            1270,
            "平铺",
            "TIFF",
            "张三"));
        File.WriteAllText(Path.Combine(task.OutputDirectory, "检查版.psd"), "result");
        var photoshop = new Mock<IPhotoshopService>();
        var tools = new DuanxingQuickActionTools(service, photoshop.Object);

        var json = tools.批准最近结果();

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("已完成").GetString().Should().Contain("复核通过");
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName;
        var review = service.BuildReviewSummary(taskDirectory);
        review.Approved.Should().BeTrue();
        review.ReviewStatus.Should().Be("已批准，可导出生产版");
        photoshop.VerifyNoOtherCalls();
    }

    [Fact]
    public void RejectLatestResult_SavesCustomerInstructionWithoutTaskPath()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "木纹.png");
        File.WriteAllText(source, "original");
        var service = CreateService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source,
            Path.Combine(_testRoot, "输出"),
            "木纹复核",
            100,
            100,
            1270,
            "平铺",
            "TIFF",
            "张三"));
        File.WriteAllText(Path.Combine(task.OutputDirectory, "检查版.psd"), "result");
        var photoshop = new Mock<IPhotoshopService>();
        var tools = new DuanxingQuickActionTools(service, photoshop.Object);

        var json = tools.退回最近结果("中间竖缝太明显，请减弱");

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeTrue();
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName;
        service.BuildTaskProgress(taskDirectory).Status.Should().Be("已退回，等待修改");
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
    public void DirectExportLatestApproved_NeedsNoTaskOrFilePath()
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
        File.WriteAllText(Path.Combine(task.OutputDirectory, "已确认结果.psd"), "approved");
        service.SaveReview(
            Directory.GetParent(task.OutputDirectory)?.FullName,
            "张三",
            true,
            "通过");
        var photoshop = new Mock<IPhotoshopService>();
        photoshop
            .Setup(item => item.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
        var tools = new DuanxingQuickActionTools(service, photoshop.Object);

        var json = tools.直接导出最近生产版();

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("成功").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("已完成").GetString().Should().Be("生产版已经导出");
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
