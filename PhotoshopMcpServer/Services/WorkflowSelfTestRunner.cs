using System.Security.Cryptography;
using System.Text.Json;
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
        var preset = taskWorkspaceService.SaveMostRecentTaskAsPreset("现场自检规格");
        var loadedPreset = taskWorkspaceService.GetProductionPreset("现场自检规格");
        if (loadedPreset.WidthMillimeters != task.WidthMillimeters ||
            loadedPreset.HeightMillimeters != task.HeightMillimeters ||
            loadedPreset.Dpi != task.Dpi ||
            !string.Equals(loadedPreset.Name, preset.Name, StringComparison.Ordinal))
            throw new InvalidOperationException("中文规格模板保存后与任务规格不一致。");
        var tools = new PhotoshopProductionTools(photoshopService, taskWorkspaceService);
        var previewMessage = tools.一键生成工艺检查版(taskDirectory);
        var reviewFile = Directory.GetFiles(task.OutputDirectory, "*.psd")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (reviewFile == null)
            throw new InvalidOperationException($"没有生成 Photoshop 检查版：{previewMessage}");
        var quickTools = new DuanxingQuickActionTools(
            taskWorkspaceService,
            photoshopService);
        var reviewPreviewMessage = quickTools.查看最近结果();
        using var reviewPreviewDocument = JsonDocument.Parse(reviewPreviewMessage);
        var reviewPreviewRoot = reviewPreviewDocument.RootElement;
        if (!reviewPreviewRoot.GetProperty("成功").GetBoolean() ||
            !reviewPreviewRoot.GetProperty("中文复核单").TryGetProperty("请回答", out _))
            throw new InvalidOperationException("预览没有同时返回中文复核单。");
        var previewDirectory = Path.Combine(taskDirectory, "03_复核记录", "预览图");
        var previewFile = Directory.Exists(previewDirectory)
            ? Directory.GetFiles(previewDirectory, "*.png")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;
        if (previewFile == null)
            throw new InvalidOperationException($"没有生成 Codex 复核预览：{reviewPreviewMessage}");

        var approvalAndExportMessage = quickTools.批准并导出最近结果();
        using var approvalAndExportDocument = JsonDocument.Parse(approvalAndExportMessage);
        if (!approvalAndExportDocument.RootElement.GetProperty("成功").GetBoolean())
            throw new InvalidOperationException(
                $"通过并导出快捷入口失败：{approvalAndExportMessage}");
        var approvedFile = taskWorkspaceService.GetApprovedResultFile(taskDirectory);
        if (!string.Equals(
            Path.GetFullPath(reviewFile),
            approvedFile,
            StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("复核批准没有绑定到刚生成的检查版。");

        var productionDirectory = Path.Combine(taskDirectory, "04_生产版");
        var productionFile = Directory.Exists(productionDirectory)
            ? Directory.GetFiles(productionDirectory, "*.tif")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;
        if (productionFile == null)
            throw new InvalidOperationException(
                $"通过并导出后没有生成 TIFF 生产版：{approvalAndExportMessage}");

        var sourceUnchanged = CalculateSha256(sourcePath) == sourceHashBefore;
        if (!sourceUnchanged)
            throw new InvalidOperationException("完整流程自检发现原图发生变化，已经停止。");
        var deliveryReport = taskWorkspaceService.GenerateDeliveryReport(taskDirectory, "POC");
        if (!deliveryReport.ReadyForSignOff || !File.Exists(deliveryReport.ReportFile))
            throw new InvalidOperationException("中文交付报告未生成或材料状态不完整。");
        var messages = new List<string>
        {
            "已建立中文任务目录并创建工作副本。",
            "已保存并重新读取中文规格模板，参数保持一致。",
            "已通过一句“通过并导出”绑定人工复核记录并生成生产版。",
            "已同时生成可在 Codex 中查看的轻量预览和中文复核单。",
            "已导出 TIFF 生产版。",
            "已生成材料齐全的中文 POC 交付报告。",
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
            previewFile,
            productionFile,
            deliveryReport.ReportFile,
            taskWorkspaceService.BuildReviewSummary(taskDirectory).ReviewStatus,
            messages);
    }

    private static string CalculateSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
