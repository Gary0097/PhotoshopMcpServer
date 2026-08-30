using System.ComponentModel;
using ModelContextProtocol.Server;
using PhotoshopMcpServer.Models;
using PhotoshopMcpServer.Services;

namespace PhotoshopMcpServer.Tools;

// MCP tools for controlling Adobe Photoshop via COM automation.
// The primary tool is ExecuteJavaScript which allows full Photoshop scripting.
// Additional convenience tools provide structured access to common operations.
[McpServerToolType]
public class PhotoshopTools
{
    private readonly IPhotoshopService _photoshopService;
    private readonly bool _allowArbitraryScripts;

    public PhotoshopTools(IPhotoshopService photoshopService)
        : this(
            photoshopService,
            string.Equals(
                Environment.GetEnvironmentVariable("DUANXING_ALLOW_ARBITRARY_SCRIPTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
    {
    }

    public PhotoshopTools(IPhotoshopService photoshopService, bool allowArbitraryScripts)
    {
        _photoshopService = photoshopService;
        _allowArbitraryScripts = allowArbitraryScripts;
    }

    [McpServerTool(Name = "photoshop_execute_script")]
    [Description(
        "仅供授权实施人员调试的 Photoshop 任意脚本工具。生产模式默认关闭；客户作图必须使用端行业务工具，避免覆盖原图或绕过复核。")]
    public string ExecuteJavaScript(
        [Description("实施人员审核后的 Photoshop 脚本内容。客户日常不得填写。")]
        string 脚本内容)
    {
        if (!_allowArbitraryScripts)
            return "生产模式已关闭任意脚本。请使用端行中文作图流程；需要调试时联系实施人员。";

        var result = _photoshopService.ExecuteJavaScriptWithResult(脚本内容);
        if (!result.Success)
            return $"执行失败：{result.ErrorMessage}";
        return result.Result;
    }

    [McpServerTool(Name = "photoshop_is_running")]
    [Description("检查 Photoshop 是否已经启动并可以连接；返回“是”或“否”。")]
    public string IsPhotoshopRunning()
        => _photoshopService.IsPhotoshopRunning() ? "是" : "否";

    [McpServerTool(Name = "photoshop_launch")]
    [Description("启动 Photoshop，或连接到已经打开的 Photoshop。")]
    public string LaunchPhotoshop()
    {
        try
        {
            _photoshopService.LaunchPhotoshop();
            return "Photoshop 已启动并连接成功。";
        }
        catch (Exception exception)
        {
            return $"Photoshop 启动失败：{exception.Message}";
        }
    }

    [McpServerTool(Name = "photoshop_get_version")]
    [Description("读取当前连接的 Photoshop 版本，用于检查是否兼容 2026 版本。")]
    public string GetPhotoshopVersion()
    {
        try
        {
            return _photoshopService.GetPhotoshopVersion();
        }
        catch (Exception exception)
        {
            return $"读取 Photoshop 版本失败：{exception.Message}";
        }
    }

    [McpServerTool(Name = "photoshop_get_active_document")]
    [Description("读取 Photoshop 当前图片的名称、位置、像素尺寸、颜色模式和图像精度。")]
    public string GetActiveDocumentInfo()
    {
        try
        {
            var documentInfo = _photoshopService.GetActiveDocumentInfo();
            return
                $"文件名称：{documentInfo.Name}\n" +
                $"文件位置：{documentInfo.FilePath}\n" +
                $"像素尺寸：{documentInfo.Width} × {documentInfo.Height}\n" +
                $"颜色模式：{documentInfo.ColorMode}\n" +
                $"图像精度：{documentInfo.Resolution}";
        }
        catch (Exception exception)
        {
            return $"读取当前文档失败：{exception.Message}";
        }
    }

    [McpServerTool(Name = "photoshop_get_open_documents")]
    [Description("列出 Photoshop 当前打开的所有图片名称；没有图片时返回中文提示。")]
    public string GetOpenDocuments()
    {
        try
        {
            var documentNames = _photoshopService.GetOpenDocumentNames();
            if (documentNames.Count == 0)
                return "当前没有打开的文档。";
            return string.Join(", ", documentNames);
        }
        catch (Exception exception)
        {
            return $"读取文档列表失败：{exception.Message}";
        }
    }

    [McpServerTool(Name = "photoshop_open_document")]
    [Description("通用打开图片工具，仅限实施调试。生产作图请使用“打开任务工作副本”。")]
    public string OpenDocument(
        [Description("要打开的图片完整位置。")]
        string 文件路径)
    {
        if (!_allowArbitraryScripts)
            return ProductionWriteBlocked();

        var script = $"app.open(new File(\"{文件路径.Replace("\\", "/")}\"));";
        var result = _photoshopService.ExecuteJavaScriptWithResult(script);
        if (!result.Success)
            return $"打开文件失败：{result.ErrorMessage}";
        return $"已打开：{文件路径}";
    }

    [McpServerTool(Name = "photoshop_save_active_document")]
    [Description("通用保存当前图片工具，仅限实施调试。生产作图请使用端行版本化保存和导出流程。")]
    public string SaveActiveDocument()
    {
        if (!_allowArbitraryScripts)
            return ProductionWriteBlocked();

        var result = _photoshopService.ExecuteJavaScriptWithResult("app.activeDocument.save();");
        if (!result.Success)
            return $"保存文档失败：{result.ErrorMessage}";
        return "文档保存成功。";
    }

    [McpServerTool(Name = "photoshop_create_document")]
    [Description("通用新建 Photoshop 图片工具，仅限实施调试。")]
    public string CreateNewDocument(
        [Description("新图片宽度，单位为像素。")]
        int 宽度像素,
        [Description("新图片高度，单位为像素。")]
        int 高度像素,
        [Description("图像精度，每英寸像素数。")]
        double 图像精度,
        [Description("新图片名称。")]
        string 图片名称)
    {
        if (!_allowArbitraryScripts)
            return ProductionWriteBlocked();

        var script =
            $"var doc = app.documents.add({宽度像素}, {高度像素}, {图像精度}, \"{图片名称}\"); doc.name;";
        var result = _photoshopService.ExecuteJavaScriptWithResult(script);
        if (!result.Success)
            return $"创建文档失败：{result.ErrorMessage}";
        return $"已创建图片“{图片名称}”，像素尺寸 {宽度像素} × {高度像素}，图像精度 {图像精度}。";
    }

    [McpServerTool(Name = "photoshop_export_png")]
    [Description("通用导出 PNG 图片工具，仅限实施调试。生产作图请使用“一键导出生产版”。")]
    public string ExportAsPng(
        [Description("导出文件的完整保存位置。")]
        string 输出路径)
    {
        if (!_allowArbitraryScripts)
            return ProductionWriteBlocked();

        var normalizedPath = 输出路径.Replace("\\", "/");
        var script =
            $"var exportOptions = new ExportOptionsSaveForWeb();" +
            $"exportOptions.format = SaveDocumentType.PNG;" +
            $"exportOptions.PNG8 = false;" +
            $"exportOptions.transparency = true;" +
            $"app.activeDocument.exportDocument(new File(\"{normalizedPath}\"), ExportType.SAVEFORWEB, exportOptions);" +
            $"\"{输出路径}\"";
        var result = _photoshopService.ExecuteJavaScriptWithResult(script);
        if (!result.Success)
            return $"导出图片失败：{result.ErrorMessage}";
        return $"图片已导出到：{输出路径}";
    }

    [McpServerTool(Name = "photoshop_export_jpeg")]
    [Description("通用导出 JPEG 图片工具，仅限实施调试。生产作图请使用“一键导出生产版”。")]
    public string ExportAsJpeg(
        [Description("导出文件的完整保存位置。")]
        string 输出路径,
        [Description("图片质量，0 最低，100 最高。")]
        int 图片质量)
    {
        if (!_allowArbitraryScripts)
            return ProductionWriteBlocked();

        var normalizedPath = 输出路径.Replace("\\", "/");
        var script =
            $"var exportOptions = new ExportOptionsSaveForWeb();" +
            $"exportOptions.format = SaveDocumentType.JPEG;" +
            $"exportOptions.quality = {图片质量};" +
            $"app.activeDocument.exportDocument(new File(\"{normalizedPath}\"), ExportType.SAVEFORWEB, exportOptions);" +
            $"\"{输出路径}\"";
        var result = _photoshopService.ExecuteJavaScriptWithResult(script);
        if (!result.Success)
            return $"导出图片失败：{result.ErrorMessage}";
        return $"图片已导出，质量 {图片质量}，位置：{输出路径}";
    }

    [McpServerTool(Name = "photoshop_get_layers")]
    [Description("读取 Photoshop 当前图片的顶层图层名称、类型和是否可见。")]
    public string GetLayerInfo()
    {
        var script =
            "(function() {" +
            "  var doc = app.activeDocument;" +
            "  var result = [];" +
            "  for (var i = 0; i < doc.layers.length; i++) {" +
            "    var layer = doc.layers[i];" +
            "    result.push(layer.name + ' [' + layer.typename + ', visible=' + layer.visible + ']');" +
            "  }" +
            "  return result.join('\\n');" +
            "})();";
        var operationResult = _photoshopService.ExecuteJavaScriptWithResult(script);
        if (!operationResult.Success)
            return $"读取图层失败：{operationResult.ErrorMessage}";
        return operationResult.Result;
    }

    private static string ProductionWriteBlocked()
        => "生产模式不允许使用通用写入工具。请使用端行任务流程，避免覆盖原图或绕过人工复核。";
}
