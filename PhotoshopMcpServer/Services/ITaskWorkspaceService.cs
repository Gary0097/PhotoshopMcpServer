using PhotoshopMcpServer.Models;

namespace PhotoshopMcpServer.Services;

public interface ITaskWorkspaceService
{
    DuanxingTaskRecord PrepareTask(DuanxingTaskRequest request);
    DuanxingTaskRecord LoadTask(string taskDirectory);
    bool IsApproved(string taskDirectory);
    DuanxingReviewRecord SaveReview(
        string taskDirectory,
        string reviewer,
        bool approved,
        string comment);
}
