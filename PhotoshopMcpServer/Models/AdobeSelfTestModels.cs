namespace PhotoshopMcpServer.Models;

public record AdobeSelfTestResult(
    bool Success,
    string CheckedAt,
    string OutputDirectory,
    string PhotoshopVersion,
    string PhotoshopTestFile,
    string IllustratorVersion,
    string IllustratorTestFile,
    IReadOnlyList<string> Messages
);
