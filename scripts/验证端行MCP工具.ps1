$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()
trap {
    Write-Host ''
    Write-Host "Codex 工具检查未通过：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$serverPath = Join-Path $repositoryRoot `
    'plugins\duanxing-creative-automation\server\win-x64\PhotoshopMcpServer.exe'
if (-not [string]::IsNullOrWhiteSpace($env:DUANXING_TEST_SERVER_PATH)) {
    $serverPath = [IO.Path]::GetFullPath($env:DUANXING_TEST_SERVER_PATH)
}
if (-not (Test-Path -LiteralPath $serverPath -PathType Leaf)) {
    throw '没有找到端行作图服务，请重新运行根目录的一键安装。'
}

$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $serverPath
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardInput = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$protocolTestLocalAppData = Join-Path ([IO.Path]::GetTempPath()) `
    ("端行MCP验收-" + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $protocolTestLocalAppData -Force
$startInfo.Environment['DUANXING_RECENT_TASKS_FILE'] = Join-Path `
    $protocolTestLocalAppData '最近任务.json'
$startInfo.Environment['DUANXING_SUPPORT_REPORT_DIRECTORY'] = $protocolTestLocalAppData
$startInfo.Environment['DUANXING_TECHNICAL_LOG_PATH'] = Join-Path `
    $protocolTestLocalAppData '技术错误.log'
$process = [Diagnostics.Process]::new()
$process.StartInfo = $startInfo
$null = $process.Start()

function Send-ProtocolMessage([hashtable]$message) {
    $json = $message | ConvertTo-Json -Compress -Depth 10
    $escaped = [Text.StringBuilder]::new()
    foreach ($character in $json.ToCharArray()) {
        if ([int]$character -gt 127) {
            $null = $escaped.Append(('\u{0:x4}' -f [int]$character))
        }
        else {
            $null = $escaped.Append($character)
        }
    }
    $json = $escaped.ToString()
    $process.StandardInput.WriteLine($json)
    $process.StandardInput.Flush()
}

try {
    Send-ProtocolMessage @{
        jsonrpc = '2.0'
        id = 1
        method = 'initialize'
        params = @{
            protocolVersion = '2025-06-18'
            capabilities = @{}
            clientInfo = @{ name = '端行验收'; version = '1.0' }
        }
    }
    $initialize = $process.StandardOutput.ReadLine() | ConvertFrom-Json
    if ($null -eq $initialize.result.serverInfo) {
        throw '作图服务没有完成初始化。'
    }
    Send-ProtocolMessage @{
        jsonrpc = '2.0'
        method = 'notifications/initialized'
        params = @{}
    }
    Send-ProtocolMessage @{
        jsonrpc = '2.0'
        id = 2
        method = 'tools/list'
        params = @{}
    }
    $toolResponse = $process.StandardOutput.ReadLine() | ConvertFrom-Json
    if ($null -ne $toolResponse.error) {
        throw 'Codex 无法读取端行工具列表。'
    }
    $tools = @($toolResponse.result.tools)
    $required = @(
        'duanxing_make_this_image',
        'duanxing_approve_and_export_latest',
        'duanxing_save_recent_as_preset',
        'duanxing_list_presets',
        'duanxing_start_from_preset_and_run',
        'duanxing_batch_start_from_preset',
        'duanxing_batch_approve_and_export',
        'duanxing_start_and_run',
        'duanxing_start_like_recent_and_run',
        'duanxing_continue_and_run',
        'duanxing_show_latest_result',
        'duanxing_approve_latest_result',
        'duanxing_reject_latest_result',
        'duanxing_export_latest_approved'
        'duanxing_trace_wave_reference'
        'duanxing_generate_support_report'
    )
    $forbidden = @(
        'photoshop_execute_script',
        'photoshop_open_document',
        'duanxing_prepare_task_simple',
        'duanxing_prepare_like_recent'
    )
    $names = @($tools | ForEach-Object { $_.name })
    foreach ($name in $required) {
        if ($name -notin $names) {
            throw '缺少客户日常作图工具，请重新安装插件。'
        }
    }
    foreach ($name in $forbidden) {
        if ($name -in $names) {
            throw '客户模式加载了不应显示的底层工具，请联系实施人员。'
        }
    }
    if (($names | Select-Object -Unique).Count -ne $names.Count) {
        throw '工具列表中存在重复项目，请联系实施人员。'
    }
    foreach ($tool in $tools) {
        if ([string]::IsNullOrWhiteSpace($tool.title) -or
            $tool.title -notmatch '[\u4e00-\u9fff]') {
            throw '发现没有中文显示标题的工具，客户确认框可能出现英文，请联系实施人员。'
        }
        if ([string]::IsNullOrWhiteSpace($tool.description) -or
            $tool.description -notmatch '[\u4e00-\u9fff]') {
            throw '发现没有中文说明的工具，请联系实施人员。'
        }
        $parameterNames = @(
            $tool.inputSchema.properties.psobject.Properties |
                ForEach-Object { $_.Name }
        )
        foreach ($parameterName in $parameterNames) {
            if ($parameterName -notmatch '[\u4e00-\u9fff]') {
                throw '发现不是中文的参数名称，请联系实施人员。'
            }
        }
    }
    $readOnlyTools = @(
        'duanxing_check_environment',
        'duanxing_list_presets',
        'duanxing_continue_latest_task',
        'duanxing_continue_most_recent_task',
        'duanxing_get_chinese_prompts',
        'duanxing_check_export_approval'
    )
    foreach ($name in $readOnlyTools) {
        $tool = $tools | Where-Object { $_.name -eq $name } | Select-Object -First 1
        if ($tool.annotations.readOnlyHint -ne $true) {
            throw '只读工具没有正确标记，可能向客户显示多余确认。'
        }
    }
    $writeTools = @(
        'duanxing_make_this_image',
        'duanxing_approve_and_export_latest',
        'duanxing_batch_approve_and_export',
        'duanxing_export_latest_approved'
    )
    foreach ($name in $writeTools) {
        $tool = $tools | Where-Object { $_.name -eq $name } | Select-Object -First 1
        if ($tool.annotations.readOnlyHint -eq $true) {
            throw '写入或导出工具被错误标成只读，安全确认可能失效。'
        }
    }
    Send-ProtocolMessage @{
        jsonrpc = '2.0'
        id = 3
        method = 'tools/call'
        params = @{
            name = 'duanxing_get_chinese_prompts'
            arguments = @{}
        }
    }
    $helpResponse = $process.StandardOutput.ReadLine() | ConvertFrom-Json
    if ($null -ne $helpResponse.error -or
        $helpResponse.result.isError -eq $true -or
        $helpResponse.result.content[0].text -notmatch '只需要记三句话' -or
        $helpResponse.result.content[0].text -notmatch '按端行样板做') {
        throw '中文帮助工具无法正常执行，请重新安装插件。'
    }
    Send-ProtocolMessage @{
        jsonrpc = '2.0'
        id = 4
        method = 'tools/call'
        params = @{
            name = 'duanxing_make_this_image'
            arguments = @{
                原图路径 = 'C:\端行验收\首次原图.png'
                成品宽度毫米 = 0
                成品高度毫米 = 0
                印刷精度 = 0
                复核人 = ''
                拼接方式 = ''
                输出格式 = ''
            }
        }
    }
    $makeResponse = $process.StandardOutput.ReadLine() | ConvertFrom-Json
    $makePayload = $makeResponse.result.content[0].text | ConvertFrom-Json
    if ($null -ne $makeResponse.error -or
        $makeResponse.result.isError -eq $true -or
        $makePayload.下一步 -notmatch '请一次告诉我') {
        throw '做这张入口无法给首次客户提供中文引导，请重新安装插件。'
    }
    Send-ProtocolMessage @{
        jsonrpc = '2.0'
        id = 5
        method = 'tools/call'
        params = @{
            name = 'duanxing_make_this_image'
            arguments = @{
                原图路径 = 'C:\端行验收\不存在的秘密原图.tif'
                成品宽度毫米 = 100
                成品高度毫米 = 100
                印刷精度 = 300
                拼接方式 = '平铺'
                输出格式 = 'TIFF'
                复核人 = '自动验收'
            }
        }
    }
    $errorResponse = $process.StandardOutput.ReadLine() | ConvertFrom-Json
    $errorText = $errorResponse.result.content[0].text
    $errorPayload = $errorText | ConvertFrom-Json
    $customerErrorText = $errorPayload | ConvertTo-Json -Depth 5
    if ($null -ne $errorResponse.error -or
        $errorResponse.result.isError -eq $true -or
        $customerErrorText -notmatch '[\u4e00-\u9fff]' -or
        $customerErrorText -match '[A-Za-z]:\\|HRESULT|Exception| at ') {
        throw '客户错误提示泄露了英文技术信息或本地路径。'
    }
    Send-ProtocolMessage @{
        jsonrpc = '2.0'
        id = 6
        method = 'tools/call'
        params = @{
            name = 'duanxing_generate_support_report'
            arguments = @{}
        }
    }
    $reportResponse = $process.StandardOutput.ReadLine() | ConvertFrom-Json
    $reportPayload = $reportResponse.result.content[0].text | ConvertFrom-Json
    $reportPath = Join-Path $protocolTestLocalAppData '端行作图故障报告.txt'
    if ($null -ne $reportResponse.error -or
        $reportResponse.result.isError -eq $true -or
        $reportPayload.成功 -ne $true -or
        -not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
        throw '中文故障报告无法自动生成。'
    }
    $reportText = Get-Content -LiteralPath $reportPath -Raw
    if ($reportText -match '[A-Za-z]:\\|sk-[A-Za-z0-9_-]{8,}|\bat\s+[A-Za-z_]') {
        throw '中文故障报告没有正确隐藏本地路径或技术信息。'
    }
    Write-Host ''
    Write-Host 'Codex 工具列表检查通过。' -ForegroundColor Green
    Write-Host "客户模式工具数量：$($tools.Count)"
    Write-Host '“做这张”、“按端行样板做”、波纹矢量、通过并导出等入口均正常。'
    Write-Host '所有工具标题、说明和参数名称均为中文，三句口令帮助和首次规格引导均可正常调用。'
    Write-Host '只读检查不会多余确认，改图和导出仍保留安全确认。'
    Write-Host '失败场景只返回中文处理提示，不泄露本地路径或英文技术异常。'
    Write-Host '客户说“还是不行”时可一键生成桌面中文脱敏故障报告。'
    Write-Host '底层任意脚本和旧的不完整入口均未加载。'
}
finally {
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(3000)) {
        $process.Kill()
        $process.WaitForExit()
    }
    $process.Dispose()
    if (Test-Path -LiteralPath $protocolTestLocalAppData) {
        Remove-Item -LiteralPath $protocolTestLocalAppData -Recurse -Force
    }
}
