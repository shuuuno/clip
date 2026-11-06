param([Parameter(Mandatory=$true)][string]$Output, [string]$ProductProject, [switch]$Baseline)
. "$PSScriptRoot\env.ps1"
$reviewProject = Join-Path $ClipRoot '.tools\design-polish\review'
New-Item -ItemType Directory -Force $reviewProject | Out-Null
if (-not $ProductProject) { $ProductProject = Join-Path $ClipRoot 'src\Clip\Clip.csproj' }
$productProject = [System.Security.SecurityElement]::Escape([System.IO.Path]::GetFullPath($ProductProject))
$reviewSource = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'design-polish-review.cs'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows10.0.18362.0</TargetFramework><UseWPF>true</UseWPF><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup><ProjectReference Include="$productProject"/><Compile Include="$reviewSource"/></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $reviewProject 'Review.csproj') -Encoding UTF8
$reviewArguments = @([System.IO.Path]::GetFullPath($Output))
if ($Baseline) { $reviewArguments += '--baseline' }
& $Dotnet run --project (Join-Path $reviewProject 'Review.csproj') -c Release -- @reviewArguments
if ($LASTEXITCODE -ne 0) { throw 'WPF 디자인 검증 실패' }
