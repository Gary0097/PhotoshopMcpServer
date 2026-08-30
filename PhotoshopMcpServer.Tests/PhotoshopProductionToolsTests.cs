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

    private static DuanxingTaskRecord CreateTask()
        => new(
            "DX-test",
            "待处理",
            DateTimeOffset.Now.ToString("O"),
            @"D:\样板\原图.tif",
            "hash",
            @"D:\输出\任务\01_工作副本\原图.tif",
            @"D:\输出\任务\02_处理结果",
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
