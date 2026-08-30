using FluentAssertions;
using Moq;
using PhotoshopMcpServer.Models;
using PhotoshopMcpServer.Services;
using PhotoshopMcpServer.Tools;
using Xunit;

namespace PhotoshopMcpServer.Tests;

public class PhotoshopProductionToolsTests
{
    private readonly Mock<IPhotoshopService> _photoshopService = new();
    private readonly Mock<ITaskWorkspaceService> _taskWorkspaceService = new();

    [Fact]
    public void SetTaskDimensions_UsesRecordedPixelsAndDpi()
    {
        var task = CreateTask();
        _taskWorkspaceService.Setup(service => service.LoadTask("task")).Returns(task);
        _photoshopService.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
        var tools = new PhotoshopProductionTools(
            _photoshopService.Object,
            _taskWorkspaceService.Object);

        var result = tools.按任务规格设置尺寸("task");

        result.Should().Contain("20000×20000");
        result.Should().Contain("2540 DPI");
        _photoshopService.Verify(service => service.ExecuteJavaScriptWithResult(
            It.Is<string>(script =>
                script.Contains("UnitValue(20000,'px')") && script.Contains(",2540,"))));
    }

    [Fact]
    public void CreateSeamlessCheck_UsesWraparoundOffset()
    {
        _taskWorkspaceService.Setup(service => service.LoadTask("task")).Returns(CreateTask());
        _photoshopService.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
        var tools = new PhotoshopProductionTools(
            _photoshopService.Object,
            _taskWorkspaceService.Object);

        tools.生成平铺无缝检查图("task");

        _photoshopService.Verify(service => service.ExecuteJavaScriptWithResult(
            It.Is<string>(script =>
                script.Contains("applyOffset") &&
                script.Contains("OffsetUndefinedAreas.WRAPAROUND"))));
    }

    [Theory]
    [InlineData(true, "允许导出")]
    [InlineData(false, "禁止导出生产版")]
    public void ProductionExportGate_UsesLatestReview(bool approved, string expected)
    {
        _taskWorkspaceService.Setup(service => service.IsApproved("task")).Returns(approved);
        var tools = new PhotoshopProductionTools(
            _photoshopService.Object,
            _taskWorkspaceService.Object);

        tools.检查是否允许导出生产版("task").Should().Contain(expected);
    }

    [Fact]
    public void CreateHalfDropCheck_BuildsTwoColumnOffsetLayout()
    {
        _taskWorkspaceService.Setup(service => service.LoadTask("task")).Returns(CreateTask());
        _photoshopService.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
        var tools = new PhotoshopProductionTools(
            _photoshopService.Object,
            _taskWorkspaceService.Object);

        tools.生成二分之一错位检查图("task");

        _photoshopService.Verify(service => service.ExecuteJavaScriptWithResult(
            It.Is<string>(script =>
                script.Contains("width*2,height*2") &&
                script.Contains("pasteAt(width,-height/2)") &&
                script.Contains("pasteAt(width,height/2)"))));
    }

    [Fact]
    public void ExportProductionFile_WhenNotApproved_DoesNotCallPhotoshop()
    {
        _taskWorkspaceService.Setup(service => service.LoadTask("task")).Returns(CreateTask());
        _taskWorkspaceService.Setup(service => service.IsApproved("task")).Returns(false);
        var tools = new PhotoshopProductionTools(
            _photoshopService.Object,
            _taskWorkspaceService.Object);

        tools.导出已批准生产版("task", @"D:\输出\结果.psd", "TIFF")
            .Should().Contain("禁止导出生产版");
        _photoshopService.Verify(
            service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void ExportProductionFile_WhenApproved_UsesFixedProductionDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "duanxing-export", Guid.NewGuid().ToString("N"));
        try
        {
            var taskDirectory = Path.Combine(root, "task");
            var outputDirectory = Path.Combine(taskDirectory, "02_处理结果");
            Directory.CreateDirectory(outputDirectory);
            var source = Path.Combine(outputDirectory, "已复核.psd");
            File.WriteAllText(source, "sample");
            _taskWorkspaceService.Setup(service => service.LoadTask(taskDirectory))
                .Returns(CreateTask(outputDirectory));
            _taskWorkspaceService.Setup(service => service.IsApproved(taskDirectory)).Returns(true);
            _taskWorkspaceService.Setup(service => service.IsResultApproved(taskDirectory, source)).Returns(true);
            _photoshopService.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
                .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
            var tools = new PhotoshopProductionTools(
                _photoshopService.Object,
                _taskWorkspaceService.Object);

            var result = tools.导出已批准生产版(taskDirectory, source, "TIFF");

            result.Should().Contain("04_生产版");
            _photoshopService.Verify(service => service.ExecuteJavaScriptWithResult(
                It.Is<string>(script =>
                    script.Contains("TiffSaveOptions") && script.Contains("_生产版.tif"))));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("平铺", "applyOffset")]
    [InlineData("1/2错位", "pasteAt(width,-height/2)")]
    [InlineData("不拼接", "resizeImage")]
    public void OneClickPreview_SelectsWorkflowFromTask(string tilingMode, string expectedScript)
    {
        var task = CreateTask() with { TilingMode = tilingMode };
        _taskWorkspaceService.Setup(service => service.LoadTask("task")).Returns(task);
        _photoshopService.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
        var tools = new PhotoshopProductionTools(
            _photoshopService.Object,
            _taskWorkspaceService.Object);

        tools.一键生成工艺检查版("task");

        _photoshopService.Verify(service => service.ExecuteJavaScriptWithResult(
            It.Is<string>(script => script.Contains(expectedScript))));
    }

    [Fact]
    public void CreateExtensionCanvas_UsesRecordedTargetAndKeepsLayers()
    {
        _taskWorkspaceService.Setup(service => service.LoadTask("task")).Returns(CreateTask());
        _photoshopService.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
        var tools = new PhotoshopProductionTools(
            _photoshopService.Object,
            _taskWorkspaceService.Object);

        tools.创建补图扩展画布("task");

        _photoshopService.Verify(service => service.ExecuteJavaScriptWithResult(
            It.Is<string>(script =>
                script.Contains("targetWidth=20000,targetHeight=20000") &&
                script.Contains("resizeCanvas") &&
                script.Contains("AI补图或人工修补区域"))));
    }

    [Fact]
    public void CreateSharpenPreview_DuplicatesLayerAndUsesValidatedParameters()
    {
        _taskWorkspaceService.Setup(service => service.LoadTask("task")).Returns(CreateTask());
        _photoshopService.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(true, "ok", string.Empty));
        var tools = new PhotoshopProductionTools(
            _photoshopService.Object,
            _taskWorkspaceService.Object);

        var result = tools.生成基础清晰化预览("task", 80, 1.2, 2);

        result.Should().Contain("基础清晰化预览已生成");
        _photoshopService.Verify(service => service.ExecuteJavaScriptWithResult(
            It.Is<string>(script =>
                script.Contains("activeLayer.duplicate") &&
                script.Contains("applyUnSharpMask(80,1.2,2)"))));
    }

    private static DuanxingTaskRecord CreateTask(string outputDirectory = @"D:\输出\任务\02_处理结果")
        => new(
            "DX-test",
            "待处理",
            DateTimeOffset.Now.ToString("O"),
            @"D:\样板\原图.tif",
            "hash",
            @"D:\输出\任务\01_工作副本\原图.tif",
            outputDirectory,
            "木纹无缝",
            200,
            200,
            2540,
            20000,
            20000,
            400_000_000,
            "平铺",
            "TIFF",
            "张三",
            []);
}
