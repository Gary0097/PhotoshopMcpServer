namespace PhotoshopMcpServer.Models;

public record WorkflowSelfTestResult(
    bool Success,
    string CheckedAt,
    string TaskDirectory,
    string SourceFile,
    bool SourceUnchanged,
    string WorkingCopy,
    string ReviewFile,
    string ProductionFile,
    string DeliveryReport,
    string ReviewStatus,
    IReadOnlyList<string> Messages
);
