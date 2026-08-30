using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PhotoshopMcpServer.Models;

namespace PhotoshopMcpServer.Services;

public sealed partial class TaskWorkspaceService : ITaskWorkspaceService
{
    private static readonly HashSet<string> SupportedInputExtensions = new(
        [".psd", ".psb", ".tif", ".tiff", ".png", ".jpg", ".jpeg", ".bmp"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SupportedOutputFormats = new(
        ["PSD", "PSB", "TIFF", "PNG", "JPEG", "AI", "SVG", "PDF"],
        StringComparer.OrdinalIgnoreCase);

    public DuanxingTaskRecord PrepareTask(DuanxingTaskRequest request)
    {
        ValidateRequest(request);

        var sourcePath = Path.GetFullPath(request.SourceFile);
        var outputRoot = Path.GetFullPath(request.OutputRoot);
        Directory.CreateDirectory(outputRoot);

        var taskId = $"DX-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..31];
        var taskFolderName = $"{taskId}_{SanitizeName(request.TaskName)}";
        var taskDirectory = Path.Combine(outputRoot, taskFolderName);
        var workingDirectory = Path.Combine(taskDirectory, "01_工作副本");
        var resultDirectory = Path.Combine(taskDirectory, "02_处理结果");
        var reviewDirectory = Path.Combine(taskDirectory, "03_复核记录");
        Directory.CreateDirectory(workingDirectory);
        Directory.CreateDirectory(resultDirectory);
        Directory.CreateDirectory(reviewDirectory);

        var workingCopy = Path.Combine(workingDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, workingCopy, overwrite: false);

        var targetWidthPixels = MillimetersToPixels(request.WidthMillimeters, request.Dpi);
        var targetHeightPixels = MillimetersToPixels(request.HeightMillimeters, request.Dpi);
        var targetPixelCount = checked((long)targetWidthPixels * targetHeightPixels);
        var warnings = BuildWarnings(targetPixelCount);
        var record = new DuanxingTaskRecord(
            taskId,
            "待处理",
            DateTimeOffset.Now.ToString("O"),
            sourcePath,
            CalculateSha256(sourcePath),
            workingCopy,
            resultDirectory,
            request.TaskName.Trim(),
            request.WidthMillimeters,
            request.HeightMillimeters,
            request.Dpi,
            targetWidthPixels,
            targetHeightPixels,
            targetPixelCount,
            NormalizeTilingMode(request.TilingMode),
            request.OutputFormat.ToUpperInvariant(),
            request.Reviewer.Trim(),
            warnings);
        WriteJson(Path.Combine(taskDirectory, "task.json"), record);
        return record;
    }

    public DuanxingReviewRecord SaveReview(
        string taskDirectory,
        string reviewer,
        bool approved,
        string comment)
    {
        if (string.IsNullOrWhiteSpace(taskDirectory))
            throw new ArgumentException("任务目录不能为空。", nameof(taskDirectory));
        if (string.IsNullOrWhiteSpace(reviewer))
            throw new ArgumentException("复核人不能为空。", nameof(reviewer));

        var fullTaskDirectory = Path.GetFullPath(taskDirectory);
        var task = LoadTask(fullTaskDirectory);
        var review = new DuanxingReviewRecord(
            task.TaskId,
            DateTimeOffset.Now.ToString("O"),
            reviewer.Trim(),
            approved,
            comment?.Trim() ?? string.Empty,
            approved ? "已批准，可导出生产版" : "已退回，需要修改");
        var reviewDirectory = Path.Combine(fullTaskDirectory, "03_复核记录");
        Directory.CreateDirectory(reviewDirectory);
        WriteJson(Path.Combine(reviewDirectory, $"review-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json"), review);
        return review;
    }

    public DuanxingAiResultRecord RegisterAiResult(
        string taskDirectory,
        string generatedFile,
        string operation,
        string prompt)
    {
        if (string.IsNullOrWhiteSpace(generatedFile) || !File.Exists(generatedFile))
            throw new FileNotFoundException("找不到 AI 生成结果，请先确认图片已经生成。", generatedFile);
        if (!SupportedInputExtensions.Contains(Path.GetExtension(generatedFile)))
            throw new ArgumentException("AI 结果格式不支持。请使用 PSD、PSB、TIFF、PNG、JPEG 或 BMP。");
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("请说明 AI 做了什么，例如：补图扩展、清晰修复或纹理生成。", nameof(operation));

        var fullTaskDirectory = Path.GetFullPath(taskDirectory);
        var task = LoadTask(fullTaskDirectory);
        var sourcePath = Path.GetFullPath(generatedFile);
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var safeOperation = SanitizeName(operation);
        var resultName = $"AI_{safeOperation}_{DateTime.Now:yyyyMMdd-HHmmss-fff}{extension}";
        var resultPath = Path.Combine(task.OutputDirectory, resultName);
        Directory.CreateDirectory(task.OutputDirectory);
        File.Copy(sourcePath, resultPath, overwrite: false);

        var record = new DuanxingAiResultRecord(
            task.TaskId,
            DateTimeOffset.Now.ToString("O"),
            operation.Trim(),
            prompt?.Trim() ?? string.Empty,
            sourcePath,
            resultPath,
            CalculateSha256(resultPath),
            "待人工复核");
        var aiDirectory = Path.Combine(fullTaskDirectory, "05_AI记录");
        Directory.CreateDirectory(aiDirectory);
        WriteJson(Path.Combine(aiDirectory, $"ai-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json"), record);

        SaveReview(fullTaskDirectory, task.Reviewer, false, "AI 结果已更新，旧复核自动失效，请重新检查后批准。");
        return record;
    }

    public DuanxingTaskRecord LoadTask(string taskDirectory)
    {
        if (string.IsNullOrWhiteSpace(taskDirectory))
            throw new ArgumentException("任务目录不能为空。", nameof(taskDirectory));
        var fullTaskDirectory = Path.GetFullPath(taskDirectory);
        var taskFile = Path.Combine(fullTaskDirectory, "task.json");
        if (!File.Exists(taskFile))
            throw new InvalidOperationException("没有找到 task.json，这不是有效的端行任务目录。");
        return JsonSerializer.Deserialize<DuanxingTaskRecord>(File.ReadAllText(taskFile))
            ?? throw new InvalidOperationException("任务记录无法读取。");
    }

    public bool IsApproved(string taskDirectory)
    {
        var task = LoadTask(taskDirectory);
        var reviewDirectory = Path.Combine(Path.GetFullPath(taskDirectory), "03_复核记录");
        if (!Directory.Exists(reviewDirectory))
            return false;
        var latestReview = Directory.GetFiles(reviewDirectory, "review-*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (latestReview == null)
            return false;
        var review = JsonSerializer.Deserialize<DuanxingReviewRecord>(File.ReadAllText(latestReview));
        return review != null && review.TaskId == task.TaskId && review.Approved;
    }

    private static void ValidateRequest(DuanxingTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceFile) || !File.Exists(request.SourceFile))
            throw new FileNotFoundException("找不到原图，请检查文件路径。", request.SourceFile);
        if (!SupportedInputExtensions.Contains(Path.GetExtension(request.SourceFile)))
            throw new ArgumentException("原图格式不支持。请使用 PSD、PSB、TIFF、PNG、JPEG 或 BMP。");
        if (string.IsNullOrWhiteSpace(request.OutputRoot))
            throw new ArgumentException("请选择输出目录。", nameof(request.OutputRoot));
        if (string.IsNullOrWhiteSpace(request.TaskName))
            throw new ArgumentException("请填写任务名称。", nameof(request.TaskName));
        if (request.WidthMillimeters <= 0 || request.HeightMillimeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "成品宽度和高度必须大于 0 mm。");
        if (request.Dpi is < 72 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(request.Dpi), "DPI 必须在 72 到 10000 之间。");
        if (!SupportedOutputFormats.Contains(request.OutputFormat ?? string.Empty))
            throw new ArgumentException("输出格式不支持。", nameof(request.OutputFormat));
        if (string.IsNullOrWhiteSpace(request.Reviewer))
            throw new ArgumentException("请填写复核人。", nameof(request.Reviewer));
    }

    private static int MillimetersToPixels(double millimeters, int dpi)
        => checked((int)Math.Round(millimeters / 25.4 * dpi, MidpointRounding.AwayFromZero));

    private static IReadOnlyList<string> BuildWarnings(long pixelCount)
    {
        var warnings = new List<string>();
        if (pixelCount > 500_000_000)
            warnings.Add("目标图像超过 5 亿像素，处理前请确认内存、暂存盘和 PSB/TIFF 格式。");
        if (pixelCount > 2_000_000_000)
            warnings.Add("目标图像接近 Photoshop 大图限制，必须先做小样测试。");
        return warnings;
    }

    private static string NormalizeTilingMode(string tilingMode)
        => tilingMode?.Trim() switch
        {
            "平铺" => "平铺",
            "1/2错位" or "二分之一错位" or "半落" => "1/2错位",
            _ => "不拼接"
        };

    private static string CalculateSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void WriteJson<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string SanitizeName(string value)
    {
        var sanitized = InvalidFileNameCharacters().Replace(value.Trim(), "_");
        return sanitized.Length > 40 ? sanitized[..40] : sanitized;
    }

    [GeneratedRegex("[\\\\/:*?\"<>|]+")]
    private static partial Regex InvalidFileNameCharacters();
}
