param(
    [string]$PhotoshopPath = 'K:\TOOL\Adobe Photoshop 2026',
    [string]$IllustratorPath = 'K:\TOOL\Adobe Illustrator 2026'
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()

function Write-CheckResult {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$SuccessText,
        [string]$FailureText
    )

    if ($Passed) {
        Write-Host "[通过] $Name - $SuccessText" -ForegroundColor Green
    }
    else {
        Write-Host "[未通过] $Name - $FailureText" -ForegroundColor Red
    }
}

Write-Host ''
Write-Host '端行 Codex 作图环境检查' -ForegroundColor Cyan
Write-Host '========================' -ForegroundColor Cyan

$codexCommand = Get-Command codex -ErrorAction SilentlyContinue
$photoshopInstalled = Test-Path -LiteralPath $PhotoshopPath -PathType Container
$illustratorInstalled = Test-Path -LiteralPath $IllustratorPath -PathType Container
$photoshopCom = $null -ne [Type]::GetTypeFromProgID('Photoshop.Application')
$illustratorCom = $null -ne [Type]::GetTypeFromProgID('Illustrator.Application')
$photoshopTypeLibraryReady = $false
if ($photoshopCom) {
    $photoshopClsid = (Get-ItemProperty -LiteralPath `
        'Registry::HKEY_CLASSES_ROOT\Photoshop.Application\CLSID' `
        -ErrorAction SilentlyContinue).'(default)'
    $photoshopTypeLib = (Get-ItemProperty -LiteralPath `
        "Registry::HKEY_CLASSES_ROOT\CLSID\$photoshopClsid\TypeLib" `
        -ErrorAction SilentlyContinue).'(default)'
    $photoshopTypeLibBase = "Registry::HKEY_CLASSES_ROOT\TypeLib\$photoshopTypeLib\1.0\0"
    $photoshopWin64Library = (Get-ItemProperty -LiteralPath `
        "$photoshopTypeLibBase\win64" -ErrorAction SilentlyContinue).'(default)'
    $photoshopWin32Library = (Get-ItemProperty -LiteralPath `
        "$photoshopTypeLibBase\Win32" -ErrorAction SilentlyContinue).'(default)'
    $photoshopTypeLibraryReady =
        (-not [string]::IsNullOrWhiteSpace($photoshopWin64Library) -and
            (Test-Path -LiteralPath $photoshopWin64Library -PathType Leaf)) -or
        ([string]::IsNullOrWhiteSpace($photoshopWin64Library) -and
            -not [string]::IsNullOrWhiteSpace($photoshopWin32Library) -and
            (Test-Path -LiteralPath $photoshopWin32Library -PathType Leaf))
}

Write-CheckResult 'Codex' ($null -ne $codexCommand) `
    "已安装：$($codexCommand.Source)" `
    '没有找到 Codex。请先安装 Codex 并重新打开本窗口。'
Write-CheckResult 'Photoshop 2026 目录' $photoshopInstalled `
    $PhotoshopPath `
    "没有找到：$PhotoshopPath"
Write-CheckResult 'Photoshop 自动控制' $photoshopCom `
    'Windows 已注册 Photoshop 自动控制接口' `
    '没有找到 Photoshop 自动控制接口。请启动一次 Photoshop 2026，仍失败时重新安装。'
Write-CheckResult 'Photoshop 64 位控制文件' $photoshopTypeLibraryReady `
    '类型库路径有效' `
    '类型库路径失效。请双击“修复Adobe自动控制.cmd”，然后重新检查。'
Write-CheckResult 'Illustrator 2026 目录' $illustratorInstalled `
    $IllustratorPath `
    "没有找到：$IllustratorPath"
Write-CheckResult 'Illustrator 自动控制' $illustratorCom `
    'Windows 已注册 Illustrator 自动控制接口' `
    '没有找到 Illustrator 自动控制接口。请启动一次 Illustrator 2026，仍失败时重新安装。'

Write-Host ''
Write-Host '请人工确认以下三项：' -ForegroundColor Yellow
Write-Host '[  ] 已购买 GPT，账号可以正常登录 Codex'
Write-Host '[  ] VPN 专线已经连接'
Write-Host '[  ] Photoshop 2026 和 Illustrator 2026 均已激活'

$automaticPassed = $null -ne $codexCommand -and
    $photoshopInstalled -and $illustratorInstalled -and
    $photoshopCom -and $photoshopTypeLibraryReady -and $illustratorCom

Write-Host ''
if ($automaticPassed) {
    Write-Host '自动检查通过。完成上面三项人工确认后，可以安装/使用端行作图助手。' `
        -ForegroundColor Green
    exit 0
}

Write-Host '自动检查未通过。请先处理红色项目，再进行现场部署。' -ForegroundColor Red
exit 1
