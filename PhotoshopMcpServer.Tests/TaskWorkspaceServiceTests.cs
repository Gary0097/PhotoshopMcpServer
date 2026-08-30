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

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }
}
