using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PhotoshopMcpServer.Services;

namespace PhotoshopMcpServer.Tools;

[McpServerToolType]
public class DuanxingQuickActionTools(
    ITaskWorkspaceService taskWorkspaceService,
    IPhotoshopService photoshopService)
{
    [McpServerTool(Name = "duanxing_continue_and_run")]
    [Description(
        "一键继续最近的端行任务：等待处理时生成检查版和预览，等待复核时重新显示预览，" +
        "复核通过时导出生产版。遇到退回修改、原图异常或已经完成时不会擅自处理。")]
    public string 继续并执行下一步()
    {
        try
        {
            var task = taskWorkspaceService.FindMostRecentTask();
            var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
                ?? throw new InvalidOperationException("无法确定最近任务目录。");
            var progress = taskWorkspaceService.BuildTaskProgress(taskDirectory);
            var productionTools = new PhotoshopProductionTools(
                photoshopService,
                taskWorkspaceService);

            return progress.Status switch
            {
                "任务已建立，等待处理" => CreateCheckAndPreview(
                    productionTools,
                    taskDirectory),
                "已有处理结果，等待复核" or "结果有变化，需要重新复核" => ShowPreview(
                    productionTools,
                    taskDirectory),
                "已复核通过，等待导出" => ExportProduction(
                    productionTools,
                    taskDirectory),
                "已退回，等待修改" => SerializeResult(
                    true,
                    "等待修改要求",
                    "请只说明要修改的位置和效果；如果不要本次修改，可以说“回到上一版”。"),
                "已导出生产版" => SerializeResult(
                    true,
                    "任务已经完成",
                    "需要交付材料时说“生成中文交付报告”。"),
                "已停止：原图异常" => SerializeResult(
                    false,
                    "原图保护检查未通过，已经停止",
                    "请不要继续操作，联系实施人员检查原图。"),
                _ => SerializeResult(
                    false,
                    "暂时无法判断任务进度",
                    progress.NextStep)
            };
        }
        catch (Exception exception)
        {
            return SerializeResult(false, "没有继续任务", exception.Message);
        }
    }

    private static string CreateCheckAndPreview(
        PhotoshopProductionTools tools,
        string taskDirectory)
    {
        var checkResult = tools.一键生成工艺检查版(taskDirectory);
        if (OperationFailed(checkResult))
            return SerializeResult(false, "检查版没有生成", checkResult);

        var previewResult = tools.生成复核预览图(taskDirectory);
        if (OperationFailed(previewResult))
            return SerializeResult(
                true,
                "检查版已经生成，但预览没有显示",
                "说“给我看结果”重试预览；仍失败时联系实施人员。",
                checkResult);
        return SerializeResult(
            true,
            "检查版和预览已经生成",
            "请查看预览，然后只回答“通过”或“退回修改”。",
            previewResult);
    }

    private static string ShowPreview(
        PhotoshopProductionTools tools,
        string taskDirectory)
    {
        var previewResult = tools.生成复核预览图(taskDirectory);
        return OperationFailed(previewResult)
            ? SerializeResult(false, "预览没有生成", previewResult)
            : SerializeResult(
                true,
                "复核预览已经生成",
                "请查看预览，然后只回答“通过”或“退回修改”。",
                previewResult);
    }

    private static string ExportProduction(
        PhotoshopProductionTools tools,
        string taskDirectory)
    {
        var exportResult = tools.一键导出生产版(taskDirectory);
        return OperationFailed(exportResult)
            ? SerializeResult(false, "生产版没有导出", exportResult)
            : SerializeResult(
                true,
                "生产版已经导出",
                "需要交付材料时说“生成中文交付报告”。",
                exportResult);
    }

    private static bool OperationFailed(string result)
        => result.Contains("失败", StringComparison.Ordinal) ||
            result.StartsWith("无法", StringComparison.Ordinal) ||
            result.StartsWith("还没有", StringComparison.Ordinal);

    private static string SerializeResult(
        bool succeeded,
        string completed,
        string nextStep,
        string detail = "")
        => JsonSerializer.Serialize(new
        {
            成功 = succeeded,
            已完成 = completed,
            处理详情 = string.IsNullOrWhiteSpace(detail) ? "无" : detail,
            下一步 = nextStep
        }, new JsonSerializerOptions { WriteIndented = true });
}
