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
                工作副本 = record.WorkingCopy,
                处理结果目录 = record.OutputDirectory,
                目标像素 = $"{record.TargetWidthPixels} × {record.TargetHeightPixels}",
                record.Warnings
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

    [McpServerTool(Name = "duanxing_save_review")]
    [Description("保存端行人工复核结论。只有明确批准后，任务才可以导出为生产版。")]
    public string 保存复核结论(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录,
        [Description("复核人员姓名。")]
        string 复核人,
        [Description("通过填写 true，退回修改填写 false。")]
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
                review.Status,
                review.Reviewer,
                review.Comment
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
    [Description("返回端行员工可以直接复制使用的中文作图口令。")]
    public string 获取中文作图口令()
        => """
            1. 检查环境
            2. 开始处理这张图：D:\样板\原图.tif，成品 200×200 mm，2540 DPI，平铺无缝，输出 TIFF，复核人张三
            3. 把当前图做成 1/2 错位无缝，先出预览，不要覆盖原图
            4. 按 S 型折光线处理：线宽___，间距___，角度___，同时输出 TIFF 和 AI
            5. 检查尺寸、DPI、接缝和文件名，生成复核记录
            6. 复核通过，导出生产版
            """;
}
