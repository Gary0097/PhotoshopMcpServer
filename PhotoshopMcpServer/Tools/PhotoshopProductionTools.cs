using System.ComponentModel;
using ModelContextProtocol.Server;
using PhotoshopMcpServer.Services;

namespace PhotoshopMcpServer.Tools;

[McpServerToolType]
public class PhotoshopProductionTools(
    IPhotoshopService photoshopService,
    ITaskWorkspaceService taskWorkspaceService)
{
    [McpServerTool]
    [Description("在 Photoshop 2026 中打开端行任务的工作副本。只接受包含 task.json 的有效任务目录。")]
    public string 打开任务工作副本(
        [Description("端行任务目录，例如 D:\\端行输出\\DX-任务编号_木纹无缝。")]
        string 任务目录)
    {
        try
        {
            var task = taskWorkspaceService.LoadTask(任务目录);
            var script = $"app.open(new File(\"{EscapePath(task.WorkingCopy)}\"));";
            var result = photoshopService.ExecuteJavaScriptWithResult(script);
            if (!result.Success)
                return $"打开失败：{result.ErrorMessage}";
            return $"已打开工作副本：{task.WorkingCopy}\n原图未修改。";
        }
        catch (Exception exception)
        {
            return $"无法打开任务：{exception.Message}";
        }
    }

    [McpServerTool]
    [Description(
        "按照任务中确认的毫米尺寸和 DPI 设置当前 Photoshop 工作副本的像素尺寸，" +
        "使用双三次重采样，并把结果另存为任务目录中的规格化 PSD。")]
    public string 按任务规格设置尺寸(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录)
    {
        try
        {
            var task = taskWorkspaceService.LoadTask(任务目录);
            var outputPath = Path.Combine(
                task.OutputDirectory,
                $"{SanitizeFileName(task.TaskName)}_{task.TargetWidthPixels}x{task.TargetHeightPixels}_{task.Dpi}dpi_规格化.psd");
            var script = "(function(){" +
                $"var sourceFile=new File(\"{EscapePath(task.WorkingCopy)}\");" +
                "var doc=app.open(sourceFile);" +
                $"doc.resizeImage(UnitValue({task.TargetWidthPixels},'px')," +
                $"UnitValue({task.TargetHeightPixels},'px'),{task.Dpi},ResampleMethod.BICUBIC);" +
                "var options=new PhotoshopSaveOptions();options.layers=true;" +
                $"doc.saveAs(new File(\"{EscapePath(outputPath)}\"),options,true,Extension.LOWERCASE);" +
                $"return \"{EscapeJavaScript(outputPath)}\";" +
                "})();";
            var result = photoshopService.ExecuteJavaScriptWithResult(script);
            if (!result.Success)
                return $"设置尺寸失败：{result.ErrorMessage}";
            return $"规格设置完成：{task.TargetWidthPixels}×{task.TargetHeightPixels} 像素，" +
                $"{task.Dpi} DPI。\n已保存：{outputPath}";
        }
        catch (Exception exception)
        {
            return $"无法设置任务规格：{exception.Message}";
        }
    }

    [McpServerTool]
    [Description(
        "为端行任务生成平铺无缝检查图：复制工作副本，将图层水平和垂直各偏移一半并环绕，" +
        "把原来的四边接缝移动到画面中央，便于人工检查和修补。不会覆盖原图。")]
    public string 生成平铺无缝检查图(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录)
    {
        try
        {
            var task = taskWorkspaceService.LoadTask(任务目录);
            var outputPath = Path.Combine(
                task.OutputDirectory,
                $"{SanitizeFileName(task.TaskName)}_平铺无缝检查.psd");
            var script = "(function(){" +
                $"var doc=app.open(new File(\"{EscapePath(task.WorkingCopy)}\"));" +
                "if(doc.layers.length>1){doc.flatten();}" +
                "var horizontal=Math.round(doc.width.as('px')/2);" +
                "var vertical=Math.round(doc.height.as('px')/2);" +
                "doc.activeLayer.applyOffset(horizontal,vertical,OffsetUndefinedAreas.WRAPAROUND);" +
                "var options=new PhotoshopSaveOptions();options.layers=true;" +
                $"doc.saveAs(new File(\"{EscapePath(outputPath)}\"),options,true,Extension.LOWERCASE);" +
                $"return \"{EscapeJavaScript(outputPath)}\";" +
                "})();";
            var result = photoshopService.ExecuteJavaScriptWithResult(script);
            if (!result.Success)
                return $"生成检查图失败：{result.ErrorMessage}";
            return $"平铺无缝检查图已生成：{outputPath}\n请重点检查画面中央的横向和纵向接缝。";
        }
        catch (Exception exception)
        {
            return $"无法生成无缝检查图：{exception.Message}";
        }
    }

    [McpServerTool]
    [Description("检查端行任务是否已通过人工复核。未通过时禁止称为生产版或执行最终交付。")]
    public string 检查是否允许导出生产版(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录)
    {
        try
        {
            return taskWorkspaceService.IsApproved(任务目录)
                ? "允许导出：最新人工复核结论为通过。"
                : "禁止导出生产版：尚无有效的人工复核通过记录。";
        }
        catch (Exception exception)
        {
            return $"无法检查复核状态：{exception.Message}";
        }
    }

    private static string EscapePath(string path)
        => EscapeJavaScript(Path.GetFullPath(path).Replace('\\', '/'));

    private static string EscapeJavaScript(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string SanitizeFileName(string value)
    {
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');
        return value;
    }
}
