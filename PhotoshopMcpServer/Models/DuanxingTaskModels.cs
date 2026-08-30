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
    string Status
);
