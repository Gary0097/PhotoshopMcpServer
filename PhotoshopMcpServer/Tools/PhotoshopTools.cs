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
        "Executes arbitrary JavaScript in Adobe Photoshop's scripting engine. " +
        "This is the primary tool for all Photoshop automation. " +
        "The script has access to the full Photoshop DOM via 'app' (the Application object). " +
        "Example: 'app.activeDocument.flatten(); app.activeDocument.save();' " +
        "Returns the string result of the last evaluated expression, or empty string.")]
    public string ExecuteJavaScript(
        [Description(
            "The JavaScript code to execute in Photoshop. " +
            "Use 'app' to access the Photoshop Application object. " +
            "Use 'app.activeDocument' for the current document. " +
            "You can return values by making the last expression evaluate to a string. " +
            "Example scripts:\n" +
            "  - Get document name: 'app.activeDocument.name'\n" +
            "  - Create new doc: 'app.documents.add(800, 600, 72, \"New Doc\")'\n" +
            "  - Save as PNG: 'var opts = new ExportOptionsSaveForWeb(); opts.format = SaveDocumentType.PNG; app.activeDocument.exportDocument(new File(\"/path/out.png\"), ExportType.SAVEFORWEB, opts);'\n" +
            "  - Apply filter: 'app.activeDocument.activeLayer.applySharpen()'\n" +
            "  - Get layer list: 'var names=[]; for(var i=0;i<app.activeDocument.layers.length;i++) names.push(app.activeDocument.layers[i].name); names.join(\",\")'")]
        string script)
    {
        if (!_allowArbitraryScripts)
            return "生产模式已关闭任意脚本。请使用端行中文作图流程；需要调试时联系实施人员。";

        var result = _photoshopService.ExecuteJavaScriptWithResult(script);
        if (!result.Success)
            return $"执行失败：{result.ErrorMessage}";
        return result.Result;
    }

    [McpServerTool(Name = "photoshop_is_running")]
    [Description(
        "Checks whether Adobe Photoshop is currently running and accessible via COM. " +
        "Returns 'true' if Photoshop is running, 'false' otherwise.")]
    public string IsPhotoshopRunning()
        => _photoshopService.IsPhotoshopRunning().ToString().ToLowerInvariant();

    [McpServerTool(Name = "photoshop_launch")]
    [Description(
        "Launches Adobe Photoshop if it is not already running, " +
        "or connects to the running instance. " +
        "Must be called before executing JavaScript if Photoshop is not open.")]
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
    [Description(
        "Gets the version string of the running Adobe Photoshop instance. " +
        "Useful for verifying connectivity and checking Photoshop version compatibility.")]
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
    [Description(
        "Gets information about the currently active (frontmost) Photoshop document. " +
        "Returns document name, file path, dimensions, color mode, and resolution. " +
        "Returns an error message if no document is open.")]
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
    [Description(
        "Gets a list of all currently open Photoshop document names. " +
        "Returns a comma-separated list of document names, or 'No documents open' if none.")]
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
    [Description(
        "Opens an image file in Photoshop. " +
        "Provide the full absolute path to the image file. " +
        "Supports PSD, JPEG, PNG, TIFF, BMP, GIF, and other formats Photoshop can open.")]
    public string OpenDocument(
        [Description("The full absolute path to the image file to open. Example: C:\\Images\\photo.jpg")]
        string filePath)
    {
        if (!_allowArbitraryScripts)
            return ProductionWriteBlocked();

        var script = $"app.open(new File(\"{filePath.Replace("\\", "/")}\"));";
        var result = _photoshopService.ExecuteJavaScriptWithResult(script);
        if (!result.Success)
            return $"打开文件失败：{result.ErrorMessage}";
        return $"已打开：{filePath}";
    }

    [McpServerTool(Name = "photoshop_save_active_document")]
    [Description(
        "Saves the currently active Photoshop document in its current format. " +
        "For PSD files, saves as PSD. " +
        "Use ExecuteJavaScript for advanced save options (Save As, Export, etc.).")]
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
    [Description(
        "Creates a new Photoshop document with the specified dimensions. " +
        "Returns confirmation with the document name.")]
    public string CreateNewDocument(
        [Description("Width of the new document in pixels.")]
        int width,
        [Description("Height of the new document in pixels.")]
        int height,
        [Description("Resolution in pixels per inch (PPI). Common values: 72 (screen), 300 (print).")]
        double resolution,
        [Description("Name for the new document.")]
        string documentName)
    {
        if (!_allowArbitraryScripts)
            return ProductionWriteBlocked();

        var script =
            $"var doc = app.documents.add({width}, {height}, {resolution}, \"{documentName}\"); doc.name;";
        var result = _photoshopService.ExecuteJavaScriptWithResult(script);
        if (!result.Success)
            return $"创建文档失败：{result.ErrorMessage}";
        return $"已创建文档“{documentName}”，像素尺寸 {width} × {height}，图像精度 {resolution}。";
    }

    [McpServerTool(Name = "photoshop_export_png")]
    [Description(
        "Exports the active Photoshop document as a PNG file to the specified path. " +
        "Uses Save for Web with PNG-24 settings (lossless, supports transparency).")]
    public string ExportAsPng(
        [Description("The full absolute path where the PNG file should be saved. Example: C:\\Output\\result.png")]
        string outputPath)
    {
        if (!_allowArbitraryScripts)
            return ProductionWriteBlocked();

        var normalizedPath = outputPath.Replace("\\", "/");
        var script =
            $"var exportOptions = new ExportOptionsSaveForWeb();" +
            $"exportOptions.format = SaveDocumentType.PNG;" +
            $"exportOptions.PNG8 = false;" +
            $"exportOptions.transparency = true;" +
            $"app.activeDocument.exportDocument(new File(\"{normalizedPath}\"), ExportType.SAVEFORWEB, exportOptions);" +
            $"\"{outputPath}\"";
        var result = _photoshopService.ExecuteJavaScriptWithResult(script);
        if (!result.Success)
            return $"导出图片失败：{result.ErrorMessage}";
        return $"图片已导出到：{outputPath}";
    }

    [McpServerTool(Name = "photoshop_export_jpeg")]
    [Description(
        "Exports the active Photoshop document as a JPEG file to the specified path. " +
        "Quality ranges from 0 (lowest) to 100 (highest).")]
    public string ExportAsJpeg(
        [Description("The full absolute path where the JPEG file should be saved.")]
        string outputPath,
        [Description("JPEG quality from 0 (lowest) to 100 (highest). Default is 80.")]
        int quality)
    {
        if (!_allowArbitraryScripts)
            return ProductionWriteBlocked();

        var normalizedPath = outputPath.Replace("\\", "/");
        var script =
            $"var exportOptions = new ExportOptionsSaveForWeb();" +
            $"exportOptions.format = SaveDocumentType.JPEG;" +
            $"exportOptions.quality = {quality};" +
            $"app.activeDocument.exportDocument(new File(\"{normalizedPath}\"), ExportType.SAVEFORWEB, exportOptions);" +
            $"\"{outputPath}\"";
        var result = _photoshopService.ExecuteJavaScriptWithResult(script);
        if (!result.Success)
            return $"导出图片失败：{result.ErrorMessage}";
        return $"图片已导出，质量 {quality}，位置：{outputPath}";
    }

    [McpServerTool(Name = "photoshop_get_layers")]
    [Description(
        "Gets a JSON-like summary of all layers in the active Photoshop document. " +
        "Returns layer names, types, and visibility for up to the top-level layers.")]
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
