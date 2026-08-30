using System.Text.RegularExpressions;
using FluentAssertions;
using Moq;
using PhotoshopMcpServer.Models;
using PhotoshopMcpServer.Services;
using Xunit;

namespace PhotoshopMcpServer.Tests;

public class WorkflowSelfTestRunnerTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "duanxing-workflow-self-test",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Run_CompletesProtectedReviewAndProductionFlow()
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, "自检原图.png");
        File.WriteAllText(source, "original");
        var photoshop = new Mock<IPhotoshopService>();
        photoshop.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns((string script) =>
            {
                var matches = Regex.Matches(script, "new File\\(\\\"([^\\\"]+)\\\"\\)");
                var outputPath = matches[^1].Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllText(outputPath, "photoshop-result");
                return new PhotoshopScriptResult(true, outputPath, string.Empty);
            });
        var taskService = new TaskWorkspaceService();
        var runner = new WorkflowSelfTestRunner(photoshop.Object, taskService);

        var result = runner.Run(source, Path.Combine(_testRoot, "完整流程结果"));

        result.Success.Should().BeTrue();
        result.SourceUnchanged.Should().BeTrue();
        File.ReadAllText(source).Should().Be("original");
        File.Exists(result.WorkingCopy).Should().BeTrue();
        File.Exists(result.ReviewFile).Should().BeTrue();
        File.Exists(result.ProductionFile).Should().BeTrue();
        result.ReviewStatus.Should().Be("已批准，可导出生产版");
        taskService.IsResultApproved(result.TaskDirectory, result.ReviewFile).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }
}
