. "$PSScriptRoot\env.ps1"
& $Dotnet build "$ClipRoot\src\Clip\Clip.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw '빌드 실패' }
