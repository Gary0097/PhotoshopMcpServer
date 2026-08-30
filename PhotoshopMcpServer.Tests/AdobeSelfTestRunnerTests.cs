using FluentAssertions;
using Moq;
using PhotoshopMcpServer.Models;
using PhotoshopMcpServer.Services;
using Xunit;

namespace PhotoshopMcpServer.Tests;

public class AdobeSelfTestRunnerTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "duanxing-adobe-self-test",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Run_CreatesBothAdobeEvidenceFiles()
    {
        var photoshop = new Mock<IPhotoshopService>();
        var illustrator = new Mock<IIllustratorService>();
        photoshop.Setup(service => service.IsPhotoshopRunning()).Returns(true);
        illustrator.Setup(service => service.IsIllustratorRunning()).Returns(true);
        photoshop.Setup(service => service.GetPhotoshopVersion()).Returns("27.0");
        illustrator.Setup(service => service.GetIllustratorVersion()).Returns("30.0");
        photoshop.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(() =>
            {
                var outputDirectory = Directory.GetDirectories(_testRoot).Single();
                File.WriteAllText(Path.Combine(outputDirectory, "Photoshop_自检.png"), "png");
                return new PhotoshopScriptResult(true, "ok", string.Empty);
            });
        illustrator.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(() =>
            {
                var outputDirectory = Directory.GetDirectories(_testRoot).Single();
                File.WriteAllText(Path.Combine(outputDirectory, "Illustrator_自检.ai"), "ai");
                return new IllustratorScriptResult(true, "ok", string.Empty);
            });
        var runner = new AdobeSelfTestRunner(photoshop.Object, illustrator.Object, 1, 0);

        var result = runner.Run(_testRoot);

        result.Success.Should().BeTrue();
        result.PhotoshopVersion.Should().Be("27.0");
        result.IllustratorVersion.Should().Be("30.0");
        File.Exists(result.PhotoshopTestFile).Should().BeTrue();
        File.Exists(result.IllustratorTestFile).Should().BeTrue();
        result.Messages.Should().OnlyContain(message => !message.Contains("MCP"));
        photoshop.Verify(service => service.LaunchPhotoshop());
        illustrator.Verify(service => service.LaunchIllustrator());
    }

    [Fact]
    public void Run_WhenPhotoshopFails_StopsBeforeIllustrator()
    {
        var photoshop = new Mock<IPhotoshopService>();
        var illustrator = new Mock<IIllustratorService>();
        photoshop.Setup(service => service.IsPhotoshopRunning()).Returns(true);
        photoshop.Setup(service => service.GetPhotoshopVersion()).Returns("27.0");
        photoshop.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(new PhotoshopScriptResult(false, string.Empty, "测试错误"));
        var runner = new AdobeSelfTestRunner(photoshop.Object, illustrator.Object, 1, 0);

        var action = () => runner.Run(_testRoot);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Photoshop 启动后仍无法执行测试作图*");
        illustrator.Verify(service => service.LaunchIllustrator(), Times.Never);
    }

    [Fact]
    public void Run_WhenAdobeLaunchReturnsAnEarlyError_WaitsForRunningInstance()
    {
        var photoshop = new Mock<IPhotoshopService>();
        var illustrator = new Mock<IIllustratorService>();
        photoshop.Setup(service => service.LaunchPhotoshop())
            .Throws(new InvalidOperationException("Adobe 尚未完成启动"));
        photoshop.Setup(service => service.IsPhotoshopRunning()).Returns(true);
        illustrator.Setup(service => service.IsIllustratorRunning()).Returns(true);
        photoshop.Setup(service => service.GetPhotoshopVersion()).Returns("27.0");
        illustrator.Setup(service => service.GetIllustratorVersion()).Returns("30.0");
        photoshop.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(() =>
            {
                var outputDirectory = Directory.GetDirectories(_testRoot).Single();
                File.WriteAllText(Path.Combine(outputDirectory, "Photoshop_自检.png"), "png");
                return new PhotoshopScriptResult(true, "ok", string.Empty);
            });
        illustrator.Setup(service => service.ExecuteJavaScriptWithResult(It.IsAny<string>()))
            .Returns(() =>
            {
                var outputDirectory = Directory.GetDirectories(_testRoot).Single();
                File.WriteAllText(Path.Combine(outputDirectory, "Illustrator_自检.ai"), "ai");
                return new IllustratorScriptResult(true, "ok", string.Empty);
            });
        var runner = new AdobeSelfTestRunner(photoshop.Object, illustrator.Object, 2, 0);

        runner.Run(_testRoot).Success.Should().BeTrue();

        photoshop.Verify(service => service.LaunchPhotoshop(), Times.Once);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }
}
