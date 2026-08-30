using PhotoshopMcpServer.Models;

namespace PhotoshopMcpServer.Services;

// Interface for the minimum Illustrator connection required by the first deployment milestone.
public interface IIllustratorService
{
    bool IsIllustratorRunning();
    void LaunchIllustrator();
    string GetIllustratorVersion();
    string GetActiveDocumentName();
    IllustratorScriptResult ExecuteJavaScriptWithResult(string script);
    void Disconnect();
}
