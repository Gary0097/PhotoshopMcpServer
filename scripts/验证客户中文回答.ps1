$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()

trap {
    Write-Host ''
    Write-Host "客户中文回答验收未通过：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resultFile = Join-Path ([IO.Path]::GetTempPath()) `
    ("端行中文回答-" + [Guid]::NewGuid().ToString('N') + '.txt')
$prompt = '帮助。我是端行员工，不懂英文。请告诉我最简单的使用方法。'

try {
    Write-Host ''
    Write-Host '端行客户中文回答验收' -ForegroundColor Cyan
    Write-Host '======================' -ForegroundColor Cyan
    Write-Host '正在启动临时 Codex 会话，只检查最终中文回答。'

    & codex exec --ephemeral --sandbox read-only --color never `
        --output-last-message $resultFile -C $repositoryRoot $prompt
    if ($LASTEXITCODE -ne 0) {
        throw '临时 Codex 会话没有正常完成。请检查 GPT 登录和 VPN 网络。'
    }
    if (-not (Test-Path -LiteralPath $resultFile -PathType Leaf)) {
        throw '没有收到 Codex 最终回答。'
    }

    $answer = Get-Content -LiteralPath $resultFile -Raw -Encoding UTF8
    $requiredPhrases = @('做这张', '按端行样板做', '通过并导出', '下一步做什么')
    foreach ($phrase in $requiredPhrases) {
        if ($answer -notmatch [regex]::Escape($phrase)) {
            throw "最终回答缺少口令：$phrase。"
        }
    }

    $forbiddenPatterns = @(
        'MCP',
        'JSON',
        'JavaScript',
        'task\.json',
        '[A-Za-z]:\\',
        '实施人员工具',
        '\.cmd'
    )
    foreach ($pattern in $forbiddenPatterns) {
        if ($answer -match $pattern) {
            throw '最终回答出现了客户不需要看到的技术词、路径或安装步骤。'
        }
    }
    if ($answer.Length -gt 350) {
        throw "最终回答过长，共 $($answer.Length) 个字符；应保持在 350 字以内。"
    }

    Write-Host ''
    Write-Host '客户中文回答验收通过。' -ForegroundColor Green
    Write-Host "回答长度：$($answer.Length) 个字符"
    Write-Host '三句主口令和备用下一步均正确，未发现技术词、路径或安装步骤。'
    Write-Host ''
    Write-Host '实际回答：'
    Write-Host $answer
}
finally {
    if (Test-Path -LiteralPath $resultFile) {
        Remove-Item -LiteralPath $resultFile -Force
    }
}
