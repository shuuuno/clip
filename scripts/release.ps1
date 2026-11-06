param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [switch]$Upload
)
. "$PSScriptRoot\env.ps1"
$projectPath = Join-Path $ClipRoot 'src\Clip\Clip.csproj'
$projectText = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
$currentVersion = [version]([xml]$projectText).Project.PropertyGroup.Version
if ([version]$Version -lt $currentVersion) { throw '이전 버전으로 내릴 수 없습니다.' }
if ($Upload) {
    $dirty = & git -C $ClipRoot status --porcelain
    if ($LASTEXITCODE -ne 0 -or $dirty) { throw '업로드는 버전 변경을 커밋한 깨끗한 작업 트리에서만 가능합니다.' }
    if ([version]$Version -ne $currentVersion) { throw '먼저 업로드 없이 버전을 올리고 검증·커밋하세요.' }
    & gh release view "v$Version" --repo shuuuno/clip *> $null
    if ($LASTEXITCODE -eq 0) { throw '이미 있는 Release는 덮어쓰지 않습니다.' }
}
if ([version]$Version -ne $currentVersion) {
    $projectText = $projectText -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
    [IO.File]::WriteAllText($projectPath, $projectText, [Text.UTF8Encoding]::new($false))
}
& "$PSScriptRoot\build.ps1"
& "$PSScriptRoot\test.ps1"
$releaseRoot = Join-Path $ClipRoot "artifacts\release\$Version"
$publishDir = Join-Path $releaseRoot 'publish'
$packages = Join-Path $releaseRoot 'packages'
$expectedRoot = [IO.Path]::GetFullPath((Join-Path $ClipRoot 'artifacts\release')) + [IO.Path]::DirectorySeparatorChar
foreach ($outputDirectory in @($publishDir, $packages)) {
    $resolvedOutput = [IO.Path]::GetFullPath($outputDirectory)
    if (-not $resolvedOutput.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw '배포 경로가 잘못됐습니다.' }
    if (Test-Path -LiteralPath $resolvedOutput) { Remove-Item -LiteralPath $resolvedOutput -Recurse -Force }
}
& $Dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw '설치판 빌드 실패' }
$vpkVersion = ([xml](Get-Content -LiteralPath $projectPath -Raw)).Project.ItemGroup.PackageReference |
    Where-Object Include -eq 'Velopack' | Select-Object -ExpandProperty Version
$vpkDir = Join-Path $ClipRoot '.tools\vpk'
$vpk = Join-Path $vpkDir 'vpk.exe'
if (-not (Test-Path -LiteralPath $vpk)) {
    & $Dotnet tool install vpk --tool-path $vpkDir --version $vpkVersion
    if ($LASTEXITCODE -ne 0) { throw 'vpk 설치 실패' }
}
$installedTools = & $Dotnet tool list --tool-path $vpkDir
if ($LASTEXITCODE -ne 0 -or -not ($installedTools -match "^vpk\s+$([regex]::Escape($vpkVersion))\s")) { throw 'vpk와 Velopack 패키지 버전을 맞춰 주세요.' }
& $vpk pack --packId Clip --packVersion $Version --packDir $publishDir --mainExe Clip.exe --packTitle Clip --packAuthors shuuuno --runtime win-x64 --outputDir $packages
if ($LASTEXITCODE -ne 0) { throw 'Velopack 패키징 실패' }
Write-Host "로컬 설치판·업데이트 피드: $packages"
if ($Upload) {
    $commit = & git -C $ClipRoot rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw '커밋 조회 실패' }
    # main 병합·push는 이 스크립트가 수행하지 않는다. 업로드는 별도 승인 후 -Upload로 명시한다.
    & gh api "repos/shuuuno/clip/commits/$commit" --silent
    if ($LASTEXITCODE -ne 0) { throw '현재 커밋이 원격 저장소에 없습니다. 승인된 push를 먼저 진행하세요.' }
    $assets = Get-ChildItem -LiteralPath $packages -File | Select-Object -ExpandProperty FullName
    & gh release create "v$Version" @assets --repo shuuuno/clip --target $commit --title "Clip $Version" --notes "Clip $Version 설치판 및 자동 업데이트 패키지"
    if ($LASTEXITCODE -ne 0) { throw 'Release 업로드 실패' }
}
