using System.ComponentModel;
using ModelContextProtocol.Server;
using PhotoshopMcpServer.Services;

namespace PhotoshopMcpServer.Tools;

[McpServerToolType]
public class IllustratorTools(IIllustratorService illustratorService)
{
    [McpServerTool]
    [Description("Checks whether Adobe Illustrator is running and accessible through Windows COM.")]
    public string IsIllustratorRunning()
        => illustratorService.IsIllustratorRunning().ToString().ToLowerInvariant();

    [McpServerTool]
    [Description("Launches Adobe Illustrator or connects to its running instance.")]
    public string LaunchIllustrator()
    {
        try
        {
            illustratorService.LaunchIllustrator();
            return "Illustrator launched and connected successfully.";
        }
        catch (Exception exception)
        {
            return $"Failed to launch Illustrator: {exception.Message}";
        }
    }

    [McpServerTool]
    [Description("Gets the version of the connected Adobe Illustrator instance.")]
    public string GetIllustratorVersion()
    {
        try
        {
            return illustratorService.GetIllustratorVersion();
        }
        catch (Exception exception)
        {
            return $"Error: {exception.Message}";
        }
    }

    [McpServerTool]
    [Description("Gets the name of the active Illustrator document.")]
    public string GetActiveIllustratorDocument()
    {
        try
        {
            return illustratorService.GetActiveDocumentName();
        }
        catch (Exception exception)
        {
            return $"Error: {exception.Message}";
        }
    }
}
