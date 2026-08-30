using PhotoshopMcpServer.Models;

namespace PhotoshopMcpServer.Services;

public interface ITaskWorkspaceService
{
    DuanxingTaskRecord PrepareTask(DuanxingTaskRequest request);
    DuanxingReviewRecord SaveReview(
        string taskDirectory,
        string reviewer,
        bool approved,
        string comment);
}
