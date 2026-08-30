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

    [McpServerTool]
    [Description(
        "Runs the Duanxing deployment preflight for Codex, Photoshop 2026 and Illustrator 2026. " +
        "GPT subscription/login and VPN availability require human confirmation.")]
    public string CheckDuanxingEnvironment()
    {
        var photoshopPath = Environment.GetEnvironmentVariable("DUANXING_PHOTOSHOP_PATH")
            ?? DefaultPhotoshopPath;
        var illustratorPath = Environment.GetEnvironmentVariable("DUANXING_ILLUSTRATOR_PATH")
            ?? DefaultIllustratorPath;
        var result = new
        {
            ready = CommandExists("codex") &&
                Directory.Exists(photoshopPath) &&
                Directory.Exists(illustratorPath),
            codexInstalled = CommandExists("codex"),
            photoshop2026 = new
            {
                installed = Directory.Exists(photoshopPath),
                path = photoshopPath,
                running = photoshopService.IsPhotoshopRunning()
            },
            illustrator2026 = new
            {
                installed = Directory.Exists(illustratorPath),
                path = illustratorPath,
                running = illustratorService.IsIllustratorRunning()
            },
            manualChecks = new[]
            {
                "GPT plan/account purchased and Codex login verified",
                "VPN dedicated network is connected",
                "Photoshop 2026 and Illustrator 2026 licenses are activated"
            },
            arbitraryPhotoshopScriptsEnabled = string.Equals(
                Environment.GetEnvironmentVariable("DUANXING_ALLOW_ARBITRARY_SCRIPTS"),
                "true",
                StringComparison.OrdinalIgnoreCase)
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
