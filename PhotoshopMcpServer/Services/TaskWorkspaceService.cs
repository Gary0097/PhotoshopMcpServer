using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PhotoshopMcpServer.Models;

namespace PhotoshopMcpServer.Services;

public sealed partial class TaskWorkspaceService : ITaskWorkspaceService
{
    private readonly string _recentTasksFile;
    private readonly string _presetsFile;

    private static readonly HashSet<string> SupportedInputExtensions = new(
        [".psd", ".psb", ".tif", ".tiff", ".png", ".jpg", ".jpeg", ".bmp"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SupportedOutputFormats = new(
        ["PSD", "PSB", "TIFF", "PNG", "JPEG", "AI", "SVG", "PDF"],
        StringComparer.OrdinalIgnoreCase);

    public TaskWorkspaceService()
        : this(ResolveRecentTasksFile())
    {
    }

    public TaskWorkspaceService(string recentTasksFile)
    {
        _recentTasksFile = Path.GetFullPath(recentTasksFile);
        var stateDirectory = Path.GetDirectoryName(_recentTasksFile)
            ?? throw new InvalidOperationException("无法确定端行状态保存目录。");
        _presetsFile = Path.Combine(stateDirectory, "规格模板.json");
    }

    private static string ResolveRecentTasksFile()
    {
        var configuredPath = Environment.GetEnvironmentVariable(
            "DUANXING_RECENT_TASKS_FILE");
        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "端行作图助手",
                "最近任务.json")
            : configuredPath;
    }

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
        RememberTask(taskDirectory);
        return record;
    }

    public DuanxingTaskRecord FindMostRecentTask()
    {
        if (!File.Exists(_recentTasksFile))
            throw new InvalidOperationException("还没有最近任务，请先拖入一张原图并说“开始”。");

        List<string> taskDirectories;
        try
        {
            taskDirectories = JsonSerializer.Deserialize<List<string>>(
                File.ReadAllText(_recentTasksFile)) ?? [];
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("最近任务记录无法读取，请拖入原图后说“继续这张图”。");
        }

        foreach (var taskDirectory in taskDirectories)
        {
            try
            {
                return LoadTask(taskDirectory);
            }
            catch (IOException)
            {
                continue;
            }
            catch (InvalidOperationException)
            {
                continue;
            }
        }

        throw new InvalidOperationException("最近任务已经移动或删除，请拖入原图后说“继续这张图”。");
    }

    public DuanxingProductionPreset SaveMostRecentTaskAsPreset(string presetName)
    {
        var normalizedName = ValidatePresetName(presetName);
        var task = FindMostRecentTask();
        var preset = new DuanxingProductionPreset(
            normalizedName,
            DateTimeOffset.Now.ToString("O"),
            task.WidthMillimeters,
            task.HeightMillimeters,
            task.Dpi,
            task.TilingMode,
            task.OutputFormat,
            task.Reviewer);
        var presets = LoadProductionPresets().ToList();
        presets.RemoveAll(item => string.Equals(
            item.Name,
            normalizedName,
            StringComparison.OrdinalIgnoreCase));
        presets.Insert(0, preset);
        if (presets.Count > 50)
            presets.RemoveRange(50, presets.Count - 50);
        var directory = Path.GetDirectoryName(_presetsFile)
            ?? throw new InvalidOperationException("无法保存规格模板。");
        Directory.CreateDirectory(directory);
        WriteJson(_presetsFile, presets);
        return preset;
    }

    public DuanxingProductionPreset GetProductionPreset(string presetName)
    {
        var normalizedName = ValidatePresetName(presetName);
        return LoadProductionPresets().FirstOrDefault(item => string.Equals(
            item.Name,
            normalizedName,
            StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"没有名为“{normalizedName}”的规格模板。请说“查看规格模板”。");
    }

    public IReadOnlyList<DuanxingProductionPreset> GetProductionPresets()
        => LoadProductionPresets();

    public DuanxingReviewRecord SaveReview(
        string taskDirectory,
        string reviewer,
        bool approved,
        string comment)
    {
        if (string.IsNullOrWhiteSpace(taskDirectory))
            throw new ArgumentException("任务目录不能为空。");
        if (string.IsNullOrWhiteSpace(reviewer))
            throw new ArgumentException("复核人不能为空。");

        var fullTaskDirectory = Path.GetFullPath(taskDirectory);
        var task = LoadTask(fullTaskDirectory);
        var resultFile = string.Empty;
        var resultSha256 = string.Empty;
        if (approved)
        {
            var resultFiles = Directory.Exists(task.OutputDirectory)
                ? Directory.GetFiles(task.OutputDirectory)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .ToArray()
                : [];
            if (resultFiles.Length == 0)
                throw new InvalidOperationException("还没有处理结果，不能记录“通过”。请先生成检查版。");
            resultFile = resultFiles[0];
            resultSha256 = CalculateSha256(resultFile);
        }
        var review = new DuanxingReviewRecord(
            task.TaskId,
            DateTimeOffset.Now.ToString("O"),
            reviewer.Trim(),
            approved,
            comment?.Trim() ?? string.Empty,
            approved ? "已批准，可导出生产版" : "已退回，需要修改",
            resultFile,
            resultSha256);
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
            throw new ArgumentException("请说明人工智能做了什么，例如：补图扩展、清晰修复或纹理生成。");

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
            throw new ArgumentException("任务目录不能为空。");
        var fullTaskDirectory = Path.GetFullPath(taskDirectory);
        var taskFile = Path.Combine(fullTaskDirectory, "task.json");
        if (!File.Exists(taskFile))
            throw new InvalidOperationException("没有找到 task.json，这不是有效的端行任务目录。");
        return JsonSerializer.Deserialize<DuanxingTaskRecord>(File.ReadAllText(taskFile))
            ?? throw new InvalidOperationException("任务记录无法读取。");
    }

    public DuanxingTaskRecord FindLatestTaskForSource(string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
            throw new FileNotFoundException("找不到原图，请重新拖入原图后再试。", sourceFile);

        var fullSourcePath = Path.GetFullPath(sourceFile);
        var sourceDirectory = Path.GetDirectoryName(fullSourcePath)
            ?? throw new InvalidOperationException("无法确定原图所在目录。");
        var outputRoot = Path.Combine(sourceDirectory, "端行作图输出");
        if (!Directory.Exists(outputRoot))
            throw new InvalidOperationException("这张原图还没有默认任务，请先说“开始处理这张图”。");

        var matchingTasks = new List<(DuanxingTaskRecord Task, DateTimeOffset CreatedAt)>();
        foreach (var taskDirectory in Directory.GetDirectories(outputRoot))
        {
            var taskFile = Path.Combine(taskDirectory, "task.json");
            if (!File.Exists(taskFile))
                continue;
            try
            {
                var task = LoadTask(taskDirectory);
                if (string.Equals(
                    Path.GetFullPath(task.SourceFile),
                    fullSourcePath,
                    StringComparison.OrdinalIgnoreCase))
                    matchingTasks.Add((
                        task,
                        DateTimeOffset.TryParse(task.CreatedAt, out var createdAt)
                            ? createdAt
                            : File.GetLastWriteTimeUtc(taskFile)));
            }
            catch (JsonException)
            {
                continue;
            }
        }

        if (matchingTasks.Count == 0)
            throw new InvalidOperationException("没有找到这张原图的历史任务，请先说“开始处理这张图”。");
        return matchingTasks
            .OrderByDescending(item => item.CreatedAt)
            .First()
            .Task;
    }

    public bool IsApproved(string taskDirectory)
    {
        var task = LoadTask(taskDirectory);
        var review = LoadLatestReview(Path.GetFullPath(taskDirectory));
        return review != null &&
            review.TaskId == task.TaskId &&
            review.Approved &&
            IsReviewResultValid(task, review);
    }

    public bool IsResultApproved(string taskDirectory, string resultFile)
    {
        var task = LoadTask(taskDirectory);
        var review = LoadLatestReview(Path.GetFullPath(taskDirectory));
        if (review == null || review.TaskId != task.TaskId || !review.Approved)
            return false;
        var requestedPath = Path.GetFullPath(resultFile);
        return string.Equals(requestedPath, Path.GetFullPath(review.ResultFile), StringComparison.OrdinalIgnoreCase) &&
            IsReviewResultValid(task, review);
    }

    public string GetApprovedResultFile(string taskDirectory)
    {
        var task = LoadTask(taskDirectory);
        var review = LoadLatestReview(Path.GetFullPath(taskDirectory));
        if (review == null || review.TaskId != task.TaskId || !review.Approved)
            throw new InvalidOperationException("尚未复核通过，请先生成中文复核单并回答“通过”。");
        if (!IsReviewResultValid(task, review))
            throw new InvalidOperationException("已批准文件丢失或内容发生变化，请重新复核。");
        return Path.GetFullPath(review.ResultFile);
    }

    public DuanxingReviewSummary BuildReviewSummary(string taskDirectory)
    {
        var fullTaskDirectory = Path.GetFullPath(taskDirectory);
        var task = LoadTask(fullTaskDirectory);
        var resultFiles = Directory.Exists(task.OutputDirectory)
            ? Directory.GetFiles(task.OutputDirectory)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray()
            : [];
        var aiDirectory = Path.Combine(fullTaskDirectory, "05_AI记录");
        var aiRecords = Directory.Exists(aiDirectory)
            ? Directory.GetFiles(aiDirectory, "ai-*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray()
            : [];
        DuanxingAiResultRecord latestAiRecord = null;
        if (aiRecords.Length > 0)
            latestAiRecord = JsonSerializer.Deserialize<DuanxingAiResultRecord>(File.ReadAllText(aiRecords[0]));
        var latestReview = LoadLatestReview(fullTaskDirectory);
        var approved = latestReview != null &&
            latestReview.TaskId == task.TaskId &&
            latestReview.Approved &&
            IsReviewResultValid(task, latestReview);
        var checklist = new List<string>
        {
            $"尺寸应为 {task.WidthMillimeters} × {task.HeightMillimeters} mm，{task.Dpi} DPI",
            "检查主体纹理、颜色和清晰度是否符合样板",
            "检查图片中没有多余文字、标志、边框或无关内容"
        };
        if (task.TilingMode == "平铺")
            checklist.Add("检查中央横缝、中央竖缝以及四边连续性");
        else if (task.TilingMode == "1/2错位")
            checklist.Add("检查中间竖缝、右列横向连续性和半高错位位置");
        else
            checklist.Add("检查画面边缘和成品裁切范围");

        var reviewStatus = latestReview == null
            ? "尚未复核"
            : approved
                ? latestReview.Status
                : latestReview.Approved
                    ? "原批准已失效，需要重新复核"
                    : latestReview.Status;
        return new DuanxingReviewSummary(
            task.TaskId,
            fullTaskDirectory,
            resultFiles.FirstOrDefault() ?? string.Empty,
            resultFiles.Length,
            aiRecords.Length,
            latestAiRecord?.Operation ?? string.Empty,
            File.Exists(task.SourceFile) && CalculateSha256(task.SourceFile) == task.SourceSha256,
            File.Exists(task.WorkingCopy),
            approved,
            reviewStatus,
            checklist);
    }

    public DuanxingTaskProgress BuildTaskProgress(string taskDirectory)
    {
        var fullTaskDirectory = Path.GetFullPath(taskDirectory);
        var task = LoadTask(fullTaskDirectory);
        var summary = BuildReviewSummary(fullTaskDirectory);
        var productionDirectory = Path.Combine(fullTaskDirectory, "04_生产版");
        var productionFileCount = Directory.Exists(productionDirectory)
            ? Directory.GetFiles(productionDirectory).Length
            : 0;

        string status;
        string nextStep;
        if (!summary.OriginalUnchanged)
        {
            status = "已停止：原图异常";
            nextStep = "请停止操作并联系实施人员检查原图。";
        }
        else if (productionFileCount > 0)
        {
            status = "已导出生产版";
            nextStep = "需要交付材料时说“生成中文交付报告”。";
        }
        else if (summary.Approved)
        {
            status = "已复核通过，等待导出";
            nextStep = "说“直接导出生产版”。";
        }
        else if (summary.ResultFileCount > 0 && summary.ReviewStatus.StartsWith("已退回"))
        {
            status = "已退回，等待修改";
            nextStep = "说明要修改的位置和要求，或说“回到上一版”。";
        }
        else if (summary.ResultFileCount > 0)
        {
            status = summary.ReviewStatus.StartsWith("原批准已失效")
                ? "结果有变化，需要重新复核"
                : "已有处理结果，等待复核";
            nextStep = "说“给我看结果”，然后回答“通过”或“退回修改”。";
        }
        else
        {
            status = "任务已建立，等待处理";
            nextStep = "说“直接做检查版”，或说明要做的处理。";
        }

        return new DuanxingTaskProgress(
            task.TaskId,
            status,
            nextStep,
            summary.ResultFileCount,
            productionFileCount,
            summary.OriginalUnchanged);
    }

    public DuanxingDeliveryReport GenerateDeliveryReport(string taskDirectory, string stage)
    {
        var normalizedStage = stage?.Trim() switch
        {
            "首次部署" => "首次部署",
            "POC" or "概念验证" => "POC",
            "UAT" or "用户验收" => "UAT",
            "正式交付" or "交付" => "正式交付",
            _ => "POC"
        };
        var fullTaskDirectory = Path.GetFullPath(taskDirectory);
        var task = LoadTask(fullTaskDirectory);
        var summary = BuildReviewSummary(fullTaskDirectory);
        var productionDirectory = Path.Combine(fullTaskDirectory, "04_生产版");
        var productionFiles = Directory.Exists(productionDirectory)
            ? Directory.GetFiles(productionDirectory)
                .OrderBy(Path.GetFileName)
                .ToArray()
            : [];
        var readyForSignOff = summary.OriginalUnchanged &&
            summary.WorkingCopyExists &&
            summary.Approved &&
            summary.ResultFileCount > 0 &&
            productionFiles.Length > 0;
        var reportDirectory = Path.Combine(fullTaskDirectory, "06_交付记录");
        Directory.CreateDirectory(reportDirectory);
        var reportFile = Path.Combine(
            reportDirectory,
            $"{normalizedStage}_交付报告_{DateTime.Now:yyyyMMdd-HHmmss}.md");
        var builder = new StringBuilder();
        builder.AppendLine($"# 端行{normalizedStage}交付报告");
        builder.AppendLine();
        builder.AppendLine($"- 任务名称：{task.TaskName}");
        builder.AppendLine($"- 任务编号：{task.TaskId}");
        builder.AppendLine($"- 生成时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"- 成品规格：{task.WidthMillimeters} × {task.HeightMillimeters} mm，{task.Dpi} DPI");
        builder.AppendLine($"- 拼接方式：{task.TilingMode}");
        builder.AppendLine($"- 输出格式：{task.OutputFormat}");
        builder.AppendLine($"- 指定复核人：{task.Reviewer}");
        builder.AppendLine($"- 当前结论：{(readyForSignOff ? "材料齐全，等待双方签字" : "材料未齐全，暂不能签字")}");
        builder.AppendLine();
        builder.AppendLine("## 自动检查");
        builder.AppendLine();
        builder.AppendLine($"- 原图保护：{(summary.OriginalUnchanged ? "通过" : "未通过")}");
        builder.AppendLine($"- 工作副本：{(summary.WorkingCopyExists ? "存在" : "缺失")}");
        builder.AppendLine($"- 处理结果：{summary.ResultFileCount} 个");
        builder.AppendLine($"- AI 处理记录：{summary.AiResultCount} 个");
        builder.AppendLine($"- 人工复核：{summary.ReviewStatus}");
        builder.AppendLine($"- 生产版：{productionFiles.Length} 个");
        builder.AppendLine($"- 原图校验值：{task.SourceSha256}");
        builder.AppendLine();
        builder.AppendLine("## 文件清单");
        builder.AppendLine();
        AppendFile(builder, "原图", task.SourceFile, fullTaskDirectory);
        AppendFile(builder, "工作副本", task.WorkingCopy, fullTaskDirectory);
        if (!string.IsNullOrWhiteSpace(summary.LatestResultFile))
            AppendFile(builder, "最新处理结果", summary.LatestResultFile, fullTaskDirectory);
        foreach (var productionFile in productionFiles)
            AppendFile(builder, "生产版", productionFile, fullTaskDirectory);
        builder.AppendLine();
        builder.AppendLine("## 人工验收项");
        builder.AppendLine();
        foreach (var item in summary.ManualChecklist)
            builder.AppendLine($"- [ ] {item}");
        builder.AppendLine("- [ ] 已对照确认样板检查整体效果");
        builder.AppendLine("- [ ] 已确认输出文件可以正常打开");
        builder.AppendLine();
        builder.AppendLine("## 问题与限制");
        builder.AppendLine();
        builder.AppendLine("- 问题分类：无 / 程序缺陷 / 工艺参数 / AI 波动 / 样板问题 / 新增需求");
        builder.AppendLine("- 具体说明：");
        builder.AppendLine();
        builder.AppendLine("## 双方确认");
        builder.AppendLine();
        builder.AppendLine("- 甲方验收人：________________");
        builder.AppendLine("- 乙方实施人：________________");
        builder.AppendLine("- 验收结论：通过 / 限制条件下通过 / 退回修改");
        builder.AppendLine("- 日期：________________");
        File.WriteAllText(reportFile, builder.ToString(), new UTF8Encoding(false));
        return new DuanxingDeliveryReport(
            task.TaskId,
            normalizedStage,
            DateTimeOffset.Now.ToString("O"),
            reportFile,
            readyForSignOff,
            readyForSignOff ? "材料齐全，等待双方签字" : "材料未齐全，暂不能签字");
    }

    public DuanxingRollbackRecord RestorePreviousResult(string taskDirectory)
    {
        var fullTaskDirectory = Path.GetFullPath(taskDirectory);
        var task = LoadTask(fullTaskDirectory);
        var resultFiles = Directory.Exists(task.OutputDirectory)
            ? Directory.GetFiles(task.OutputDirectory)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ThenByDescending(Path.GetFileName)
                .ToArray()
            : [];
        if (resultFiles.Length < 2)
            throw new InvalidOperationException("目前没有上一版可以恢复。至少生成两个结果后才能回退。");

        var latestFile = Path.GetFullPath(resultFiles[0]);
        var previousFile = Path.GetFullPath(resultFiles[1]);
        var restoredName = $"回到上一版_{DateTime.Now:yyyyMMdd-HHmmss-fff}_" +
            $"{Guid.NewGuid():N}"[..6] +
            Path.GetExtension(previousFile).ToLowerInvariant();
        var restoredFile = Path.Combine(task.OutputDirectory, restoredName);
        File.Copy(previousFile, restoredFile, overwrite: false);

        SaveReview(
            fullTaskDirectory,
            task.Reviewer,
            false,
            $"已回到上一版：{Path.GetFileName(previousFile)}。请重新复核。");
        var record = new DuanxingRollbackRecord(
            task.TaskId,
            DateTimeOffset.Now.ToString("O"),
            latestFile,
            previousFile,
            restoredFile,
            "已恢复上一版，等待人工复核");
        var operationDirectory = Path.Combine(fullTaskDirectory, "07_操作记录");
        Directory.CreateDirectory(operationDirectory);
        WriteJson(
            Path.Combine(operationDirectory, $"回退-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json"),
            record);
        return record;
    }

    private static void AppendFile(
        StringBuilder builder,
        string label,
        string filePath,
        string taskDirectory)
    {
        var fullPath = Path.GetFullPath(filePath);
        var displayPath = fullPath.StartsWith(taskDirectory, StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(taskDirectory, fullPath)
            : fullPath;
        var size = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
        builder.AppendLine($"- {label}：{displayPath}（{size} 字节）");
    }

    private static DuanxingReviewRecord LoadLatestReview(string taskDirectory)
    {
        var reviewDirectory = Path.Combine(taskDirectory, "03_复核记录");
        if (!Directory.Exists(reviewDirectory))
            return null;
        var latestReview = Directory.GetFiles(reviewDirectory, "review-*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        return latestReview == null
            ? null
            : JsonSerializer.Deserialize<DuanxingReviewRecord>(File.ReadAllText(latestReview));
    }

    private static bool IsReviewResultValid(DuanxingTaskRecord task, DuanxingReviewRecord review)
    {
        if (string.IsNullOrWhiteSpace(review.ResultFile) || string.IsNullOrWhiteSpace(review.ResultSha256))
            return false;
        var resultPath = Path.GetFullPath(review.ResultFile);
        var outputDirectory = Path.GetFullPath(task.OutputDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!resultPath.StartsWith(outputDirectory, StringComparison.OrdinalIgnoreCase) || !File.Exists(resultPath))
            return false;
        return CalculateSha256(resultPath) == review.ResultSha256;
    }

    private static void ValidateRequest(DuanxingTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceFile) || !File.Exists(request.SourceFile))
            throw new FileNotFoundException("找不到原图，请检查文件路径。", request.SourceFile);
        if (!SupportedInputExtensions.Contains(Path.GetExtension(request.SourceFile)))
            throw new ArgumentException("原图格式不支持。请使用 PSD、PSB、TIFF、PNG、JPEG 或 BMP。");
        if (string.IsNullOrWhiteSpace(request.OutputRoot))
            throw new ArgumentException("请选择输出目录。");
        if (string.IsNullOrWhiteSpace(request.TaskName))
            throw new ArgumentException("请填写任务名称。");
        if (request.WidthMillimeters <= 0 || request.HeightMillimeters <= 0)
            throw new ArgumentException("成品宽度和高度必须大于 0 毫米。");
        if (request.Dpi is < 72 or > 10000)
            throw new ArgumentException("印刷精度必须在 72 到 10000 之间。");
        if (!SupportedOutputFormats.Contains(request.OutputFormat ?? string.Empty))
            throw new ArgumentException("输出格式不支持。");
        if (string.IsNullOrWhiteSpace(request.Reviewer))
            throw new ArgumentException("请填写复核人。");
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

    private IReadOnlyList<DuanxingProductionPreset> LoadProductionPresets()
    {
        if (!File.Exists(_presetsFile))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<DuanxingProductionPreset>>(
                File.ReadAllText(_presetsFile)) ?? [];
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                "规格模板记录无法读取，请联系实施人员检查配置。");
        }
    }

    private static string ValidatePresetName(string presetName)
    {
        var normalizedName = presetName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new ArgumentException("请给规格模板起一个中文名称，例如“木纹”。");
        if (normalizedName.Length > 30)
            throw new ArgumentException("规格模板名称不能超过 30 个字。请使用简短名称。");
        if (normalizedName.Any(char.IsControl))
            throw new ArgumentException("规格模板名称不能包含换行或控制字符。");
        return normalizedName;
    }

    private void RememberTask(string taskDirectory)
    {
        List<string> taskDirectories = [];
        if (File.Exists(_recentTasksFile))
        {
            try
            {
                taskDirectories = JsonSerializer.Deserialize<List<string>>(
                    File.ReadAllText(_recentTasksFile)) ?? [];
            }
            catch (JsonException)
            {
                taskDirectories = [];
            }
        }

        var fullTaskDirectory = Path.GetFullPath(taskDirectory);
        taskDirectories.RemoveAll(path => string.Equals(
            Path.GetFullPath(path),
            fullTaskDirectory,
            StringComparison.OrdinalIgnoreCase));
        taskDirectories.Insert(0, fullTaskDirectory);
        if (taskDirectories.Count > 20)
            taskDirectories.RemoveRange(20, taskDirectories.Count - 20);

        var recentDirectory = Path.GetDirectoryName(_recentTasksFile)
            ?? throw new InvalidOperationException("无法保存最近任务记录。");
        Directory.CreateDirectory(recentDirectory);
        WriteJson(_recentTasksFile, taskDirectories);
    }

    private static string SanitizeName(string value)
    {
        var sanitized = InvalidFileNameCharacters().Replace(value.Trim(), "_");
        return sanitized.Length > 40 ? sanitized[..40] : sanitized;
    }

    [GeneratedRegex("[\\\\/:*?\"<>|]+")]
    private static partial Regex InvalidFileNameCharacters();
}
