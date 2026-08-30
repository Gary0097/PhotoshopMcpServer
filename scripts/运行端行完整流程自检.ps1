$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()
trap {
    Write-Host ''
    Write-Host "完整流程自检未通过：$($_.Exception.Message)" -ForegroundColor Red
    Write-Host '请把本窗口拍照发给实施人员。客户原图不会受影响。'
    exit 1
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$serverPath = Join-Path $repositoryRoot `
    'plugins\duanxing-creative-automation\server\win-x64\PhotoshopMcpServer.exe'
$adobeOutputRoot = Join-Path $repositoryRoot '现场自检结果'
$workflowOutputRoot = Join-Path $repositoryRoot '完整流程自检结果'

Write-Host ''
Write-Host '端行完整作图流程自检' -ForegroundColor Cyan
Write-Host '====================' -ForegroundColor Cyan
Write-Host '将自动执行：Adobe 自检 → 建任务 → 平铺检查版 → 复核绑定 → TIFF 生产版。'
Write-Host '全程使用自动生成的测试图，不读取或修改客户原图。'

if (-not (Test-Path -LiteralPath $serverPath -PathType Leaf)) {
    throw '没有找到端行作图服务，请先双击“安装端行作图助手.cmd”。'
}

Write-Host ''
Write-Host '第 1/2 步：生成非敏感 Adobe 测试文件'
& $serverPath --adobe-self-test $adobeOutputRoot
if ($LASTEXITCODE -ne 0) {
    throw 'Adobe 创建/保存测试文件失败。'
}
$latestAdobeDirectory = Get-ChildItem -LiteralPath $adobeOutputRoot -Directory |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
$sourceFile = Join-Path $latestAdobeDirectory.FullName 'Photoshop_自检.png'
if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
    throw 'Adobe 自检完成后没有找到 Photoshop 测试图。'
}

Write-Host ''
Write-Host '第 2/2 步：执行端行任务、复核和生产导出'
& $serverPath --workflow-self-test $sourceFile $workflowOutputRoot
if ($LASTEXITCODE -ne 0) {
    throw '端行任务、复核或生产导出失败。'
}

Write-Host ''
Write-Host '端行完整作图流程自检通过。' -ForegroundColor Green
Write-Host "自检任务保存在：$workflowOutputRoot"
Write-Host '下一步：使用客户确认的代表性样板做现场 POC。' -ForegroundColor Yellow
