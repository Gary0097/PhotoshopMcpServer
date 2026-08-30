using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoshopMcpServer.Services;
using System.Text.Json;

if (args.Length > 0 && args[0] == "--adobe-self-test")
{
    var outputRoot = args.Length > 1
        ? args[1]
        : Path.Combine(AppContext.BaseDirectory, "现场自检结果");
    try
    {
        using var photoshopService = new PhotoshopService();
        using var illustratorService = new IllustratorService();
        var runner = new AdobeSelfTestRunner(photoshopService, illustratorService);
        var result = runner.Run(outputRoot);
        var json = JsonSerializer.Serialize(
            result,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(
            Path.Combine(result.OutputDirectory, "自检记录.json"),
            json,
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.WriteLine(json);
        Environment.ExitCode = 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Adobe 现场自检未通过：{exception.Message}");
        Environment.ExitCode = 1;
    }
    return;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<IPhotoshopService, PhotoshopService>();
builder.Services.AddSingleton<IIllustratorService, IllustratorService>();
builder.Services.AddSingleton<ITaskWorkspaceService, TaskWorkspaceService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
