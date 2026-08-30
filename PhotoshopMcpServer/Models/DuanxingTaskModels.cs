namespace PhotoshopMcpServer.Models;

public record DuanxingTaskRequest(
    string SourceFile,
    string OutputRoot,
    string TaskName,
    double WidthMillimeters,
    double HeightMillimeters,
    int Dpi,
    string TilingMode,
    string OutputFormat,
    string Reviewer
);

public record DuanxingTaskRecord(
    string TaskId,
    string Status,
    string CreatedAt,
    string SourceFile,
    string SourceSha256,
    string WorkingCopy,
    string OutputDirectory,
    string TaskName,
    double WidthMillimeters,
    double HeightMillimeters,
    int Dpi,
    int TargetWidthPixels,
    int TargetHeightPixels,
    long TargetPixelCount,
    string TilingMode,
    string OutputFormat,
    string Reviewer,
    IReadOnlyList<string> Warnings
);

public record DuanxingReviewRecord(
    string TaskId,
    string ReviewedAt,
    string Reviewer,
    bool Approved,
    string Comment,
    string Status,
    string ResultFile,
    string ResultSha256
);

public record DuanxingAiResultRecord(
    string TaskId,
    string RegisteredAt,
    string Operation,
    string Prompt,
    string SourceFile,
    string ResultFile,
    string ResultSha256,
    string Status
);

public record DuanxingReviewSummary(
    string TaskId,
    string TaskDirectory,
    string LatestResultFile,
    int ResultFileCount,
    int AiResultCount,
    string LatestAiOperation,
    bool OriginalUnchanged,
    bool WorkingCopyExists,
    bool Approved,
    string ReviewStatus,
    IReadOnlyList<string> ManualChecklist
);

public record DuanxingDeliveryReport(
    string TaskId,
    string Stage,
    string GeneratedAt,
    string ReportFile,
    bool ReadyForSignOff,
    string Status
);

public record DuanxingRollbackRecord(
    string TaskId,
    string RestoredAt,
    string AbandonedLatestFile,
    string PreviousFile,
    string RestoredFile,
    string Status
);

public record DuanxingTaskProgress(
    string TaskId,
    string Status,
    string NextStep,
    int ResultFileCount,
    int ProductionFileCount,
    bool OriginalUnchanged
);
