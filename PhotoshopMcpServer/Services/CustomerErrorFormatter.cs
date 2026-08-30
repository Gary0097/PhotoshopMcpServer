using System.Text;
using System.Text.RegularExpressions;

namespace PhotoshopMcpServer.Services;

public static partial class CustomerErrorFormatter
{
    public static string Format(Exception exception)
    {
        WriteTechnicalLog(exception.ToString());
        return FormatMessage(exception.Message);
    }

    public static string Format(string message)
    {
        WriteTechnicalLog(message);
        return FormatMessage(message);
    }

    private static string FormatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return GenericMessage;
        var firstLine = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim() ?? string.Empty;
        if (!ChineseText().IsMatch(firstLine))
            return GenericMessage;
        firstLine = WindowsPath().Replace(firstLine, "本地文件");
        firstLine = TechnicalSuffix().Replace(firstLine, string.Empty).Trim();
        return firstLine.Length <= 180 ? firstLine : firstLine[..180] + "……";
    }

    private static void WriteTechnicalLog(string detail)
    {
        try
        {
            var logPath = GetTechnicalLogPath();
            var logDirectory = Path.GetDirectoryName(logPath)!;
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(
                logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {detail}{Environment.NewLine}",
                new UTF8Encoding(false));
        }
        catch
        {
            // 错误日志不能覆盖原本的客户提示。
        }
    }

    internal static string GetTechnicalLogPath()
    {
        var overriddenPath = Environment.GetEnvironmentVariable("DUANXING_TECHNICAL_LOG_PATH");
        if (!string.IsNullOrWhiteSpace(overriddenPath))
            return Path.GetFullPath(overriddenPath);
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "端行作图助手",
            "技术错误.log");
    }

    private const string GenericMessage =
        "本次操作没有完成。请重试一次；仍失败时把当前画面发给实施人员。";

    [GeneratedRegex("[\\u4e00-\\u9fff]")]
    private static partial Regex ChineseText();

    [GeneratedRegex(@"(?i)(?:[a-z]:\\|\\\\)[^，。；：]+")]
    private static partial Regex WindowsPath();

    [GeneratedRegex(@"\s*(?:--->|at\s+[A-Za-z_]|HRESULT|0x[0-9A-Fa-f]+).*$")]
    private static partial Regex TechnicalSuffix();
}
