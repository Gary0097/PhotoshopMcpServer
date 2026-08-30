using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;
using PhotoshopMcpServer.Services;

namespace PhotoshopMcpServer.Tools;

[McpServerToolType]
public class IllustratorProductionTools(
    IIllustratorService illustratorService,
    ITaskWorkspaceService taskWorkspaceService)
{
    [McpServerTool(Name = "duanxing_create_straight_refraction_lines", Title = "生成直线折光线")]
    [Description(
        "按端行任务尺寸在 Illustrator 2026 中生成可编辑的直线折光线 AI 文件。" +
        "线宽、间距和角度均使用毫米/度数输入，输出自动保存在任务结果目录。")]
    public string 生成直线折光线AI(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录,
        [Description("线宽，单位 mm，必须大于 0。")]
        double 线宽毫米,
        [Description("相邻线中心之间的距离，单位 mm，必须大于线宽。")]
        double 间距毫米,
        [Description("线条角度，单位度，可填写 -180 到 180。")]
        double 角度)
    {
        try
        {
            ValidateLineParameters(线宽毫米, 间距毫米, 角度);
            var task = taskWorkspaceService.LoadTask(任务目录);
            var outputPath = Path.Combine(
                task.OutputDirectory,
                $"{SanitizeFileName(task.TaskName)}_直线折光线_{Format(角度)}度.ai");
            var script = BuildStraightLineScript(
                task.WidthMillimeters,
                task.HeightMillimeters,
                线宽毫米,
                间距毫米,
                角度,
                outputPath);
            var result = illustratorService.ExecuteJavaScriptWithResult(script);
            if (!result.Success)
                return $"直线折光线生成失败：{CustomerErrorFormatter.Format(result.ErrorMessage)}";
            return $"直线折光线 AI 已生成：{outputPath}\n" +
                $"参数：线宽 {Format(线宽毫米)} mm，间距 {Format(间距毫米)} mm，角度 {Format(角度)}°。";
        }
        catch (Exception exception)
        {
            return $"无法生成直线折光线：{CustomerErrorFormatter.Format(exception)}";
        }
    }

    [McpServerTool(Name = "duanxing_create_s_wave_refraction_lines", Title = "生成S型折光线")]
    [Description(
        "按端行任务尺寸在 Illustrator 2026 中生成可编辑的 S 型折光线 AI 文件。" +
        "支持线宽、行距、振幅、波长和整体角度，输出自动保存在任务结果目录。")]
    public string 生成S型折光线AI(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录,
        [Description("线宽，单位 mm，必须大于 0。")]
        double 线宽毫米,
        [Description("相邻 S 线中心之间的距离，单位 mm，必须大于线宽。")]
        double 行距毫米,
        [Description("S 线从中心到波峰的高度，单位 mm，必须大于 0。")]
        double 振幅毫米,
        [Description("一个完整 S 波的水平长度，单位 mm，必须大于 0。")]
        double 波长毫米,
        [Description("整体旋转角度，单位度，可填写 -180 到 180。")]
        double 角度)
    {
        try
        {
            ValidateLineParameters(线宽毫米, 行距毫米, 角度);
            if (振幅毫米 <= 0)
                throw new ArgumentException("振幅必须大于 0 毫米。");
            if (波长毫米 <= 0)
                throw new ArgumentException("波长必须大于 0 毫米。");
            var task = taskWorkspaceService.LoadTask(任务目录);
            var outputPath = Path.Combine(
                task.OutputDirectory,
                $"{SanitizeFileName(task.TaskName)}_S型折光线.ai");
            var script = BuildWaveLineScript(
                task.WidthMillimeters,
                task.HeightMillimeters,
                线宽毫米,
                行距毫米,
                振幅毫米,
                波长毫米,
                角度,
                outputPath);
            var result = illustratorService.ExecuteJavaScriptWithResult(script);
            if (!result.Success)
                return $"S 型折光线生成失败：{CustomerErrorFormatter.Format(result.ErrorMessage)}";
            return $"S 型折光线 AI 已生成：{outputPath}\n" +
                $"参数：线宽 {Format(线宽毫米)} mm，行距 {Format(行距毫米)} mm，" +
                $"振幅 {Format(振幅毫米)} mm，波长 {Format(波长毫米)} mm，角度 {Format(角度)}°。";
        }
        catch (Exception exception)
        {
            return $"无法生成 S 型折光线：{CustomerErrorFormatter.Format(exception)}";
        }
    }

    [McpServerTool(Name = "duanxing_trace_wave_reference", Title = "提取原图波纹矢量")]
    [Description(
        "从端行任务原图提取大尺度波纹，生成可编辑 AI 和 SVG 矢量候选。" +
        "自动降低描摹分辨率并过滤细密底纹，适合客户说“把原图波纹做成矢量”时使用；" +
        "结果仍需在 Illustrator 中复核走势和无缝衔接。")]
    public string 提取原图波纹矢量候选(
        [Description("包含 task.json 的端行任务目录。")]
        string 任务目录,
        [Description("明暗分界，0 到 255；通常使用默认 128，不需要客户填写。")]
        int 明暗分界 = 128,
        [Description("描摹精度，每英寸像素数；通常使用默认 72，不需要客户填写。")]
        double 描摹精度 = 72)
    {
        try
        {
            if (明暗分界 is < 0 or > 255)
                throw new ArgumentException("明暗分界必须在 0 到 255 之间。");
            if (描摹精度 is < 36 or > 300)
                throw new ArgumentException("描摹精度必须在 36 到 300 之间。");
            var task = taskWorkspaceService.LoadTask(任务目录);
            var safeName = SanitizeFileName(task.TaskName);
            var illustratorPath = Path.Combine(task.OutputDirectory, $"{safeName}_原图波纹矢量候选.ai");
            var svgPath = Path.Combine(task.OutputDirectory, $"{safeName}_原图波纹矢量候选.svg");
            var script = BuildWaveTraceScript(
                task.WidthMillimeters,
                task.HeightMillimeters,
                task.WorkingCopy,
                illustratorPath,
                svgPath,
                明暗分界,
                描摹精度);
            var result = illustratorService.ExecuteJavaScriptWithResult(script);
            if (!result.Success)
                return $"原图波纹矢量候选生成失败：{CustomerErrorFormatter.Format(result.ErrorMessage)}";
            return $"原图波纹矢量候选已生成：{illustratorPath}\n" +
                $"通用矢量副本已生成：{svgPath}\n" +
                "请检查波纹走势、路径数量和四边衔接；这一步是候选描摹，不代表已经验收。";
        }
        catch (Exception exception)
        {
            return $"无法提取原图波纹矢量：{CustomerErrorFormatter.Format(exception)}";
        }
    }

    private static string BuildStraightLineScript(
        double widthMillimeters,
        double heightMillimeters,
        double lineWidthMillimeters,
        double spacingMillimeters,
        double angleDegrees,
        string outputPath)
        => "(function(){" +
            $"var width={Points(widthMillimeters)},height={Points(heightMillimeters)};" +
            $"var stroke={Points(lineWidthMillimeters)},spacing={Points(spacingMillimeters)};" +
            $"var angle={Format(angleDegrees)}*Math.PI/180;" +
            "var doc=app.documents.add(DocumentColorSpace.RGB,width,height);" +
            "var black=new GrayColor();black.gray=100;" +
            "var diagonal=Math.sqrt(width*width+height*height)*1.5;" +
            "var normalX=Math.cos(angle+Math.PI/2),normalY=Math.sin(angle+Math.PI/2);" +
            "var directionX=Math.cos(angle),directionY=Math.sin(angle);" +
            "for(var offset=-diagonal;offset<=diagonal;offset+=spacing){" +
            "var centerX=width/2+offset*normalX,centerY=height/2+offset*normalY;" +
            "var line=doc.pathItems.add();line.stroked=true;line.filled=false;" +
            "line.strokeWidth=stroke;line.strokeColor=black;" +
            "line.setEntirePath([[centerX-diagonal*directionX,centerY-diagonal*directionY]," +
            "[centerX+diagonal*directionX,centerY+diagonal*directionY]]);}" +
            $"doc.saveAs(new File(\"{EscapePath(outputPath)}\"));" +
            $"return \"{EscapeJavaScript(outputPath)}\";" +
            "})();";

    private static string BuildWaveLineScript(
        double widthMillimeters,
        double heightMillimeters,
        double lineWidthMillimeters,
        double rowSpacingMillimeters,
        double amplitudeMillimeters,
        double wavelengthMillimeters,
        double angleDegrees,
        string outputPath)
        => "(function(){" +
            $"var width={Points(widthMillimeters)},height={Points(heightMillimeters)};" +
            $"var stroke={Points(lineWidthMillimeters)},rowSpacing={Points(rowSpacingMillimeters)};" +
            $"var amplitude={Points(amplitudeMillimeters)},wavelength={Points(wavelengthMillimeters)};" +
            $"var angle={Format(angleDegrees)}*Math.PI/180;" +
            "var doc=app.documents.add(DocumentColorSpace.RGB,width,height);" +
            "var black=new GrayColor();black.gray=100;" +
            "var centerX=width/2,centerY=height/2,margin=Math.sqrt(width*width+height*height);" +
            "for(var baseY=-margin;baseY<=height+margin;baseY+=rowSpacing){" +
            "var points=[];var samples=Math.max(80,Math.ceil((width+2*margin)/wavelength*24));" +
            "for(var i=0;i<=samples;i++){" +
            "var x=-margin+(width+2*margin)*i/samples;" +
            "var y=baseY+amplitude*Math.sin(2*Math.PI*x/wavelength);" +
            "var dx=x-centerX,dy=y-centerY;" +
            "points.push([centerX+dx*Math.cos(angle)-dy*Math.sin(angle)," +
            "centerY+dx*Math.sin(angle)+dy*Math.cos(angle)]);}" +
            "var wave=doc.pathItems.add();wave.stroked=true;wave.filled=false;" +
            "wave.strokeWidth=stroke;wave.strokeColor=black;wave.setEntirePath(points);}" +
            $"doc.saveAs(new File(\"{EscapePath(outputPath)}\"));" +
            $"return \"{EscapeJavaScript(outputPath)}\";" +
            "})();";

    private static string BuildWaveTraceScript(
        double widthMillimeters,
        double heightMillimeters,
        string sourcePath,
        string illustratorPath,
        string svgPath,
        int threshold,
        double resolution)
        => "(function(){" +
            $"var width={Points(widthMillimeters)},height={Points(heightMillimeters)};" +
            "var doc=app.documents.add(DocumentColorSpace.RGB,width,height);" +
            "var placed=doc.placedItems.add();" +
            $"placed.file=new File(\"{EscapePath(sourcePath)}\");" +
            "placed.width=width;placed.height=height;placed.left=0;placed.top=height;" +
            "var plugin=placed.trace();var tracing=plugin.tracing;var options=tracing.tracingOptions;" +
            "options.tracingMode=TracingModeType.TRACINGMODEBLACKANDWHITE;" +
            "options.fills=true;options.strokes=false;options.ignoreWhite=true;" +
            $"options.threshold={threshold};options.resample=true;options.resampleResolution={Format(resolution)};" +
            "options.preprocessBlur=2;options.minArea=100;options.pathFitting=3;options.cornerAngle=120;" +
            "app.redraw();var pathCount=tracing.pathCount;tracing.expandTracing(false);app.redraw();" +
            "var aiOptions=new IllustratorSaveOptions();" +
            $"doc.saveAs(new File(\"{EscapePath(illustratorPath)}\"),aiOptions);" +
            "var svgOptions=new ExportOptionsSVG();svgOptions.embedRasterImages=false;" +
            $"doc.exportFile(new File(\"{EscapePath(svgPath)}\"),ExportType.SVG,svgOptions);" +
            "return '路径数量：'+pathCount;" +
            "})();";

    private static void ValidateLineParameters(
        double lineWidthMillimeters,
        double spacingMillimeters,
        double angleDegrees)
    {
        if (lineWidthMillimeters <= 0)
            throw new ArgumentException("线宽必须大于 0 毫米。");
        if (spacingMillimeters <= lineWidthMillimeters)
            throw new ArgumentException("间距必须大于线宽。");
        if (angleDegrees is < -180 or > 180)
            throw new ArgumentException("角度必须在 -180° 到 180° 之间。");
    }

    private static string Points(double millimeters)
        => Format(millimeters / 25.4 * 72.0);

    private static string Format(double value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);

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
