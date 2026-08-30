# Photoshop MCP Server

## 端行中文作图插件

本仓库已经集成 `duanxing-creative-automation` Codex 插件。端行员工不需要学习英文命令、MCP、COM 或 Photoshop 脚本，只需在 Codex 中用中文描述任务。

日常只用四个动作：开始、继续、复核、导出。不知道下一步时直接说“下一步做什么？”。

必备条件：已购买可用的 GPT 服务，已安装并登录 Codex，已安装并激活 Photoshop 2026 与 Illustrator 2026。

最简单的开始方式：

1. 双击 `检查端行作图环境.cmd`。
2. 若 Photoshop 64 位控制文件未通过，双击 `修复Adobe自动控制.cmd`。
3. 全部通过后，双击 `安装端行作图助手.cmd`。
4. 首次部署双击 `运行端行Adobe现场自检.cmd`。
5. 现场验收双击 `运行端行完整流程自检.cmd`。
6. 关闭旧 Codex 任务并新建任务，然后输入：

同一个安装文件可重复运行并用于后续更新；它会自动跳过已有来源并执行作图服务健康检查。

> 检查端行作图环境。

> 开始处理这张图：成品 200×200 mm，2540 DPI，复核人张三。其他按默认，直接做检查版。

复核时只说“通过”或“退回修改”；通过后说“直接导出生产版”，无需填写文件路径或英文格式。

中文资料：

- [端行客户使用手册](docs/端行客户使用手册.md)
- [端行中文口令卡](docs/端行中文口令卡.md)
- [端行现场交付检查表](docs/端行现场交付检查表.md)
- [产品需求文档](PRD.md)
- [项目实施流程](IMPLEMENTATION_WORKFLOW.md)

插件源码位于 `plugins/duanxing-creative-automation`。正常生产模式默认禁止任意 Photoshop JavaScript，只允许经过封装的业务级流程。

---

🌐 **Language**: **English** | [한국어](README.ko.md)

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server that enables AI assistants to control Adobe Photoshop via Windows COM automation. Built with .NET 10 and C# 14 using the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

## Overview

This MCP server exposes Photoshop automation as a set of tools that any MCP-compatible AI client (Claude Desktop, GitHub Copilot, etc.) can invoke. The primary tool is `ExecuteJavaScript`, which gives the AI full access to Photoshop's scripting engine — allowing it to flexibly decide what to do at runtime.

## Requirements

- **Windows** (COM automation is Windows-only)
- **Adobe Photoshop** (any version supporting COM automation and `DoJavaScript`)
- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** or later

## Installation

### 1. Clone the repository

```bash
git clone https://github.com/airtaxi/PhotoshopMcpServer.git
cd PhotoshopMcpServer
```

### 2. Build

```bash
dotnet build
```

### 3. (Optional) Publish as a self-contained executable

```bash
dotnet publish PhotoshopMcpServer/PhotoshopMcpServer.csproj -c Release -r win-x64 --self-contained
```

The executable will be in `PhotoshopMcpServer/bin/Release/net10.0-windows/win-x64/publish/`.

## MCP Server Configuration

### Claude Desktop

Add the following to your Claude Desktop configuration file:

- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`

#### Option A: Run from source (requires .NET SDK)

```json
{
  "mcpServers": {
    "photoshop": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\PhotoshopMcpServer\\PhotoshopMcpServer"]
    }
  }
}
```

#### Option B: Run published executable

```json
{
  "mcpServers": {
    "photoshop": {
      "command": "C:\\path\\to\\PhotoshopMcpServer.exe"
    }
  }
}
```

### GitHub Copilot (VS Code)

Add to your `.vscode/mcp.json` or VS Code settings:

```json
{
  "servers": {
    "photoshop": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\PhotoshopMcpServer\\PhotoshopMcpServer"]
    }
  }
}
```

### Cursor

Add to your Cursor MCP settings (`~/.cursor/mcp.json`):

```json
{
  "mcpServers": {
    "photoshop": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\PhotoshopMcpServer\\PhotoshopMcpServer"]
    }
  }
}
```

> **Note**: Replace `C:\path\to\PhotoshopMcpServer` with the actual path where you cloned the repository.

## Available Tools

| Tool | Description |
|------|-------------|
| `ExecuteJavaScript` | **Primary tool** — execute arbitrary JavaScript in Photoshop's scripting engine |
| `IsPhotoshopRunning` | Check if Photoshop is running and accessible |
| `LaunchPhotoshop` | Launch Photoshop or connect to a running instance |
| `GetPhotoshopVersion` | Get the Photoshop version string |
| `GetActiveDocumentInfo` | Get info about the active document (name, size, color mode, resolution) |
| `GetOpenDocuments` | List all open document names |
| `OpenDocument` | Open an image file by path |
| `SaveActiveDocument` | Save the current document |
| `CreateNewDocument` | Create a new document with specified dimensions |
| `ExportAsPng` | Export the active document as PNG |
| `ExportAsJpeg` | Export the active document as JPEG with quality setting |
| `GetLayerInfo` | Get a summary of all layers in the active document |

### ExecuteJavaScript — The Power Tool

The `ExecuteJavaScript` tool is intentionally flexible. It allows the AI to construct and execute any valid Photoshop JavaScript, giving it full control over Photoshop. Example scripts:

```javascript
// Get document name
app.activeDocument.name

// Create a new document
app.documents.add(1920, 1080, 72, "My Canvas")

// Resize the active document
app.activeDocument.resizeImage(800, 600)

// Flatten all layers
app.activeDocument.flatten()

// Apply Gaussian blur to the active layer
app.activeDocument.activeLayer.applyGaussianBlur(5.0)

// Get all layer names
var names = [];
for (var i = 0; i < app.activeDocument.layers.length; i++)
    names.push(app.activeDocument.layers[i].name);
names.join(", ");
```

## Project Structure

```
PhotoshopMcpServer/
├── .github/
│   └── copilot-instructions.md          # C# code style rules for Copilot
├── PhotoshopMcpServer/
│   ├── Program.cs                       # MCP server entry point (stdio transport)
│   ├── Models/
│   │   └── PhotoshopModels.cs           # Record types for results and document info
│   ├── Services/
│   │   ├── IPhotoshopService.cs         # Photoshop COM service interface
│   │   └── PhotoshopService.cs          # COM automation implementation
│   └── Tools/
│       └── PhotoshopTools.cs            # MCP tool definitions (13 tools)
├── PhotoshopMcpServer.Tests/
│   ├── PhotoshopToolsTests.cs           # Tool unit tests (28 tests)
│   └── PhotoshopServiceTests.cs         # Model tests (7 tests)
├── PhotoshopMcpServer.slnx
├── LICENSE
└── README.md
```

## Running Tests

```bash
dotnet test
```

## How It Works

1. The MCP server starts and communicates over **stdio** (standard input/output)
2. An AI client connects and discovers the available tools
3. When the AI invokes a tool, the server uses **Windows COM automation** to send commands to Photoshop
4. Photoshop executes the command (typically via `DoJavaScript`) and returns the result
5. The result is sent back to the AI client

```
AI Client ←→ MCP (stdio) ←→ PhotoshopMcpServer ←→ COM ←→ Adobe Photoshop
```

## License

This project is licensed under the [MIT License](LICENSE).

## Acknowledgements

- [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) — Official MCP SDK for .NET
- [GitHub Copilot](https://github.com/features/copilot) — AI-assisted development of this project

## Author

**Howon Lee** ([@airtaxi](https://github.com/airtaxi))
