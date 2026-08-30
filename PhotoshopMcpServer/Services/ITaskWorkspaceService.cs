using PhotoshopMcpServer.Models;

namespace PhotoshopMcpServer.Services;

public interface ITaskWorkspaceService
{
    DuanxingTaskRecord PrepareTask(DuanxingTaskRequest request);
    DuanxingTaskRecord LoadTask(string taskDirectory);
    DuanxingTaskRecord FindMostRecentTask();
    DuanxingTaskRecord FindLatestTaskForSource(string sourceFile);
    bool IsApproved(string taskDirectory);
    bool IsResultApproved(string taskDirectory, string resultFile);
    string GetApprovedResultFile(string taskDirectory);
    DuanxingReviewSummary BuildReviewSummary(string taskDirectory);
    DuanxingDeliveryReport GenerateDeliveryReport(string taskDirectory, string stage);
    DuanxingRollbackRecord RestorePreviousResult(string taskDirectory);
    DuanxingAiResultRecord RegisterAiResult(
        string taskDirectory,
        string generatedFile,
        string operation,
        string prompt);
    DuanxingReviewRecord SaveReview(
        string taskDirectory,
        string reviewer,
        bool approved,
        string comment);
}
