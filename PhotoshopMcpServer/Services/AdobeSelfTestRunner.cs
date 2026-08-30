using PhotoshopMcpServer.Models;

namespace PhotoshopMcpServer.Services;

public sealed class AdobeSelfTestRunner(
    IPhotoshopService photoshopService,
    IIllustratorService illustratorService,
    int retryAttempts = 20,
    int retryDelayMilliseconds = 1000)
{
    public AdobeSelfTestResult Run(string outputRoot)
    {
        if (string.IsNullOrWhiteSpace(outputRoot))
            throw new ArgumentException("现场自检输出目录不能为空。", nameof(outputRoot));

        var outputDirectory = Path.Combine(
            Path.GetFullPath(outputRoot),
            $"Adobe自检_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(outputDirectory);
        var photoshopFile = Path.Combine(outputDirectory, "Photoshop_自检.png");
        var illustratorFile = Path.Combine(outputDirectory, "Illustrator_自检.ai");
        var messages = new List<string>();

        photoshopService.LaunchPhotoshop();
        var photoshopVersion = Retry(
            photoshopService.GetPhotoshopVersion,
            version => !string.IsNullOrWhiteSpace(version),
            "Photoshop 启动后仍无法读取版本，请确认软件已完成启动且没有弹窗。");
        var photoshopResult = Retry(
            () => photoshopService.ExecuteJavaScriptWithResult(BuildPhotoshopScript(photoshopFile)),
            result => result.Success,
            "Photoshop 启动后仍无法执行测试作图，请检查是否有许可或恢复文档弹窗。");
        if (!photoshopResult.Success || !File.Exists(photoshopFile))
            throw new InvalidOperationException(
                $"Photoshop 自检失败：{photoshopResult.ErrorMessage}".TrimEnd('：'));
        messages.Add("Photoshop 已创建并保存非敏感测试图。客户原图未被读取。");

        illustratorService.LaunchIllustrator();
        var illustratorVersion = Retry(
            illustratorService.GetIllustratorVersion,
            version => !string.IsNullOrWhiteSpace(version),
            "Illustrator 启动后仍无法读取版本，请确认软件已完成启动且没有弹窗。");
        var illustratorResult = Retry(
            () => illustratorService.ExecuteJavaScriptWithResult(BuildIllustratorScript(illustratorFile)),
            result => result.Success,
            "Illustrator 启动后仍无法执行测试作图，请检查是否有许可或恢复文档弹窗。");
        if (!illustratorResult.Success || !File.Exists(illustratorFile))
            throw new InvalidOperationException(
                $"Illustrator 自检失败：{illustratorResult.ErrorMessage}".TrimEnd('：'));
        messages.Add("Illustrator 已创建并保存可编辑测试线稿。客户文件未被读取。");
        messages.Add("Adobe 现场自检通过。可以进入客户样板测试。");

        return new AdobeSelfTestResult(
            true,
            DateTimeOffset.Now.ToString("O"),
            outputDirectory,
            photoshopVersion,
            photoshopFile,
            illustratorVersion,
            illustratorFile,
            messages);
    }

    private T Retry<T>(Func<T> action, Func<T, bool> completed, string timeoutMessage)
    {
        Exception lastException = null;
        for (var attempt = 1; attempt <= retryAttempts; attempt++)
        {
            try
            {
                var result = action();
                if (completed(result))
                    return result;
            }
            catch (Exception exception)
            {
                lastException = exception;
            }
            if (retryDelayMilliseconds > 0)
                Thread.Sleep(retryDelayMilliseconds);
        }
        throw new InvalidOperationException(timeoutMessage, lastException);
    }

    internal static string BuildPhotoshopScript(string outputPath)
        => "(function(){" +
            "app.displayDialogs=DialogModes.NO;" +
            "var doc=app.documents.add(256,256,72,'Duanxing_Self_Test',NewDocumentMode.RGB,DocumentFill.WHITE);" +
            "var color=new SolidColor();color.rgb.red=32;color.rgb.green=128;color.rgb.blue=196;" +
            "doc.selection.selectAll();doc.selection.fill(color);doc.selection.deselect();" +
            $"var file=new File(\"{EscapePath(outputPath)}\");" +
            "var options=new PNGSaveOptions();doc.saveAs(file,options,true,Extension.LOWERCASE);" +
            "doc.close(SaveOptions.DONOTSAVECHANGES);return file.fsName;" +
            "})();";

    internal static string BuildIllustratorScript(string outputPath)
        => "(function(){" +
            "app.userInteractionLevel=UserInteractionLevel.DONTDISPLAYALERTS;" +
            "var doc=app.documents.add(DocumentColorSpace.RGB,256,256);" +
            "var line=doc.pathItems.add();line.setEntirePath([[24,24],[232,232]]);" +
            "line.stroked=true;line.filled=false;line.strokeWidth=2;" +
            "var color=new RGBColor();color.red=32;color.green=128;color.blue=196;line.strokeColor=color;" +
            $"var file=new File(\"{EscapePath(outputPath)}\");" +
            "var options=new IllustratorSaveOptions();doc.saveAs(file,options);" +
            "doc.close(SaveOptions.DONOTSAVECHANGES);return file.fsName;" +
            "})();";

    private static string EscapePath(string path)
        => Path.GetFullPath(path)
            .Replace('\\', '/')
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
}
