using FluentAssertions;
using PhotoshopMcpServer.Services;
using Xunit;

namespace PhotoshopMcpServer.Tests;

public class SupportReportServiceTests
{
    [Fact]
    public void Create_WritesOneChineseReportAndRemovesSensitiveDetails()
    {
        var directory = Path.Combine(Path.GetTempPath(), "端行故障报告测试-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var logPath = Path.Combine(directory, "技术错误.log");
            File.WriteAllText(logPath,
                "打开 C:\\客户\\秘密纹理.tif 失败 token=abc123456789 sk-proj-secret123456\n" +
                "   at Company.Secret.Run()\n");

            var reportPath = SupportReportService.Create(
                "Photoshop：正常\n安装目录：K:\\TOOL\\Adobe Photoshop 2026",
                directory,
                logPath);
            var report = File.ReadAllText(reportPath);

            Path.GetFileName(reportPath).Should().Be("端行作图故障报告.txt");
            report.Should().Contain("端行作图助手故障报告");
            report.Should().Contain("本地文件");
            report.Should().NotContain("C:\\");
            report.Should().NotContain("K:\\");
            report.Should().NotContain("abc123456789");
            report.Should().NotContain("sk-proj-secret123456");
            report.Should().NotContain("Company.Secret.Run");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
