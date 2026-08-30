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
    throw 'The bundled Duanxing MCP server is missing and no source project was found.'
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The plugin bundle is incomplete. A .NET 10 SDK is required for source fallback.'
}

& dotnet run --project $projectPath --no-launch-profile
exit $LASTEXITCODE
