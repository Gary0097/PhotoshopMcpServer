using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PhotoshopMcpServer.Services;

namespace PhotoshopMcpServer.Tools;

[McpServerToolType]
public class EnvironmentTools(
    IPhotoshopService photoshopService,
    IIllustratorService illustratorService)
{
    private const string DefaultPhotoshopPath = @"K:\TOOL\Adobe Photoshop 2026";
    private const string DefaultIllustratorPath = @"K:\TOOL\Adobe Illustrator 2026";

    [McpServerTool(Name = "duanxing_check_environment")]
    [Description(
        "用中文检查端行作图环境：Codex、Photoshop 2026、Illustrator 2026、运行状态和脚本安全模式。" +
        "GPT 购买/登录、VPN 和 Adobe 授权需要客户人工确认。")]
    public string CheckDuanxingEnvironment()
    {
        var photoshopPath = Environment.GetEnvironmentVariable("DUANXING_PHOTOSHOP_PATH")
            ?? DefaultPhotoshopPath;
        var illustratorPath = Environment.GetEnvironmentVariable("DUANXING_ILLUSTRATOR_PATH")
            ?? DefaultIllustratorPath;
        var machineReady = CommandExists("codex") &&
            Directory.Exists(photoshopPath) &&
            Directory.Exists(illustratorPath);
        var result = new Dictionary<string, object>
        {
            ["自动检查结果"] = machineReady ? "通过" : "未通过",
            ["Codex已安装"] = CommandExists("codex") ? "是" : "否",
            ["Photoshop 2026"] = new Dictionary<string, object>
            {
                ["已找到安装目录"] = Directory.Exists(photoshopPath) ? "是" : "否",
                ["安装目录"] = photoshopPath,
                ["当前正在运行"] = photoshopService.IsPhotoshopRunning() ? "是" : "否"
            },
            ["Illustrator 2026"] = new Dictionary<string, object>
            {
                ["已找到安装目录"] = Directory.Exists(illustratorPath) ? "是" : "否",
                ["安装目录"] = illustratorPath,
                ["当前正在运行"] = illustratorService.IsIllustratorRunning() ? "是" : "否"
            },
            ["需要人工确认"] = new[]
            {
                "GPT 已购买，账号可以登录 Codex",
                "VPN 专线已经连接",
                "Photoshop 2026 和 Illustrator 2026 已激活授权"
            },
            ["任意Photoshop脚本模式"] = string.Equals(
                Environment.GetEnvironmentVariable("DUANXING_ALLOW_ARBITRARY_SCRIPTS"),
                "true",
                StringComparison.OrdinalIgnoreCase)
                ? "已开启（仅限授权开发调试）"
                : "已关闭（生产安全模式）"
        };
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool CommandExists(string command)
    {
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", string.Empty }
            : new[] { string.Empty };
        return pathEntries.Any(path => extensions.Any(extension =>
            File.Exists(Path.Combine(path.Trim('"'), command + extension))));
    }
}
