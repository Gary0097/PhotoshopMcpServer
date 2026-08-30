$ErrorActionPreference = 'Stop'

$pluginRoot = Split-Path -Parent $PSScriptRoot
$bundledServer = Join-Path $pluginRoot 'server\win-x64\PhotoshopMcpServer.exe'

if (Test-Path -LiteralPath $bundledServer) {
    & $bundledServer
    exit $LASTEXITCODE
}

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $pluginRoot '..\..'))
$projectPath = Join-Path $repositoryRoot 'PhotoshopMcpServer\PhotoshopMcpServer.csproj'

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw '端行作图助手文件不完整，请重新运行根目录的一键安装。'
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '端行作图助手文件不完整，请重新运行根目录的一键安装。'
}

& dotnet run --project $projectPath --no-launch-profile
exit $LASTEXITCODE
