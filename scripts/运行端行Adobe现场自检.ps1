$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()
trap {
    $technicalMessage = $_.Exception.Message
    $failureLog = Join-Path (Split-Path -Parent $PSScriptRoot) '部署故障详情.txt'
    Add-Content -LiteralPath $failureLog -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Adobe 现场自检：$technicalMessage" -Encoding UTF8
    $customerMessage = if ($technicalMessage -match '[\u4e00-\u9fff]') { $technicalMessage } else { '电脑返回了技术错误，请联系实施人员处理。' }
    Write-Host ''
    Write-Host "Adobe 现场自检未通过：$customerMessage" -ForegroundColor Red
    Write-Host '请把项目根目录的“部署故障详情.txt”发给实施人员。'
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
    throw '没有找到端行作图服务，请重新运行根目录的一键安装。'
}

$resultText = & $serverPath --adobe-self-test $outputRoot | Out-String
if ($LASTEXITCODE -ne 0) {
    throw 'Photoshop 或 Illustrator 实际创建/保存测试文件失败。'
}
$result = $resultText | ConvertFrom-Json

Write-Host ''
Write-Host 'Adobe 现场自检通过。' -ForegroundColor Green
Write-Host "Photoshop 版本：$($result.PhotoshopVersion)"
Write-Host "Illustrator 版本：$($result.IllustratorVersion)"
Write-Host "Photoshop 测试图：$($result.PhotoshopTestFile)"
Write-Host "Illustrator 测试线稿：$($result.IllustratorTestFile)"
Write-Host "中文自检记录：$($result.OutputDirectory)\自检记录.json"
Write-Host '下一步：打开 Codex，输入“检查端行作图环境”。' -ForegroundColor Yellow
