using System.ComponentModel;
using ModelContextProtocol.Server;
using PhotoshopMcpServer.Services;

namespace PhotoshopMcpServer.Tools;

[McpServerToolType]
public class IllustratorTools(IIllustratorService illustratorService)
{
    [McpServerTool(Name = "illustrator_is_running")]
    [Description("检查 Illustrator 是否已经启动并可连接。")]
    public string IsIllustratorRunning()
        => illustratorService.IsIllustratorRunning() ? "是" : "否";

    [McpServerTool(Name = "illustrator_launch")]
    [Description("启动 Illustrator，或连接到已经打开的 Illustrator。")]
    public string LaunchIllustrator()
    {
        try
        {
            illustratorService.LaunchIllustrator();
            return "Illustrator 已启动并连接成功。";
        }
        catch (Exception exception)
        {
            return $"Illustrator 启动失败：{exception.Message}";
        }
    }

    [McpServerTool(Name = "illustrator_get_version")]
    [Description("读取当前连接的 Illustrator 版本。")]
    public string GetIllustratorVersion()
    {
        try
        {
            return illustratorService.GetIllustratorVersion();
        }
        catch (Exception exception)
        {
            return $"读取 Illustrator 版本失败：{exception.Message}";
        }
    }

    [McpServerTool(Name = "illustrator_get_active_document")]
    [Description("读取 Illustrator 当前文档名称。")]
    public string GetActiveIllustratorDocument()
    {
        try
        {
            return illustratorService.GetActiveDocumentName();
        }
        catch (Exception exception)
        {
            return $"读取 Illustrator 当前文档失败：{exception.Message}";
        }
    }
}
