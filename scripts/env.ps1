$ErrorActionPreference = 'Stop'
$ClipRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_ROOT = Join-Path $ClipRoot '.tools\dotnet'
$env:DOTNET_CLI_HOME = Join-Path $ClipRoot '.tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $ClipRoot '.tools\nuget'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $ClipRoot '.tools\nuget-http'
$env:TEMP = Join-Path $ClipRoot '.tools\tmp'
$env:TMP = $env:TEMP
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
New-Item -ItemType Directory -Force -Path $env:TEMP,$env:DOTNET_CLI_HOME,$env:NUGET_PACKAGES | Out-Null
$Dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
if (-not (Test-Path -LiteralPath $Dotnet)) { throw '프로젝트 로컬 .NET SDK가 필요합니다. README.md를 확인하세요.' }
