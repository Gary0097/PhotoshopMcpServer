param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()
trap {
    Write-Host ''
    Write-Host "一键部署未完成：$($_.Exception.Message)" -ForegroundColor Red
    Write-Host '请按上方提示处理后重新双击；不明白时把本窗口拍照发给实施人员。'
    exit 1
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentScript = Join-Path $PSScriptRoot '检查端行作图环境.ps1'
$repairScript = Join-Path $PSScriptRoot '修复Adobe自动控制.ps1'
$installScript = Join-Path $PSScriptRoot '安装端行作图助手.ps1'
$workflowScript = Join-Path $PSScriptRoot '运行端行完整流程自检.ps1'
$requiredScripts = @(
    $environmentScript,
    $repairScript,
    $installScript,
    $workflowScript
)

Write-Host ''
Write-Host '端行 Codex 作图助手一键首次部署' -ForegroundColor Cyan
Write-Host '===============================' -ForegroundColor Cyan
Write-Host '将自动执行：环境检查 → 插件安装/更新 → Adobe 与完整作图流程自检。'

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
Write-Host '第 1/3 步：检查电脑环境'
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
Write-Host '第 2/3 步：安装或更新端行作图助手'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installScript -SkipEnvironmentCheck
if ($LASTEXITCODE -ne 0) {
    throw '插件安装或更新没有完成。'
}

Write-Host ''
Write-Host '第 3/3 步：运行 Adobe 和完整业务流程自检'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $workflowScript
if ($LASTEXITCODE -ne 0) {
    throw '完整作图流程自检没有通过。'
}

Write-Host ''
Write-Host '端行 Codex 作图助手首次部署完成。' -ForegroundColor Green
Write-Host '请关闭旧 Codex 任务，打开新任务，把原图拖进去，然后说：' -ForegroundColor Yellow
Write-Host '开始处理这张图：成品 200×200 mm，2540 DPI，复核人张三。其他按默认，直接做检查版。'
