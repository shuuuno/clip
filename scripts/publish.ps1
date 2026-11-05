. "$PSScriptRoot\env.ps1"
& $Dotnet publish "$ClipRoot\src\Clip\Clip.csproj" -c Release -r win-x64 --self-contained true -o "$ClipRoot\artifacts\Clip-win-x64"
if ($LASTEXITCODE -ne 0) { throw '배포 빌드 실패' }
Copy-Item "$ClipRoot\README.md" "$ClipRoot\artifacts\Clip-win-x64\README.md"
New-Item -ItemType Directory -Force -Path "$ClipRoot\artifacts\Clip-win-x64\docs" | Out-Null
Copy-Item "$ClipRoot\docs\acceptance.md" "$ClipRoot\artifacts\Clip-win-x64\docs\acceptance.md"
Copy-Item "$ClipRoot\docs\sharing.md" "$ClipRoot\artifacts\Clip-win-x64\docs\sharing.md"
Compress-Archive -Path "$ClipRoot\artifacts\Clip-win-x64\*" -DestinationPath "$ClipRoot\artifacts\Clip-win-x64.zip" -Force
$project = [xml](Get-Content "$ClipRoot\src\Clip\Clip.csproj" -Raw)
$version = $project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
Copy-Item "$ClipRoot\artifacts\Clip-win-x64.zip" "$ClipRoot\artifacts\Clip-$version-win-x64.zip" -Force
