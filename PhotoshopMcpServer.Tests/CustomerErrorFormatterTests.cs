using FluentAssertions;
using PhotoshopMcpServer.Services;
using Xunit;

namespace PhotoshopMcpServer.Tests;

public class CustomerErrorFormatterTests
{
    [Fact]
    public void EnglishTechnicalError_IsReplacedWithChineseAction()
    {
        var result = CustomerErrorFormatter.Format(
            new InvalidOperationException("RPC server is unavailable. HRESULT 0x800706BA"));

        result.Should().Be("本次操作没有完成。请重试一次；仍失败时把当前画面发给实施人员。");
        result.Should().NotMatchRegex("HRESULT|0x[0-9A-Fa-f]+|RPC");
    }

    [Fact]
    public void ChineseError_IsKeptButLocalPathIsHidden()
    {
        var result = CustomerErrorFormatter.Format(
            "找不到文件 C:\\客户样板\\秘密纹理.tif，请重新选择。");

        result.Should().StartWith("找不到文件");
        result.Should().Contain("本地文件");
        result.Should().NotContain("C:\\");
    }
}
