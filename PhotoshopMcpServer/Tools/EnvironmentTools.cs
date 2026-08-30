using System.ComponentModel;
using System.Text.Json;
using Microsoft.Win32;
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

    [McpServerTool(Name = "duanxing_check_environment", Title = "检查作图环境", ReadOnly = true)]
    [Description(
        "自动检查端行作图环境。检查通过时直接继续作图，不重复询问账号、网络、授权或安装路径；" +
        "未通过时只返回缺少的一项和一个中文解决办法。")]
    public string CheckDuanxingEnvironment()
    {
        var photoshopPath = Environment.GetEnvironmentVariable("DUANXING_PHOTOSHOP_PATH")
            ?? DefaultPhotoshopPath;
        var illustratorPath = Environment.GetEnvironmentVariable("DUANXING_ILLUSTRATOR_PATH")
            ?? DefaultIllustratorPath;
        var photoshopTypeLibraryReady = IsPhotoshopTypeLibraryReady();
        var machineReady = CommandExists("codex") &&
            Directory.Exists(photoshopPath) &&
            Directory.Exists(illustratorPath) &&
            photoshopTypeLibraryReady;
        var nextStep = GetEnvironmentNextStep(
            CommandExists("codex"),
            Directory.Exists(photoshopPath),
            Directory.Exists(illustratorPath),
            photoshopTypeLibraryReady);
        var result = new Dictionary<string, object>
        {
            ["自动检查结果"] = machineReady ? "通过" : "未通过",
            ["Codex已安装"] = CommandExists("codex") ? "是" : "否",
            ["Photoshop 2026"] = new Dictionary<string, object>
            {
                ["已找到安装目录"] = Directory.Exists(photoshopPath) ? "是" : "否",
                ["当前正在运行"] = photoshopService.IsPhotoshopRunning() ? "是" : "否",
                ["64位自动控制文件"] = photoshopTypeLibraryReady
                    ? "正常"
                    : "需要修复"
            },
            ["Illustrator 2026"] = new Dictionary<string, object>
            {
                ["已找到安装目录"] = Directory.Exists(illustratorPath) ? "是" : "否",
                ["当前正在运行"] = illustratorService.IsIllustratorRunning() ? "是" : "否"
            },
            ["下一步"] = nextStep,
            ["任意Photoshop脚本模式"] = string.Equals(
                Environment.GetEnvironmentVariable("DUANXING_ALLOW_ARBITRARY_SCRIPTS"),
                "true",
                StringComparison.OrdinalIgnoreCase)
                ? "已开启（仅限授权开发调试）"
                : "已关闭（生产安全模式）"
        };
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GetEnvironmentNextStep(
        bool codexReady,
        bool photoshopReady,
        bool illustratorReady,
        bool automationReady)
    {
        if (!codexReady)
            return "请先安装并登录 Codex，然后重新打开。";
        if (!photoshopReady)
            return "没有找到 Photoshop 2026，请让实施人员完成安装。";
        if (!illustratorReady)
            return "没有找到 Illustrator 2026，请让实施人员完成安装。";
        if (!automationReady)
            return "请关闭作图软件和 Codex，再双击“【客户双击这里】首次安装端行作图助手.cmd”。";
        return "环境正常，直接继续作图。";
    }

    [McpServerTool(Name = "duanxing_generate_support_report", Title = "生成中文故障报告")]
    [Description("客户说“还是不行”或“帮我排查”时使用。自动把环境状态和最近错误整理成桌面上的中文脱敏报告。")]
    public string GenerateSupportReport()
    {
        try
        {
            var reportPath = SupportReportService.Create(CheckDuanxingEnvironment());
            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["成功"] = true,
                ["已完成"] = "故障报告已经生成到桌面。",
                ["报告文件"] = reportPath,
                ["下一步"] = "把桌面上的“端行作图故障报告.txt”发给实施人员。"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["成功"] = false,
                ["提示"] = CustomerErrorFormatter.Format(exception),
                ["下一步"] = "请把当前画面发给实施人员。"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private static bool IsPhotoshopTypeLibraryReady()
    {
        try
        {
            var classId = Registry.GetValue(
                @"HKEY_CLASSES_ROOT\Photoshop.Application\CLSID",
                string.Empty,
                null)?.ToString();
            if (string.IsNullOrWhiteSpace(classId))
                return false;
            var typeLibraryId = Registry.GetValue(
                $@"HKEY_CLASSES_ROOT\CLSID\{classId}\TypeLib",
                string.Empty,
                null)?.ToString();
            if (string.IsNullOrWhiteSpace(typeLibraryId))
                return false;
            var typeLibraryRoot = $@"HKEY_CLASSES_ROOT\TypeLib\{typeLibraryId}\1.0\0";
            var win64Path = Registry.GetValue(
                $@"{typeLibraryRoot}\win64",
                string.Empty,
                null)?.ToString();
            if (!string.IsNullOrWhiteSpace(win64Path))
                return File.Exists(win64Path);
            var win32Path = Registry.GetValue(
                $@"{typeLibraryRoot}\Win32",
                string.Empty,
                null)?.ToString();
            return !string.IsNullOrWhiteSpace(win32Path) && File.Exists(win32Path);
        }
        catch (Exception)
        {
            return false;
        }
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
