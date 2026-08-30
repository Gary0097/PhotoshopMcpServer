$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()

trap {
    Write-Host ''
    Write-Host "客户中文回答验收未通过：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$skillRoot = Join-Path $repositoryRoot `
    'plugins\duanxing-creative-automation\skills\duanxing-image-craft'
$skillPath = Join-Path $skillRoot 'SKILL.md'
$skillText = Get-Content -LiteralPath $skillPath -Raw -Encoding UTF8
if ((Get-Item -LiteralPath $skillPath).Length -gt 7000) {
    throw '端行技能入口超过 7 KB，会拖慢客户的简单口令。请把专项细节移入按需参考。'
}
$referenceLinks = [regex]::Matches($skillText, '\]\((references/[^)]+)\)')
foreach ($match in $referenceLinks) {
    $referencePath = Join-Path $skillRoot ($match.Groups[1].Value -replace '/', '\')
    if (-not (Test-Path -LiteralPath $referencePath -PathType Leaf)) {
        throw "端行技能引用缺失：$($match.Groups[1].Value)"
    }
}
$testRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ("端行中文回答-" + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testRoot -Force
$previousRecentTasks = $env:DUANXING_RECENT_TASKS_FILE
$previousReportDirectory = $env:DUANXING_SUPPORT_REPORT_DIRECTORY
$previousTechnicalLog = $env:DUANXING_TECHNICAL_LOG_PATH
$env:DUANXING_RECENT_TASKS_FILE = Join-Path $testRoot '不存在的最近任务.json'
$env:DUANXING_SUPPORT_REPORT_DIRECTORY = $testRoot
$env:DUANXING_TECHNICAL_LOG_PATH = Join-Path $testRoot '技术错误.log'

function Invoke-CustomerScenario(
    [string]$Name,
    [string]$Prompt,
    [string[]]$RequiredPhrases,
    [int]$MaximumLength = 350
) {
    $resultFile = Join-Path $testRoot ($Name + '.txt')
    $traceFile = Join-Path $testRoot ($Name + '-过程.txt')
    Write-Host "正在验证：$Name"
    $null = & codex exec --ephemeral --sandbox read-only --color never `
        --output-last-message $resultFile -C $repositoryRoot $Prompt 2> $traceFile
    if ($LASTEXITCODE -ne 0) {
        throw "【${Name}】临时会话没有正常完成。请检查 GPT 登录和 VPN 网络。"
    }
    if (-not (Test-Path -LiteralPath $resultFile -PathType Leaf)) {
        throw "【${Name}】没有收到 Codex 最终回答。"
    }

    $answer = Get-Content -LiteralPath $resultFile -Raw -Encoding UTF8
    foreach ($phrase in $RequiredPhrases) {
        if ($answer -notmatch [regex]::Escape($phrase)) {
            throw "【${Name}】最终回答缺少口令：$phrase。实际回答：$($answer.Trim())"
        }
    }
    $forbiddenPatterns = @(
        'MCP', 'JSON', 'JavaScript', 'task\.json', '[A-Za-z]:\\',
        '实施人员工具', '\.cmd', 'HRESULT', 'Exception', 'API[_ -]?Key',
        '截图', '\bgit\b', '构建', '项目当前状态', '部署日志'
    )
    foreach ($pattern in $forbiddenPatterns) {
        if ($answer -match $pattern) {
            throw "【${Name}】最终回答出现了客户不需要看到的技术词、路径或安装步骤。"
        }
    }
    if ($answer.Length -gt $MaximumLength) {
        throw "【${Name}】最终回答过长，共 $($answer.Length) 个字符；上限为 $MaximumLength 字。"
    }
    $tokenLine = Select-String -LiteralPath $traceFile -Pattern 'tokens used\s+([0-9,]+)' |
        Select-Object -Last 1
    if ($null -ne $tokenLine -and $tokenLine.Matches.Count -gt 0) {
        Write-Host "【$Name】会话用量：$($tokenLine.Matches[0].Groups[1].Value) tokens"
    }
    return $answer.Trim()
}

try {
    Write-Host ''
    Write-Host '端行零培训客户中文回答验收' -ForegroundColor Cyan
    Write-Host '============================' -ForegroundColor Cyan
    Write-Host "轻量技能入口：$((Get-Item -LiteralPath $skillPath).Length) 字节，按需参考：$($referenceLinks.Count) 份。"
    Write-Host '将启动三个互相隔离的新 Codex 会话，只检查客户最终看到的中文回答。'

    $helpAnswer = Invoke-CustomerScenario `
        '帮助' `
        '帮助。我是端行员工，不懂英文。请告诉我最简单的使用方法。' `
        @('做这张', '按端行样板做', '通过并导出', '下一步做什么', '生成故障报告')
    $failureAnswer = Invoke-CustomerScenario `
        '还是不行' `
        '还是不行，帮我排查。' `
        @('故障报告') `
        160
    $nextAnswer = Invoke-CustomerScenario `
        '下一步做什么' `
        '下一步做什么？我是端行员工，不懂英文。' `
        @() `
        180
    $validNextActions = @('做这张', '给我看结果', '继续上次', '通过并导出', '退回修改')
    $matchedNextActions = @($validNextActions | Where-Object {
        $nextAnswer -match [regex]::Escape($_)
    })
    if ($matchedNextActions.Count -ne 1) {
        throw "【下一步做什么】必须只给一个有效下一步。实际回答：$nextAnswer"
    }

    Write-Host ''
    Write-Host '零培训客户中文回答验收通过。' -ForegroundColor Green
    Write-Host '帮助、失败恢复和真实任务下一步均为短中文回答，未出现技术词、路径或安装步骤。'
    Write-Host ''
    Write-Host "【帮助】$helpAnswer"
    Write-Host "【还是不行】$failureAnswer"
    Write-Host "【下一步做什么】$nextAnswer"
}
finally {
    if ($null -eq $previousRecentTasks) { Remove-Item Env:DUANXING_RECENT_TASKS_FILE -ErrorAction SilentlyContinue }
    else { $env:DUANXING_RECENT_TASKS_FILE = $previousRecentTasks }
    if ($null -eq $previousReportDirectory) { Remove-Item Env:DUANXING_SUPPORT_REPORT_DIRECTORY -ErrorAction SilentlyContinue }
    else { $env:DUANXING_SUPPORT_REPORT_DIRECTORY = $previousReportDirectory }
    if ($null -eq $previousTechnicalLog) { Remove-Item Env:DUANXING_TECHNICAL_LOG_PATH -ErrorAction SilentlyContinue }
    else { $env:DUANXING_TECHNICAL_LOG_PATH = $previousTechnicalLog }
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
