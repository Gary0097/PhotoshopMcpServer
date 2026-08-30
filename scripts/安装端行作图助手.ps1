param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$pluginManifest = Join-Path $repositoryRoot `
    'plugins\duanxing-creative-automation\.codex-plugin\plugin.json'
$marketplaceManifest = Join-Path $repositoryRoot '.agents\plugins\marketplace.json'
$environmentCheck = Join-Path $PSScriptRoot '检查端行作图环境.ps1'

Write-Host ''
Write-Host '端行 Codex 作图助手安装程序' -ForegroundColor Cyan
Write-Host '===========================' -ForegroundColor Cyan

if (-not (Test-Path -LiteralPath $pluginManifest)) {
    throw "插件文件不完整：$pluginManifest"
}
if (-not (Test-Path -LiteralPath $marketplaceManifest)) {
    throw "插件市场配置不完整：$marketplaceManifest"
}
if (-not (Get-Command codex -ErrorAction SilentlyContinue)) {
    throw '没有找到 Codex。请先安装 Codex，登录 GPT 账号后再运行本程序。'
}

if ($DryRun) {
    Write-Host '[演练] 插件文件完整。' -ForegroundColor Green
    Write-Host "[演练] 将添加本地插件来源：$repositoryRoot"
    Write-Host '[演练] 将安装：duanxing-creative-automation@personal'
    Write-Host '[演练] 不会修改 Codex 配置。'
    exit 0
}

Write-Host '第 1/3 步：检查电脑环境'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $environmentCheck
if ($LASTEXITCODE -ne 0) {
    throw '环境检查未通过，安装已经停止。'
}

Write-Host ''
Write-Host '第 2/3 步：添加端行本地插件来源'
& codex plugin marketplace add $repositoryRoot --json
if ($LASTEXITCODE -ne 0) {
    throw '添加插件来源失败。请把本窗口内容发给实施人员。'
}

Write-Host ''
Write-Host '第 3/3 步：安装端行作图助手'
& codex plugin add 'duanxing-creative-automation@personal' --json
if ($LASTEXITCODE -ne 0) {
    throw '安装插件失败。请把本窗口内容发给实施人员。'
}

Write-Host ''
Write-Host '安装完成。' -ForegroundColor Green
Write-Host '请关闭当前 Codex 任务，打开一个新任务，然后输入：'
Write-Host '检查端行作图环境。' -ForegroundColor Yellow
