param(
    [switch]$DryRun,
    [switch]$SkipEnvironmentCheck
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()
trap {
    $technicalMessage = $_.Exception.Message
    $failureLog = Join-Path (Split-Path -Parent $PSScriptRoot) '部署故障详情.txt'
    Add-Content -LiteralPath $failureLog -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] 插件安装：$technicalMessage" -Encoding UTF8
    $customerMessage = if ($technicalMessage -match '[\u4e00-\u9fff]') { $technicalMessage } else { '电脑返回了技术错误，请联系实施人员处理。' }
    Write-Host ''
    Write-Host "安装未完成：$customerMessage" -ForegroundColor Red
    Write-Host '请把项目根目录的“部署故障详情.txt”发给实施人员。'
    exit 1
}
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
if ($SkipEnvironmentCheck) {
    Write-Host '一键部署已经完成环境检查，自动跳过。' -ForegroundColor Green
}
else {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $environmentCheck
    if ($LASTEXITCODE -ne 0) {
        throw '环境检查未通过，安装已经停止。'
    }
}

Write-Host ''
Write-Host '第 2/3 步：添加端行本地插件来源'
$marketplaceJson = & codex plugin marketplace list --json | Out-String
if ($LASTEXITCODE -ne 0) {
    throw '无法读取 Codex 插件来源。'
}
$marketplaces = ($marketplaceJson | ConvertFrom-Json).marketplaces
$personalMarketplace = $marketplaces | Where-Object { $_.name -eq 'personal' } | Select-Object -First 1
if ($null -eq $personalMarketplace) {
    $null = & codex plugin marketplace add $repositoryRoot --json
    if ($LASTEXITCODE -ne 0) {
        throw '添加端行插件来源失败。'
    }
    Write-Host '已添加端行插件来源。' -ForegroundColor Green
}
else {
    $configuredRoot = [IO.Path]::GetFullPath(($personalMarketplace.root -replace '^\\\\\?\\', ''))
    $currentRoot = [IO.Path]::GetFullPath($repositoryRoot)
    if (-not [string]::Equals($configuredRoot, $currentRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Codex 中已有名为 personal 的其他插件来源：$configuredRoot。请不要删除它，联系实施人员处理。"
    }
    Write-Host '端行插件来源已经存在，自动跳过。' -ForegroundColor Green
}

Write-Host ''
Write-Host '第 3/3 步：安装端行作图助手'
$installJson = & codex plugin add 'duanxing-creative-automation@personal' --json | Out-String
if ($LASTEXITCODE -ne 0) {
    throw '安装或更新端行作图助手失败。'
}
$installResult = $installJson | ConvertFrom-Json
$serverPath = Join-Path $installResult.installedPath 'server\win-x64\PhotoshopMcpServer.exe'
if (-not (Test-Path -LiteralPath $serverPath -PathType Leaf)) {
    throw '插件已登记，但没有找到作图服务程序。'
}
$serverProcess = Start-Process -FilePath $serverPath -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 3
if ($serverProcess.HasExited) {
    throw "作图服务启动失败，退出代码：$($serverProcess.ExitCode)"
}
Stop-Process -Id $serverProcess.Id -Force

Write-Host ''
Write-Host '安装完成。' -ForegroundColor Green
Write-Host "版本：$($installResult.version)"
Write-Host '作图服务健康检查：通过' -ForegroundColor Green
Write-Host '请关闭当前 Codex 任务，打开一个新任务，然后输入：'
Write-Host '检查端行作图环境。' -ForegroundColor Yellow
