$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()
trap {
    Write-Host ''
    Write-Host "Adobe 现场自检未通过：$($_.Exception.Message)" -ForegroundColor Red
    Write-Host '请把本窗口拍照发给实施人员。'
    exit 1
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$serverPath = Join-Path $repositoryRoot `
    'plugins\duanxing-creative-automation\server\win-x64\PhotoshopMcpServer.exe'
$outputRoot = Join-Path $repositoryRoot '现场自检结果'

Write-Host ''
Write-Host '端行 Adobe 现场自检' -ForegroundColor Cyan
Write-Host '===================' -ForegroundColor Cyan
Write-Host '本次只创建非敏感测试文件，不读取或修改客户原图。'

if (-not (Test-Path -LiteralPath $serverPath -PathType Leaf)) {
    throw '没有找到端行作图服务，请先双击“安装端行作图助手.cmd”。'
}

& $serverPath --adobe-self-test $outputRoot
if ($LASTEXITCODE -ne 0) {
    throw 'Photoshop 或 Illustrator 实际创建/保存测试文件失败。'
}

Write-Host ''
Write-Host 'Adobe 现场自检通过。' -ForegroundColor Green
Write-Host "自检文件保存在：$outputRoot"
Write-Host '下一步：打开 Codex，输入“检查端行作图环境”。' -ForegroundColor Yellow
