using System.ComponentModel;
using ModelContextProtocol.Server;
using PhotoshopMcpServer.Services;

namespace PhotoshopMcpServer.Tools;

[McpServerToolType]
public class PhotoshopProductionTools(
    IPhotoshopService photoshopService,
    ITaskWorkspaceService taskWorkspaceService)
{
    [McpServerTool(Name = "duanxing_open_working_copy")]
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

    [McpServerTool(Name = "duanxing_set_production_dimensions")]
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

    [McpServerTool(Name = "duanxing_create_seamless_check")]
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
                $"doc.resizeImage(UnitValue({task.TargetWidthPixels},'px')," +
                $"UnitValue({task.TargetHeightPixels},'px'),{task.Dpi},ResampleMethod.BICUBIC);" +
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

    [McpServerTool(Name = "duanxing_create_half_drop_check")]
    [Description(
        "为端行任务生成 1/2 错位拼接检查图：建立 2×2 画布，右列上下错开半个图案高度，" +
        "用于检查二分之一错位后的接缝和连续性。不会覆盖原图。")]
    public string 生成二分之一错位检查图(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录)
    {
        try
        {
            var task = taskWorkspaceService.LoadTask(任务目录);
            var outputPath = Path.Combine(
                task.OutputDirectory,
                $"{SanitizeFileName(task.TaskName)}_二分之一错位检查.psd");
            var script = "(function(){" +
                $"var source=app.open(new File(\"{EscapePath(task.WorkingCopy)}\"));" +
                $"source.resizeImage(UnitValue({task.TargetWidthPixels},'px')," +
                $"UnitValue({task.TargetHeightPixels},'px'),{task.Dpi},ResampleMethod.BICUBIC);" +
                "if(source.layers.length>1){source.flatten();}" +
                "var width=source.width.as('px'),height=source.height.as('px');" +
                "source.selection.selectAll();source.selection.copy(true);" +
                "var result=app.documents.add(width*2,height*2,source.resolution," +
                "'1-2错位检查',NewDocumentMode.RGB,DocumentFill.TRANSPARENT);" +
                "function pasteAt(x,y){app.activeDocument=result;var layer=result.paste();" +
                "layer.translate(x+width/2-width,y+height/2-height);}" +
                "pasteAt(0,0);pasteAt(0,height);" +
                "pasteAt(width,-height/2);pasteAt(width,height/2);pasteAt(width,height*1.5);" +
                "var options=new PhotoshopSaveOptions();options.layers=true;" +
                $"result.saveAs(new File(\"{EscapePath(outputPath)}\"),options,true,Extension.LOWERCASE);" +
                $"return \"{EscapeJavaScript(outputPath)}\";" +
                "})();";
            var result = photoshopService.ExecuteJavaScriptWithResult(script);
            if (!result.Success)
                return $"生成 1/2 错位检查图失败：{result.ErrorMessage}";
            return $"1/2 错位检查图已生成：{outputPath}\n" +
                "请检查中间竖向接缝，以及右列上下错位后的横向连续性。";
        }
        catch (Exception exception)
        {
            return $"无法生成 1/2 错位检查图：{exception.Message}";
        }
    }

    [McpServerTool(Name = "duanxing_create_task_preview")]
    [Description(
        "一键生成端行工艺检查版。自动读取任务尺寸、DPI 和拼接方式：" +
        "不拼接时生成规格化 PSD，平铺时生成无缝检查图，1/2 错位时生成错位检查图。")]
    public string 一键生成工艺检查版(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录)
    {
        try
        {
            var task = taskWorkspaceService.LoadTask(任务目录);
            return task.TilingMode switch
            {
                "平铺" => 生成平铺无缝检查图(任务目录),
                "1/2错位" => 生成二分之一错位检查图(任务目录),
                _ => 按任务规格设置尺寸(任务目录)
            };
        }
        catch (Exception exception)
        {
            return $"无法生成工艺检查版：{exception.Message}";
        }
    }

    [McpServerTool(Name = "duanxing_create_review_preview")]
    [Description(
        "把当前任务最新处理结果生成轻量 PNG 复核预览，供 Codex 直接展示。" +
        "只读取并复制结果，不修改原结果，不把预览当作生产文件。")]
    public string 生成复核预览图(
        [Description("当前端行任务目录。")]
        string 任务目录,
        [Description("预览图最长边像素，默认 1600；通常无需客户填写。")]
        int 最长边像素 = 1600)
    {
        try
        {
            if (最长边像素 is < 800 or > 4000)
                throw new ArgumentOutOfRangeException(
                    nameof(最长边像素),
                    "预览图最长边必须在 800 到 4000 像素之间。");
            var summary = taskWorkspaceService.BuildReviewSummary(任务目录);
            if (string.IsNullOrWhiteSpace(summary.LatestResultFile) ||
                !File.Exists(summary.LatestResultFile))
                return "还没有可预览的处理结果，请先生成检查版。";

            var previewDirectory = Path.Combine(
                Path.GetFullPath(任务目录),
                "03_复核记录",
                "预览图");
            Directory.CreateDirectory(previewDirectory);
            var outputPath = Path.Combine(
                previewDirectory,
                $"复核预览_{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");
            var script = "(function(){" +
                $"var source=app.open(new File(\"{EscapePath(summary.LatestResultFile)}\"));" +
                "var preview=source.duplicate('端行复核预览',true);" +
                "source.close(SaveOptions.DONOTSAVECHANGES);app.activeDocument=preview;" +
                "try{if(preview.mode!=DocumentMode.RGB){preview.changeMode(ChangeMode.RGB);}}catch(e){}" +
                "try{preview.bitsPerChannel=BitsPerChannelType.EIGHT;}catch(e){}" +
                "var w=preview.width.as('px'),h=preview.height.as('px');" +
                $"var maxEdge={最长边像素};" +
                "if(w>maxEdge||h>maxEdge){if(w>=h){preview.resizeImage(UnitValue(maxEdge,'px'),null,null,ResampleMethod.BICUBICSHARPER);}" +
                "else{preview.resizeImage(null,UnitValue(maxEdge,'px'),null,ResampleMethod.BICUBICSHARPER);}}" +
                "var options=new ExportOptionsSaveForWeb();options.format=SaveDocumentType.PNG;" +
                "options.PNG8=false;options.transparency=true;" +
                $"preview.exportDocument(new File(\"{EscapePath(outputPath)}\"),ExportType.SAVEFORWEB,options);" +
                "preview.close(SaveOptions.DONOTSAVECHANGES);" +
                $"return \"{EscapeJavaScript(outputPath)}\";" +
                "})();";
            var result = photoshopService.ExecuteJavaScriptWithResult(script);
            if (!result.Success)
                return $"复核预览生成失败：{result.ErrorMessage}";
            return $"复核预览已生成：{outputPath}\n" +
                "请直接显示这张图片，并提醒客户预览仅用于看效果，不是生产文件。";
        }
        catch (Exception exception)
        {
            return $"无法生成复核预览：{exception.Message}";
        }
    }

    [McpServerTool(Name = "duanxing_create_extension_canvas")]
    [Description(
        "创建补图/扩展画布：按任务记录的目标像素扩展 Photoshop 工作副本画布，" +
        "保留原图内容并把新增透明区域留给 AI 补图或人工修补。不会覆盖原图。")]
    public string 创建补图扩展画布(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录)
    {
        try
        {
            var task = taskWorkspaceService.LoadTask(任务目录);
            var outputPath = Path.Combine(
                task.OutputDirectory,
                $"{SanitizeFileName(task.TaskName)}_{task.TargetWidthPixels}x{task.TargetHeightPixels}_补图画布.psd");
            var script = "(function(){" +
                $"var doc=app.open(new File(\"{EscapePath(task.WorkingCopy)}\"));" +
                "var currentWidth=Math.round(doc.width.as('px'));" +
                "var currentHeight=Math.round(doc.height.as('px'));" +
                $"var targetWidth={task.TargetWidthPixels},targetHeight={task.TargetHeightPixels};" +
                "if(targetWidth<currentWidth||targetHeight<currentHeight){" +
                "throw new Error('目标画布不能小于原图，请检查成品尺寸和 DPI。');}" +
                "doc.resizeCanvas(UnitValue(targetWidth,'px'),UnitValue(targetHeight,'px')," +
                "AnchorPosition.MIDDLECENTER);" +
                "var marker=doc.artLayers.add();marker.name='AI补图或人工修补区域';" +
                "var options=new PhotoshopSaveOptions();options.layers=true;" +
                $"doc.saveAs(new File(\"{EscapePath(outputPath)}\"),options,true,Extension.LOWERCASE);" +
                $"return \"{EscapeJavaScript(outputPath)}\";" +
                "})();";
            var result = photoshopService.ExecuteJavaScriptWithResult(script);
            if (!result.Success)
                return $"创建补图画布失败：{result.ErrorMessage}";
            return $"补图扩展画布已创建：{outputPath}\n" +
                "新增透明区域可用于 AI 补图或人工修补，请保持原纹理方向和接缝连续。";
        }
        catch (Exception exception)
        {
            return $"无法创建补图扩展画布：{exception.Message}";
        }
    }

    [McpServerTool(Name = "duanxing_create_sharpen_preview")]
    [Description(
        "生成基础清晰化预览：在工作副本上复制图层并使用 Photoshop USM 锐化，" +
        "保留原始图层以便对比和回退。适合先做小样，不代表 AI 修复。")]
    public string 生成基础清晰化预览(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录,
        [Description("锐化数量，1 到 500，建议先用 80。")]
        double 锐化数量 = 80,
        [Description("半径，0.1 到 250 像素，建议先用 1.2。")]
        double 半径像素 = 1.2,
        [Description("阈值，0 到 255，建议先用 2。")]
        int 阈值 = 2)
    {
        try
        {
            if (锐化数量 is < 1 or > 500)
                throw new ArgumentOutOfRangeException(nameof(锐化数量), "锐化数量必须在 1 到 500 之间。");
            if (半径像素 is < 0.1 or > 250)
                throw new ArgumentOutOfRangeException(nameof(半径像素), "锐化半径必须在 0.1 到 250 像素之间。");
            if (阈值 is < 0 or > 255)
                throw new ArgumentOutOfRangeException(nameof(阈值), "阈值必须在 0 到 255 之间。");
            var task = taskWorkspaceService.LoadTask(任务目录);
            var outputPath = Path.Combine(
                task.OutputDirectory,
                $"{SanitizeFileName(task.TaskName)}_基础清晰化预览.psd");
            var script = "(function(){" +
                $"var doc=app.open(new File(\"{EscapePath(task.WorkingCopy)}\"));" +
                "var layer=doc.activeLayer.duplicate();layer.name='基础清晰化预览';" +
                $"layer.applyUnSharpMask({Format(锐化数量)},{Format(半径像素)},{阈值});" +
                "var options=new PhotoshopSaveOptions();options.layers=true;" +
                $"doc.saveAs(new File(\"{EscapePath(outputPath)}\"),options,true,Extension.LOWERCASE);" +
                $"return \"{EscapeJavaScript(outputPath)}\";" +
                "})();";
            var result = photoshopService.ExecuteJavaScriptWithResult(script);
            if (!result.Success)
                return $"清晰化预览生成失败：{result.ErrorMessage}";
            return $"基础清晰化预览已生成：{outputPath}\n" +
                $"参数：数量 {Format(锐化数量)}，半径 {Format(半径像素)} px，阈值 {阈值}。";
        }
        catch (Exception exception)
        {
            return $"无法生成清晰化预览：{exception.Message}";
        }
    }

    [McpServerTool(Name = "duanxing_check_export_approval")]
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

    [McpServerTool(Name = "duanxing_export_approved_production_file")]
    [Description(
        "把已经通过人工复核的 Photoshop 结果导出为生产版。" +
        "源文件必须位于当前任务的处理结果目录，生产版固定保存到该任务的“04_生产版”目录。")]
    public string 导出已批准生产版(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录,
        [Description("已经复核的结果文件完整路径，必须位于任务的“02_处理结果”目录。")]
        string 已复核结果文件,
        [Description("生产输出格式：PSD、PSB、TIFF、PNG 或 JPEG。")]
        string 输出格式,
        [Description("JPEG 质量 1 到 100；其他格式可填写 100。")]
        int JPEG质量 = 100)
    {
        try
        {
            var task = taskWorkspaceService.LoadTask(任务目录);
            if (!taskWorkspaceService.IsApproved(任务目录))
                return "禁止导出生产版：请先由指定人员复核，并保存“通过”结论。";
            var sourcePath = Path.GetFullPath(已复核结果文件);
            if (!File.Exists(sourcePath))
                return "无法导出：找不到已复核结果文件。";
            if (!IsPathInside(sourcePath, task.OutputDirectory))
                return "禁止导出：源文件必须位于当前任务的“02_处理结果”目录。";
            if (!taskWorkspaceService.IsResultApproved(任务目录, sourcePath))
                return "禁止导出生产版：这个文件不是复核时批准的版本，或文件内容已经改变。请重新生成复核单并明确通过。";

            var normalizedFormat = (输出格式 ?? string.Empty).Trim().ToUpperInvariant();
            var extension = normalizedFormat switch
            {
                "PSD" => ".psd",
                "PSB" => ".psb",
                "TIFF" or "TIF" => ".tif",
                "PNG" => ".png",
                "JPEG" or "JPG" => ".jpg",
                _ => throw new ArgumentException("生产输出格式只支持 PSD、PSB、TIFF、PNG 或 JPEG。")
            };
            if (JPEG质量 is < 1 or > 100)
                throw new ArgumentOutOfRangeException(nameof(JPEG质量), "JPEG 质量必须在 1 到 100 之间。");

            var productionDirectory = Path.Combine(Path.GetFullPath(任务目录), "04_生产版");
            Directory.CreateDirectory(productionDirectory);
            var outputPath = Path.Combine(
                productionDirectory,
                $"{Path.GetFileNameWithoutExtension(sourcePath)}_生产版{extension}");
            if (File.Exists(outputPath))
                return $"禁止覆盖已有生产版：{outputPath}\n请先确认旧文件或创建新任务版本。";

            var script = BuildProductionExportScript(
                sourcePath,
                outputPath,
                normalizedFormat,
                JPEG质量);
            var result = photoshopService.ExecuteJavaScriptWithResult(script);
            if (!result.Success)
                return $"生产版导出失败：{result.ErrorMessage}";
            return $"生产版已导出：{outputPath}\n来源：{sourcePath}\n复核状态：已通过。";
        }
        catch (Exception exception)
        {
            return $"无法导出生产版：{exception.Message}";
        }
    }

    [McpServerTool(Name = "duanxing_export_approved_simple")]
    [Description(
        "一键导出已经批准的 Photoshop 生产版。客户不需要提供结果文件路径；系统自动使用复核时锁定的文件，默认沿用任务输出格式。")]
    public string 一键导出生产版(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录,
        [Description("通常留空，自动沿用任务格式；明确需要时可填 PSD、PSB、TIFF、PNG 或 JPEG。")]
        string 输出格式 = "",
        [Description("JPEG 质量 1 到 100；非 JPEG 会忽略。")]
        int JPEG质量 = 100)
    {
        try
        {
            var task = taskWorkspaceService.LoadTask(任务目录);
            var approvedFile = taskWorkspaceService.GetApprovedResultFile(任务目录);
            var requestedFormat = string.IsNullOrWhiteSpace(输出格式)
                ? task.OutputFormat
                : 输出格式;
            return 导出已批准生产版(任务目录, approvedFile, requestedFormat, JPEG质量);
        }
        catch (Exception exception)
        {
            return $"无法一键导出生产版：{exception.Message}";
        }
    }

    private static string BuildProductionExportScript(
        string sourcePath,
        string outputPath,
        string outputFormat,
        int jpegQuality)
    {
        var prefix = "(function(){" +
            $"var doc=app.open(new File(\"{EscapePath(sourcePath)}\"));" +
            $"var output=new File(\"{EscapePath(outputPath)}\");";
        var save = outputFormat switch
        {
            "PSD" or "PSB" =>
                "var options=new PhotoshopSaveOptions();options.layers=true;" +
                "doc.saveAs(output,options,true,Extension.LOWERCASE);",
            "TIFF" or "TIF" =>
                "var options=new TiffSaveOptions();options.layers=true;" +
                "options.imageCompression=TIFFEncoding.TIFFLZW;" +
                "doc.saveAs(output,options,true,Extension.LOWERCASE);",
            "PNG" =>
                "var options=new ExportOptionsSaveForWeb();options.format=SaveDocumentType.PNG;" +
                "options.PNG8=false;options.transparency=true;" +
                "doc.exportDocument(output,ExportType.SAVEFORWEB,options);",
            "JPEG" or "JPG" =>
                "var options=new ExportOptionsSaveForWeb();options.format=SaveDocumentType.JPEG;" +
                $"options.quality={jpegQuality};" +
                "doc.exportDocument(output,ExportType.SAVEFORWEB,options);",
            _ => throw new ArgumentOutOfRangeException(nameof(outputFormat))
        };
        return prefix + save + $"return \"{EscapeJavaScript(outputPath)}\";" + "})();";
    }

    private static bool IsPathInside(string candidatePath, string allowedDirectory)
    {
        var candidate = Path.GetFullPath(candidatePath);
        var allowed = Path.GetFullPath(allowedDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return candidate.StartsWith(allowed, StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapePath(string path)
        => EscapeJavaScript(Path.GetFullPath(path).Replace('\\', '/'));

    private static string EscapeJavaScript(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Format(double value)
        => value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    private static string SanitizeFileName(string value)
    {
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');
        return value;
    }
}
