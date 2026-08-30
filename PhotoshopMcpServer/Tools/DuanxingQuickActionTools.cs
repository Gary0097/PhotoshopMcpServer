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
    [McpServerTool(Name = "duanxing_start_and_run")]
    [Description(
        "一键开始端行作图：保护客户原图、建立中文任务、生成工艺检查版和复核预览。" +
        "客户只需提供原图、成品宽高、印刷精度和复核人；默认平铺并输出 TIFF。")]
    public string 开始并生成检查版(
        [Description("客户拖入 Codex 的原图完整位置。")]
        string 原图路径,
        [Description("成品宽度，单位毫米，不能猜测。")]
        double 成品宽度毫米,
        [Description("成品高度，单位毫米，不能猜测。")]
        double 成品高度毫米,
        [Description("印刷精度，常用数值为 1270、2540 或 5080，不能猜测。")]
        int 印刷精度,
        [Description("最终查看效果并确认是否可以生产的人员姓名。")]
        string 复核人,
        [Description("默认平铺；也可填写不拼接或 1/2 错位。")]
        string 拼接方式 = "平铺",
        [Description("默认 TIFF；需要可编辑图层时可填写 PSD 或 PSB。")]
        string 输出格式 = "TIFF")
    {
        try
        {
            var fullSourcePath = Path.GetFullPath(原图路径);
            var sourceDirectory = Path.GetDirectoryName(fullSourcePath)
                ?? throw new InvalidOperationException("无法确定原图所在目录。");
            var task = taskWorkspaceService.PrepareTask(new Models.DuanxingTaskRequest(
                fullSourcePath,
                Path.Combine(sourceDirectory, "端行作图输出"),
                $"{Path.GetFileNameWithoutExtension(fullSourcePath)}_{拼接方式}",
                成品宽度毫米,
                成品高度毫米,
                印刷精度,
                拼接方式,
                输出格式,
                复核人));
            var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName
                ?? throw new InvalidOperationException("无法确定新任务目录。");
            var productionTools = new PhotoshopProductionTools(
                photoshopService,
                taskWorkspaceService);
            return CreateCheckAndPreview(productionTools, taskDirectory);
        }
        catch (Exception exception)
        {
            return SerializeResult(false, "任务没有开始", exception.Message);
        }
    }

    [McpServerTool(Name = "duanxing_show_latest_result")]
    [Description("显示最近端行任务的复核预览。客户只需说“给我看结果”，不需要提供任务目录或文件位置。")]
    public string 查看最近结果()
    {
        try
        {
            var taskDirectory = GetRecentTaskDirectory();
            var productionTools = new PhotoshopProductionTools(
                photoshopService,
                taskWorkspaceService);
            return ShowPreview(productionTools, taskDirectory);
        }
        catch (Exception exception)
        {
            return SerializeResult(false, "没有显示结果", exception.Message);
        }
    }

    [McpServerTool(Name = "duanxing_approve_latest_result")]
    [Description(
        "批准最近端行任务的最新处理结果。客户明确说“通过”时调用，" +
        "自动使用任务中登记的复核人并绑定当前文件校验值。")]
    public string 批准最近结果()
    {
        try
        {
            var task = taskWorkspaceService.FindMostRecentTask();
            var taskDirectory = GetTaskDirectory(task);
            taskWorkspaceService.SaveReview(
                taskDirectory,
                task.Reviewer,
                true,
                "客户确认通过");
            return SerializeResult(
                true,
                "最新结果已复核通过",
                "说“直接导出生产版”。");
        }
        catch (Exception exception)
        {
            return SerializeResult(false, "没有保存通过结论", exception.Message);
        }
    }

    [McpServerTool(Name = "duanxing_reject_latest_result")]
    [Description(
        "退回最近端行任务的最新处理结果。客户说“退回修改”时调用，" +
        "只需填写客户说的具体修改要求，不需要任务目录或复核人。")]
    public string 退回最近结果(
        [Description("客户说明的修改位置和目标效果，例如：中间竖缝太明显，请减弱。")]
        string 修改要求)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(修改要求))
                throw new ArgumentException("请说明哪里需要修改以及希望达到的效果。");
            var task = taskWorkspaceService.FindMostRecentTask();
            var taskDirectory = GetTaskDirectory(task);
            taskWorkspaceService.SaveReview(
                taskDirectory,
                task.Reviewer,
                false,
                修改要求.Trim());
            return SerializeResult(
                true,
                "最新结果已退回修改",
                "请继续说明修改要求，或说“回到上一版”。");
        }
        catch (Exception exception)
        {
            return SerializeResult(false, "没有保存退回结论", exception.Message);
        }
    }

    [McpServerTool(Name = "duanxing_export_latest_approved")]
    [Description(
        "导出最近端行任务已经批准的生产版。客户只需说“直接导出生产版”，" +
        "系统自动使用批准文件、任务格式和生产目录。")]
    public string 直接导出最近生产版()
    {
        try
        {
            var taskDirectory = GetRecentTaskDirectory();
            var productionTools = new PhotoshopProductionTools(
                photoshopService,
                taskWorkspaceService);
            return ExportProduction(productionTools, taskDirectory);
        }
        catch (Exception exception)
        {
            return SerializeResult(false, "生产版没有导出", exception.Message);
        }
    }

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

    private string GetRecentTaskDirectory()
        => GetTaskDirectory(taskWorkspaceService.FindMostRecentTask());

    private static string GetTaskDirectory(Models.DuanxingTaskRecord task)
        => Directory.GetParent(task.OutputDirectory)?.FullName
            ?? throw new InvalidOperationException("无法确定最近任务目录。");

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
