using System.Text;
using System.Text.RegularExpressions;

namespace PhotoshopMcpServer.Services;

public static partial class SupportReportService
{
    public static string Create(
        string environmentSummary,
        string outputDirectory = null,
        string technicalLogPath = null)
    {
        outputDirectory ??= Environment.GetEnvironmentVariable("DUANXING_SUPPORT_REPORT_DIRECTORY");
        if (string.IsNullOrWhiteSpace(outputDirectory))
            outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Directory.CreateDirectory(outputDirectory);

        technicalLogPath ??= CustomerErrorFormatter.GetTechnicalLogPath();
        var recentErrors = ReadRecentErrors(technicalLogPath);
        var report = $"""
            端行作图助手故障报告
            生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}

            一、自动环境检查
            {Sanitize(environmentSummary)}

            二、最近技术记录（已自动脱敏）
            {recentErrors}

            三、客户只需做一件事
            请把这份“端行作图故障报告.txt”发给实施人员，不需要复制英文报错或查找其他日志。
            """;
        var reportPath = Path.Combine(outputDirectory, "端行作图故障报告.txt");
        File.WriteAllText(reportPath, report, new UTF8Encoding(false));
        return reportPath;
    }

    public static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "暂无记录。";
        var result = WindowsPath().Replace(value, "本地文件");
        result = ApiToken().Replace(result, "[已隐藏令牌]");
        result = BearerToken().Replace(result, "Bearer [已隐藏令牌]");
        result = NamedSecret().Replace(result, "$1=[已隐藏]");
        result = StackTrace().Replace(result, string.Empty);
        return result.Length <= 8000 ? result.Trim() : result[^8000..].Trim();
    }

    private static string ReadRecentErrors(string logPath)
    {
        try
        {
            if (!File.Exists(logPath))
                return "暂无技术错误记录。";
            var lines = File.ReadLines(logPath).TakeLast(40);
            return Sanitize(string.Join(Environment.NewLine, lines));
        }
        catch
        {
            return "技术记录暂时无法读取。";
        }
    }

    [GeneratedRegex(@"(?i)(?:[a-z]:\\|\\\\)[^\r\n\""']+")]
    private static partial Regex WindowsPath();

    [GeneratedRegex(@"(?i)\bsk-[a-z0-9_-]{8,}\b")]
    private static partial Regex ApiToken();

    [GeneratedRegex(@"(?i)\bBearer\s+[a-z0-9._~+/=-]{8,}")]
    private static partial Regex BearerToken();

    [GeneratedRegex(@"(?i)\b(api[_-]?key|token|password|secret)\s*[:=]\s*[^\s,;]+")]
    private static partial Regex NamedSecret();

    [GeneratedRegex(@"(?m)^\s*at\s+[^\r\n]+$")]
    private static partial Regex StackTrace();
}
