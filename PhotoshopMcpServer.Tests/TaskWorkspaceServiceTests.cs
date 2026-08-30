using System.Text.Json;
using FluentAssertions;
using PhotoshopMcpServer.Models;
using PhotoshopMcpServer.Services;
using Xunit;

namespace PhotoshopMcpServer.Tests;

public class TaskWorkspaceServiceTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "duanxing-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void PrepareTask_CreatesProtectedWorkingCopyAndCorrectPixels()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.tif");
        File.WriteAllText(source, "sample");
        var output = Path.Combine(_testRoot, "输出");
        var service = new TaskWorkspaceService();

        var record = service.PrepareTask(new DuanxingTaskRequest(
            source, output, "木纹无缝", 200, 200, 2540, "平铺", "TIFF", "张三"));

        record.TargetWidthPixels.Should().Be(20000);
        record.TargetHeightPixels.Should().Be(20000);
        record.WorkingCopy.Should().NotBe(source);
        File.Exists(record.WorkingCopy).Should().BeTrue();
        File.ReadAllText(source).Should().Be("sample");
        var taskDirectory = Directory.GetParent(record.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("Task directory was not created.");
        File.Exists(Path.Combine(taskDirectory, "task.json"))
            .Should().BeTrue();
    }

    [Fact]
    public void PrepareTask_ForLargeImage_ReturnsResourceWarning()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.psb");
        File.WriteAllText(source, "sample");
        var service = new TaskWorkspaceService();

        var record = service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "输出"), "大图", 500, 500, 5080,
            "1/2错位", "PSB", "李四"));

        record.Warnings.Should().NotBeEmpty();
        record.TilingMode.Should().Be("1/2错位");
    }

    [Fact]
    public void SaveReview_WritesChineseReviewRecord()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.png");
        File.WriteAllText(source, "sample");
        var service = new TaskWorkspaceService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "输出"), "复核", 100, 100, 1270,
            "不拼接", "PNG", "王五"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("Task directory was not created.");

        var review = service.SaveReview(taskDirectory, "王五", true, "可以生产");

        review.Status.Should().Be("已批准，可导出生产版");
        Directory.GetFiles(Path.Combine(taskDirectory, "03_复核记录"), "review-*.json")
            .Should().ContainSingle();
        service.IsApproved(taskDirectory).Should().BeTrue();
    }

    [Fact]
    public void IsApproved_WithoutReview_ReturnsFalse()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.jpg");
        File.WriteAllText(source, "sample");
        var service = new TaskWorkspaceService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "输出"), "待复核", 100, 100, 1270,
            "不拼接", "JPEG", "赵六"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("Task directory was not created.");

        service.IsApproved(taskDirectory).Should().BeFalse();
    }

    [Fact]
    public void RegisterAiResult_CopiesAndRecordsResult()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.png");
        var generated = Path.Combine(_testRoot, "生成图.png");
        File.WriteAllText(source, "original");
        File.WriteAllText(generated, "ai-result");
        var service = new TaskWorkspaceService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "输出"), "AI补图", 100, 100, 1270,
            "不拼接", "PNG", "赵六"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("Task directory was not created.");

        var result = service.RegisterAiResult(taskDirectory, generated, "补图扩展", "保持原纹理");

        File.Exists(result.ResultFile).Should().BeTrue();
        File.ReadAllText(result.ResultFile).Should().Be("ai-result");
        result.ResultFile.Should().StartWith(task.OutputDirectory);
        result.Status.Should().Be("待人工复核");
        result.ResultSha256.Should().HaveLength(64);
        Directory.GetFiles(Path.Combine(taskDirectory, "05_AI记录"), "ai-*.json")
            .Should().ContainSingle();
    }

    [Fact]
    public void RegisterAiResult_InvalidatesPreviousApproval()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.jpg");
        var generated = Path.Combine(_testRoot, "清晰图.jpg");
        File.WriteAllText(source, "original");
        File.WriteAllText(generated, "enhanced");
        var service = new TaskWorkspaceService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "输出"), "AI清晰", 100, 100, 1270,
            "不拼接", "JPEG", "王五"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("Task directory was not created.");
        service.SaveReview(taskDirectory, "王五", true, "通过");

        service.RegisterAiResult(taskDirectory, generated, "清晰修复", "恢复细节");

        service.IsApproved(taskDirectory).Should().BeFalse();
        var latestReview = Directory.GetFiles(Path.Combine(taskDirectory, "03_复核记录"), "review-*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
        JsonDocument.Parse(File.ReadAllText(latestReview))
            .RootElement.GetProperty("Comment").GetString()
            .Should().Contain("旧复核自动失效");
    }

    [Fact]
    public void FindLatestTaskForSource_ReturnsNewestMatchingTask()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.png");
        File.WriteAllText(source, "sample");
        var service = new TaskWorkspaceService();
        service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "端行作图输出"), "第一次", 100, 100, 1270,
            "平铺", "TIFF", "张三"));
        var latest = service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "端行作图输出"), "第二次", 200, 100, 2540,
            "1/2错位", "PSD", "李四"));

        var found = service.FindLatestTaskForSource(source);

        found.TaskId.Should().Be(latest.TaskId);
        found.TaskName.Should().Be("第二次");
    }

    [Fact]
    public void FindLatestTaskForSource_WithoutHistory_ReturnsChineseError()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "新原图.png");
        File.WriteAllText(source, "sample");
        var service = new TaskWorkspaceService();

        var action = () => service.FindLatestTaskForSource(source);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*还没有默认任务*");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }
}
