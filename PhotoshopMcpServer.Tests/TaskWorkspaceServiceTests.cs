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
        File.WriteAllText(Path.Combine(task.OutputDirectory, "待复核.png"), "result");

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
        File.WriteAllText(Path.Combine(task.OutputDirectory, "初版.jpg"), "first-result");
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

    [Fact]
    public void BuildReviewSummary_ReportsProtectionAiAndManualChecks()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.png");
        var generated = Path.Combine(_testRoot, "AI结果.png");
        File.WriteAllText(source, "original");
        File.WriteAllText(generated, "generated");
        var service = new TaskWorkspaceService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "输出"), "复核摘要", 200, 100, 2540,
            "平铺", "TIFF", "张三"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("Task directory was not created.");
        service.RegisterAiResult(taskDirectory, generated, "纹理生成", "保持色系");

        var summary = service.BuildReviewSummary(taskDirectory);

        summary.OriginalUnchanged.Should().BeTrue();
        summary.WorkingCopyExists.Should().BeTrue();
        summary.ResultFileCount.Should().Be(1);
        summary.AiResultCount.Should().Be(1);
        summary.LatestAiOperation.Should().Be("纹理生成");
        summary.Approved.Should().BeFalse();
        summary.ManualChecklist.Should().Contain(item => item.Contains("中央横缝"));
    }

    [Fact]
    public void BuildReviewSummary_DetectsChangedOriginal()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.jpg");
        File.WriteAllText(source, "original");
        var service = new TaskWorkspaceService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "输出"), "原图保护", 100, 100, 1270,
            "不拼接", "JPEG", "李四"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("Task directory was not created.");
        File.WriteAllText(source, "changed");

        var summary = service.BuildReviewSummary(taskDirectory);

        summary.OriginalUnchanged.Should().BeFalse();
        summary.ManualChecklist.Should().Contain(item => item.Contains("成品裁切范围"));
    }

    [Fact]
    public void IsResultApproved_BindsApprovalToExactFileAndHash()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.png");
        File.WriteAllText(source, "original");
        var service = new TaskWorkspaceService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "输出"), "绑定复核", 100, 100, 1270,
            "平铺", "TIFF", "张三"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("Task directory was not created.");
        var approvedFile = Path.Combine(task.OutputDirectory, "检查版.png");
        var otherFile = Path.Combine(task.OutputDirectory, "其他版本.png");
        File.WriteAllText(approvedFile, "approved-content");
        service.SaveReview(taskDirectory, "张三", true, "通过");
        File.WriteAllText(otherFile, "other-content");

        service.IsResultApproved(taskDirectory, approvedFile).Should().BeTrue();
        service.GetApprovedResultFile(taskDirectory).Should().Be(Path.GetFullPath(approvedFile));
        service.IsResultApproved(taskDirectory, otherFile).Should().BeFalse();

        File.WriteAllText(approvedFile, "changed-after-approval");
        service.IsApproved(taskDirectory).Should().BeFalse();
        service.IsResultApproved(taskDirectory, approvedFile).Should().BeFalse();
        var action = () => service.GetApprovedResultFile(taskDirectory);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*内容发生变化*");
    }

    [Fact]
    public void SaveReview_WithoutResult_CannotApprove()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.png");
        File.WriteAllText(source, "original");
        var service = new TaskWorkspaceService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "输出"), "空结果", 100, 100, 1270,
            "平铺", "TIFF", "张三"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("Task directory was not created.");

        var action = () => service.SaveReview(taskDirectory, "张三", true, "通过");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*还没有处理结果*");
    }

    [Fact]
    public void GenerateDeliveryReport_WithCompleteEvidence_IsReadyForSignOff()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.png");
        File.WriteAllText(source, "original");
        var service = new TaskWorkspaceService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "输出"), "POC样板", 200, 100, 2540,
            "平铺", "TIFF", "张三"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("Task directory was not created.");
        var resultFile = Path.Combine(task.OutputDirectory, "检查版.psd");
        File.WriteAllText(resultFile, "result");
        service.SaveReview(taskDirectory, "张三", true, "通过");
        var productionDirectory = Path.Combine(taskDirectory, "04_生产版");
        Directory.CreateDirectory(productionDirectory);
        File.WriteAllText(Path.Combine(productionDirectory, "生产版.tif"), "production");

        var report = service.GenerateDeliveryReport(taskDirectory, "UAT");

        report.ReadyForSignOff.Should().BeTrue();
        report.Stage.Should().Be("UAT");
        File.Exists(report.ReportFile).Should().BeTrue();
        var markdown = File.ReadAllText(report.ReportFile);
        markdown.Should().Contain("# 端行UAT交付报告");
        markdown.Should().Contain("原图保护：通过");
        markdown.Should().Contain("材料齐全，等待双方签字");
        markdown.Should().Contain("甲方验收人");
        markdown.Should().Contain("生产版.tif");
    }

    [Fact]
    public void GenerateDeliveryReport_WithoutProduction_IsNotReadyForSignOff()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "原图.jpg");
        File.WriteAllText(source, "original");
        var service = new TaskWorkspaceService();
        var task = service.PrepareTask(new DuanxingTaskRequest(
            source, Path.Combine(_testRoot, "输出"), "待完成样板", 100, 100, 1270,
            "不拼接", "JPEG", "李四"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("Task directory was not created.");

        var report = service.GenerateDeliveryReport(taskDirectory, "正式交付");

        report.ReadyForSignOff.Should().BeFalse();
        report.Status.Should().Contain("暂不能签字");
        File.ReadAllText(report.ReportFile).Should().Contain("生产版：0 个");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }
}
