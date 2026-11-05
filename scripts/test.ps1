. "$PSScriptRoot\env.ps1"
& $Dotnet run --project "$ClipRoot\tests\Clip.Tests\Clip.Tests.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw '검증 실패' }
