param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()
trap {
    $technicalMessage = $_.Exception.Message
    $failureLog = Join-Path (Split-Path -Parent $PSScriptRoot) '部署故障详情.txt'
    Add-Content -LiteralPath $failureLog -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] 一键部署：$technicalMessage" -Encoding UTF8
    $customerMessage = if ($technicalMessage -match '[\u4e00-\u9fff]') { $technicalMessage } else { '电脑返回了技术错误，请联系实施人员处理。' }
    Write-Host ''
    Write-Host "一键部署未完成：$customerMessage" -ForegroundColor Red
    Write-Host '请按上方提示处理后重新双击；仍不明白时把“部署故障详情.txt”发给实施人员。'
    exit 1
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentScript = Join-Path $PSScriptRoot '检查端行作图环境.ps1'
$repairScript = Join-Path $PSScriptRoot '修复Adobe自动控制.ps1'
$installScript = Join-Path $PSScriptRoot '安装端行作图助手.ps1'
$toolListScript = Join-Path $PSScriptRoot '验证端行MCP工具.ps1'
$workflowScript = Join-Path $PSScriptRoot '运行端行完整流程自检.ps1'
$requiredScripts = @(
    $environmentScript,
    $repairScript,
    $installScript,
    $toolListScript,
    $workflowScript
)

Write-Host ''
Write-Host '端行 Codex 作图助手一键首次部署' -ForegroundColor Cyan
Write-Host '===============================' -ForegroundColor Cyan
Write-Host '将自动执行：环境检查 → 插件安装/更新 → Codex 工具检查 → Adobe 与完整作图流程自检。'

foreach ($script in $requiredScripts) {
    if (-not (Test-Path -LiteralPath $script -PathType Leaf)) {
        throw "部署文件不完整：$script"
    }
}

if ($DryRun) {
    Write-Host '[演练] 所有部署脚本完整。' -ForegroundColor Green
    Write-Host "[演练] 项目目录：$repositoryRoot"
    Write-Host '[演练] 不会修改 Codex、注册表或启动 Adobe。'
    exit 0
}

Write-Host ''
Write-Host '第 1/4 步：检查电脑环境'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $environmentScript
$environmentExitCode = $LASTEXITCODE
if ($environmentExitCode -ne 0) {
    Write-Host ''
    Write-Host '环境存在未通过项，尝试检查 Photoshop 自动控制。' -ForegroundColor Yellow
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $repairScript
    if ($LASTEXITCODE -ne 0) {
        throw '自动修复没有完成。'
    }
    throw '如果上方显示“修复完成”，请完全关闭 Photoshop 和 Codex，再重新双击本文件；其他红色项目请先按提示处理。'
}

Write-Host ''
Write-Host '第 2/4 步：安装或更新端行作图助手'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installScript -SkipEnvironmentCheck
if ($LASTEXITCODE -ne 0) {
    throw '插件安装或更新没有完成。'
}

Write-Host ''
Write-Host '第 3/4 步：检查 Codex 能否读取中文作图工具'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $toolListScript
if ($LASTEXITCODE -ne 0) {
    throw 'Codex 工具列表检查没有通过。'
}

Write-Host ''
Write-Host '第 4/4 步：运行 Adobe 和完整业务流程自检'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $workflowScript
if ($LASTEXITCODE -ne 0) {
    throw '完整作图流程自检没有通过。'
}

Write-Host ''
Write-Host '端行 Codex 作图助手首次部署完成。' -ForegroundColor Green
Write-Host '请关闭旧 Codex 任务，打开新任务，把原图拖进去，然后说：' -ForegroundColor Yellow
Write-Host '开始处理这张图：成品宽200毫米、高200毫米，精度2540，复核人张三。其他按默认。'
