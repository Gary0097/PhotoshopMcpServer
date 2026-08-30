using System.Security.Cryptography;
using PhotoshopMcpServer.Models;
using PhotoshopMcpServer.Tools;

namespace PhotoshopMcpServer.Services;

public sealed class WorkflowSelfTestRunner(
    IPhotoshopService photoshopService,
    ITaskWorkspaceService taskWorkspaceService)
{
    public WorkflowSelfTestResult Run(string sourceFile, string outputRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
            throw new FileNotFoundException("找不到完整流程自检原图。", sourceFile);
        var sourcePath = Path.GetFullPath(sourceFile);
        var sourceHashBefore = CalculateSha256(sourcePath);
        var task = taskWorkspaceService.PrepareTask(new DuanxingTaskRequest(
            sourcePath,
            Path.GetFullPath(outputRoot),
            "端行完整流程自检",
            50,
            50,
            72,
            "平铺",
            "TIFF",
            "现场自检"));
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("无法确定完整流程自检任务目录。");
        var tools = new PhotoshopProductionTools(photoshopService, taskWorkspaceService);
        var previewMessage = tools.一键生成工艺检查版(taskDirectory);
        var reviewFile = Directory.GetFiles(task.OutputDirectory, "*.psd")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (reviewFile == null)
            throw new InvalidOperationException($"没有生成 Photoshop 检查版：{previewMessage}");

        var review = taskWorkspaceService.SaveReview(
            taskDirectory,
            "现场自检",
            true,
            "端行完整流程自检自动批准测试文件。");
        var approvedFile = taskWorkspaceService.GetApprovedResultFile(taskDirectory);
        if (!string.Equals(
            Path.GetFullPath(reviewFile),
            approvedFile,
            StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("复核批准没有绑定到刚生成的检查版。");

        var exportMessage = tools.一键导出生产版(taskDirectory);
        var productionDirectory = Path.Combine(taskDirectory, "04_生产版");
        var productionFile = Directory.Exists(productionDirectory)
            ? Directory.GetFiles(productionDirectory, "*.tif")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;
        if (productionFile == null)
            throw new InvalidOperationException($"没有生成 TIFF 生产版：{exportMessage}");

        var sourceUnchanged = CalculateSha256(sourcePath) == sourceHashBefore;
        if (!sourceUnchanged)
            throw new InvalidOperationException("完整流程自检发现原图发生变化，已经停止。");
        var messages = new List<string>
        {
            "已建立中文任务目录并创建工作副本。",
            "已生成平铺检查版并绑定人工复核记录。",
            "已导出 TIFF 生产版。",
            "原图 SHA256 保持不变。",
            "端行完整业务流程自检通过。"
        };
        return new WorkflowSelfTestResult(
            true,
            DateTimeOffset.Now.ToString("O"),
            taskDirectory,
            sourcePath,
            sourceUnchanged,
            task.WorkingCopy,
            reviewFile,
            productionFile,
            review.Status,
            messages);
    }

    private static string CalculateSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
