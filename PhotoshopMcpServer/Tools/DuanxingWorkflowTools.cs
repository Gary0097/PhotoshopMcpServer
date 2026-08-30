using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PhotoshopMcpServer.Models;
using PhotoshopMcpServer.Services;

namespace PhotoshopMcpServer.Tools;

[McpServerToolType]
public class DuanxingWorkflowTools(ITaskWorkspaceService taskWorkspaceService)
{
    [McpServerTool(Name = "duanxing_prepare_task")]
    [Description(
        "开始一个端行作图任务：自动保护原图、创建中文任务目录、生成工作副本、换算毫米和 DPI 对应像素，并保存任务记录。" +
        "在任何纹理处理、无缝拼接、折光线或生产导出前优先调用。")]
    public string 开始端行作图任务(
        [Description("原图的完整路径，例如 D:\\样板\\木纹.tif。原图不会被修改。")]
        string 原图路径,
        [Description("所有任务统一保存的根目录，例如 D:\\端行输出。")]
        string 输出根目录,
        [Description("便于员工识别的中文任务名称，例如 木纹无缝测试。")]
        string 任务名称,
        [Description("成品宽度，单位 mm。")]
        double 成品宽度毫米,
        [Description("成品高度，单位 mm。")]
        double 成品高度毫米,
        [Description("目标 DPI，常用值为 1270、2540 或 5080。")]
        int 目标DPI,
        [Description("填写：不拼接、平铺、1/2错位。")]
        string 拼接方式,
        [Description("填写：PSD、PSB、TIFF、PNG、JPEG、AI、SVG 或 PDF。")]
        string 输出格式,
        [Description("最终检查效果并批准生产输出的人员姓名。")]
        string 复核人)
    {
        try
        {
            var record = taskWorkspaceService.PrepareTask(new DuanxingTaskRequest(
                原图路径,
                输出根目录,
                任务名称,
                成品宽度毫米,
                成品高度毫米,
                目标DPI,
                拼接方式,
                输出格式,
                复核人));
            return JsonSerializer.Serialize(new
            {
                成功 = true,
                提示 = "任务已建立。请只处理工作副本，完成后交给指定人员复核。",
                任务编号 = record.TaskId,
                任务目录 = Directory.GetParent(record.OutputDirectory)?.FullName,
                工作副本 = record.WorkingCopy,
                处理结果目录 = record.OutputDirectory,
                目标像素 = $"{record.TargetWidthPixels} × {record.TargetHeightPixels}",
                注意事项 = record.Warnings
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new
            {
                成功 = false,
                提示 = exception.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public string 极简开始作图(
        [Description("原图完整路径；也可以是客户刚拖入 Codex 的图片路径。")]
        string 原图路径,
        [Description("成品宽度，单位毫米；这是生产参数，不能猜。")]
        double 成品宽度毫米,
        [Description("成品高度，单位毫米；这是生产参数，不能猜。")]
        double 成品高度毫米,
        [Description("印刷精度；客户说“精度”或“清晰度”时使用该数值。这是生产参数，不能猜。")]
        int 目标DPI,
        [Description("最终看图并确认能否生产的人员姓名。")]
        string 复核人,
        [Description("默认填“平铺”；也可填“不拼接”或“1/2错位”。")]
        string 拼接方式 = "平铺",
        [Description("默认 TIFF；需要时可填 PSD、PSB、PNG 或 JPEG。")]
        string 输出格式 = "TIFF")
    {
        try
        {
            var fullSourcePath = Path.GetFullPath(原图路径);
            var sourceDirectory = Path.GetDirectoryName(fullSourcePath)
                ?? throw new InvalidOperationException("无法确定原图所在目录。");
            var outputRoot = Path.Combine(sourceDirectory, "端行作图输出");
            var taskName = $"{Path.GetFileNameWithoutExtension(fullSourcePath)}_{拼接方式}";
            return 开始端行作图任务(
                fullSourcePath,
                outputRoot,
                taskName,
                成品宽度毫米,
                成品高度毫米,
                目标DPI,
                拼接方式,
                输出格式,
                复核人);
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new
            {
                成功 = false,
                提示 = exception.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public string 照上次规格开始作图(
        [Description("客户刚拖入的新原图完整路径。")]
        string 原图路径,
        [Description("复核人有变化时填写；留空则沿用最近任务的复核人。")]
        string 复核人 = "")
    {
        try
        {
            var recent = taskWorkspaceService.FindMostRecentTask();
            var effectiveReviewer = string.IsNullOrWhiteSpace(复核人)
                ? recent.Reviewer
                : 复核人.Trim();
            return 极简开始作图(
                原图路径,
                recent.WidthMillimeters,
                recent.HeightMillimeters,
                recent.Dpi,
                effectiveReviewer,
                recent.TilingMode,
                recent.OutputFormat);
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new
            {
                成功 = false,
                提示 = exception.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool(Name = "duanxing_continue_latest_task")]
    [Description(
        "继续这张原图最近一次的端行任务，不需要客户提供任务目录。" +
        "自动在原图旁的“端行作图输出”中查找，并返回中文任务状态和下一步。")]
    public string 继续这张图上次的任务(
        [Description("客户重新拖入的原图完整路径。")]
        string 原图路径)
    {
        try
        {
            var task = taskWorkspaceService.FindLatestTaskForSource(原图路径);
            return SerializeTaskSummary(task);
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new
            {
                成功 = false,
                提示 = exception.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool(Name = "duanxing_continue_most_recent_task")]
    [Description(
        "继续这台电脑上最近一次端行任务。客户只说“继续上次”时调用，" +
        "不需要重新拖入原图，也不需要提供任务编号或目录。")]
    public string 继续最近任务()
    {
        try
        {
            var task = taskWorkspaceService.FindMostRecentTask();
            return SerializeTaskSummary(task);
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new
            {
                成功 = false,
                提示 = exception.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool(Name = "duanxing_save_review")]
    [Description("保存端行人工复核结论。只有明确批准后，任务才可以导出为生产版。")]
    public string 保存复核结论(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录,
        [Description("复核人员姓名。")]
        string 复核人,
        [Description("客户明确说“通过”时为通过；说“退回修改”时为不通过。客户不需要输入英文。")]
        bool 是否通过,
        [Description("复核意见，例如：四边接缝自然，可以生产。")]
        string 复核意见)
    {
        try
        {
            var review = taskWorkspaceService.SaveReview(任务目录, 复核人, 是否通过, 复核意见);
            return JsonSerializer.Serialize(new
            {
                成功 = true,
                当前状态 = review.Status,
                复核人 = review.Reviewer,
                复核意见 = review.Comment,
                已批准文件 = string.IsNullOrEmpty(review.ResultFile) ? "无" : review.ResultFile,
                文件校验值 = string.IsNullOrEmpty(review.ResultSha256) ? "无" : review.ResultSha256
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new
            {
                成功 = false,
                提示 = exception.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool(Name = "duanxing_get_review_card")]
    [Description(
        "生成客户看得懂的中文复核单：汇总最新结果、原图保护、AI 记录、当前批准状态和必须人工检查的项目。" +
        "展示后只让客户回答“通过并导出”或“退回修改”。")]
    public string 生成中文复核单(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录)
    {
        try
        {
            var task = taskWorkspaceService.LoadTask(任务目录);
            var summary = taskWorkspaceService.BuildReviewSummary(任务目录);
            return JsonSerializer.Serialize(new
            {
                成功 = true,
                标题 = $"{task.TaskName}－复核单",
                结果文件 = string.IsNullOrEmpty(summary.LatestResultFile)
                    ? "尚未生成结果"
                    : summary.LatestResultFile,
                结果文件数量 = summary.ResultFileCount,
                AI处理次数 = summary.AiResultCount,
                最近AI处理 = string.IsNullOrEmpty(summary.LatestAiOperation)
                    ? "没有 AI 处理记录"
                    : summary.LatestAiOperation,
                原图保护 = summary.OriginalUnchanged ? "通过：原图未改变" : "异常：原图丢失或内容发生变化",
                工作副本 = summary.WorkingCopyExists ? "通过：工作副本存在" : "异常：找不到工作副本",
                当前复核状态 = summary.ReviewStatus,
                需要人工检查 = summary.ManualChecklist,
                请回答 = summary.ResultFileCount == 0
                    ? "请先生成检查版，暂时不能复核。"
                    : "请只回答：通过并导出，或退回修改并说明哪里要改。"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new
            {
                成功 = false,
                提示 = exception.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool(Name = "duanxing_register_ai_result")]
    [Description(
        "把 Codex AI 生成或编辑后的图片安全纳入端行任务：复制到处理结果目录、记录提示词和校验值，并让旧复核自动失效。" +
        "AI 补图、清晰修复或纹理生成完成后必须调用。")]
    public string 登记AI作图结果(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录,
        [Description("Codex AI 已生成图片的完整本地路径。")]
        string AI结果路径,
        [Description("填写：补图扩展、清晰修复、纹理生成或其他简短中文名称。")]
        string 处理类型,
        [Description("本次 AI 作图实际使用的中文要求，用于追溯。")]
        string 作图要求)
    {
        try
        {
            var result = taskWorkspaceService.RegisterAiResult(
                任务目录,
                AI结果路径,
                处理类型,
                作图要求);
            return JsonSerializer.Serialize(new
            {
                成功 = true,
                提示 = "AI 结果已保存到任务。请在 Photoshop 中检查，明确复核通过后才能导出生产版。",
                结果文件 = result.ResultFile,
                校验值 = result.ResultSha256,
                当前状态 = result.Status
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new
            {
                成功 = false,
                提示 = exception.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool(Name = "duanxing_generate_delivery_report")]
    [Description(
        "一键生成端行中文 POC、UAT 或正式交付报告，自动汇总任务规格、原图保护、处理结果、AI 记录、复核和生产文件，并预留双方签字项。")]
    public string 生成中文交付报告(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录,
        [Description("填写：首次部署、POC、UAT 或正式交付；默认 POC。")]
        string 阶段 = "POC")
    {
        try
        {
            var report = taskWorkspaceService.GenerateDeliveryReport(任务目录, 阶段);
            return JsonSerializer.Serialize(new
            {
                成功 = true,
                报告文件 = report.ReportFile,
                报告阶段 = report.Stage,
                材料状态 = report.Status,
                是否可以签字 = report.ReadyForSignOff,
                下一步 = report.ReadyForSignOff
                    ? "请双方查看报告中的人工验收项并签字。"
                    : "请先完成缺失的处理、复核或生产导出，再重新生成报告。"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new
            {
                成功 = false,
                提示 = exception.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool(Name = "duanxing_restore_previous_result")]
    [Description(
        "安全回到当前任务的上一版处理结果。不会删除或覆盖任何文件，" +
        "会复制上一版成为新的当前结果、记录操作并让旧审批失效。")]
    public string 回到上一版(
        [Description("当前端行任务目录。")]
        string 任务目录)
    {
        try
        {
            var record = taskWorkspaceService.RestorePreviousResult(任务目录);
            return JsonSerializer.Serialize(new
            {
                成功 = true,
                提示 = "已安全回到上一版，原来的文件都保留。请查看恢复结果并重新复核。",
                恢复结果 = record.RestoredFile,
                当前状态 = record.Status
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new
            {
                成功 = false,
                提示 = exception.Message
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool(Name = "duanxing_get_chinese_prompts")]
    [Description("返回端行员工最快只需两句话的中文作图菜单。用户说“帮助”“怎么用”或还没有任何任务时调用。")]
    public string 获取中文作图口令()
        => """
            端行作图最快只需要两句话：

            1. 拖入原图后说：
            做这张。
            第一次系统只会问一次生产规格；以后自动沿用上次规格。

            2. 看完预览和复核单后说：
            通过并导出。

            不满意就说“退回修改：哪里要改”。
            第二天接着做就说“继续上次”。
            不知道怎么办就说“下一步做什么”。
            """;

    private string SerializeTaskSummary(DuanxingTaskRecord task)
    {
        var taskDirectory = Directory.GetParent(task.OutputDirectory)?.FullName;
        var progress = taskWorkspaceService.BuildTaskProgress(taskDirectory);
        return JsonSerializer.Serialize(new
        {
            成功 = true,
            提示 = "已找到最近任务。不要修改原图，请继续处理工作副本。",
            任务目录 = taskDirectory,
            任务编号 = task.TaskId,
            当前进度 = progress.Status,
            任务名称 = task.TaskName,
            成品规格 = $"宽 {task.WidthMillimeters} 毫米 × 高 {task.HeightMillimeters} 毫米，印刷精度 {task.Dpi}",
            拼接方式 = task.TilingMode,
            输出格式 = task.OutputFormat,
            复核人 = task.Reviewer,
            工作副本 = task.WorkingCopy,
            处理结果目录 = task.OutputDirectory,
            下一步 = progress.NextStep
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
