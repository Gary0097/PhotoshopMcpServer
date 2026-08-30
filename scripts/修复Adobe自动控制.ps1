$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()
trap {
    Write-Host ''
    Write-Host "修复未完成：$($_.Exception.Message)" -ForegroundColor Red
    Write-Host '没有删除任何 Adobe 文件。请把本窗口拍照发给实施人员。'
    exit 1
}

Write-Host ''
Write-Host '端行 Adobe 自动控制修复' -ForegroundColor Cyan
Write-Host '========================' -ForegroundColor Cyan

$photoshopClsid = (Get-ItemProperty -LiteralPath `
    'Registry::HKEY_CLASSES_ROOT\Photoshop.Application\CLSID').'(default)'
$photoshopTypeLib = (Get-ItemProperty -LiteralPath `
    "Registry::HKEY_CLASSES_ROOT\CLSID\$photoshopClsid\TypeLib").'(default)'
$machineBase = "Registry::HKEY_CLASSES_ROOT\TypeLib\$photoshopTypeLib\1.0\0"
$workingLibrary = (Get-ItemProperty -LiteralPath `
    "$machineBase\Win32" -ErrorAction SilentlyContinue).'(default)'
$brokenLibrary = (Get-ItemProperty -LiteralPath `
    "$machineBase\win64" -ErrorAction SilentlyContinue).'(default)'

if ([string]::IsNullOrWhiteSpace($workingLibrary) -or
    -not (Test-Path -LiteralPath $workingLibrary -PathType Leaf)) {
    throw '没有找到可用的 Photoshop 2026 控制文件，请重新安装 Photoshop 2026。'
}

if (-not [string]::IsNullOrWhiteSpace($brokenLibrary) -and
    (Test-Path -LiteralPath $brokenLibrary -PathType Leaf)) {
    Write-Host 'Photoshop 64 位自动控制已经正常，不需要修复。' -ForegroundColor Green
    exit 0
}

$userKey = "HKCU:\Software\Classes\TypeLib\$photoshopTypeLib\1.0\0\win64"
New-Item -Path $userKey -Force | Out-Null
Set-ItemProperty -LiteralPath $userKey -Name '(default)' -Value $workingLibrary
$backupLibrary = if ($null -eq $brokenLibrary) { '' } else { $brokenLibrary }
Set-ItemProperty -LiteralPath $userKey -Name '端行修复前路径' -Value $backupLibrary
Set-ItemProperty -LiteralPath $userKey -Name '端行修复时间' -Value (Get-Date -Format 'o')

$effectiveLibrary = (Get-ItemProperty -LiteralPath `
    "$machineBase\win64" -ErrorAction SilentlyContinue).'(default)'
if (-not [string]::Equals(
    [IO.Path]::GetFullPath($effectiveLibrary),
    [IO.Path]::GetFullPath($workingLibrary),
    [StringComparison]::OrdinalIgnoreCase)) {
    throw '当前用户修复记录已写入，但系统尚未采用新路径。请注销 Windows 后重试。'
}

Write-Host '修复完成。' -ForegroundColor Green
Write-Host "旧路径：$brokenLibrary"
Write-Host "新路径：$workingLibrary"
Write-Host '请完全关闭 Photoshop 和 Codex 后重新打开，再运行现场自检。' -ForegroundColor Yellow
