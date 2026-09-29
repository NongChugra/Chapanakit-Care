$ErrorActionPreference = 'Stop'
$taskDotnet = Join-Path $env:USERPROFILE '.dotnet/dotnet.exe'
$taskEvidence = Join-Path $PWD 'docs/assurance/report-completion'
& $taskDotnet restore ChapanakitCare.sln --locked-mode --source "$env:USERPROFILE/.nuget/packages" -m:1 *> "$taskEvidence/candidate-restore.log"
if ($LASTEXITCODE -ne 0) { Get-Content "$taskEvidence/candidate-restore.log" -Tail 30; exit $LASTEXITCODE }
Write-Output 'Locked restore passed.'
$taskChangedCode = Get-Content "$taskEvidence/format-scope.txt"
foreach ($taskFormatFolder in @('src','tests')) {
    $taskFormatFiles = @($taskChangedCode | Where-Object { $_.StartsWith($taskFormatFolder + '/') } | ForEach-Object { Join-Path $PWD $_ })
    & $taskDotnet format whitespace $taskFormatFolder --folder --include $taskFormatFiles --verify-no-changes --verbosity quiet *> "$taskEvidence/candidate-format-$taskFormatFolder.log"
    if ($LASTEXITCODE -ne 0) { Get-Content "$taskEvidence/candidate-format-$taskFormatFolder.log" -Tail 30; exit $LASTEXITCODE }
}
Write-Output 'Changed-file formatting passed (folder workspaces avoid sandbox named-pipe restrictions).'
& $taskDotnet build ChapanakitCare.sln --no-restore --configuration Release -m:1 *> "$taskEvidence/candidate-build.log"
if ($LASTEXITCODE -ne 0) { Get-Content "$taskEvidence/candidate-build.log" -Tail 40; exit $LASTEXITCODE }
Write-Output 'Release solution build passed.'
& $taskDotnet test ChapanakitCare.sln --no-build --no-restore --configuration Release -m:1 --logger 'console;verbosity=minimal' *> "$taskEvidence/candidate-tests.log"
if ($LASTEXITCODE -ne 0) { Get-Content "$taskEvidence/candidate-tests.log" -Tail 45; exit $LASTEXITCODE }
Get-Content "$taskEvidence/candidate-tests.log" | Select-String 'Passed!'
& $taskDotnet publish src/ChapanakitCare.Web/ChapanakitCare.Web.csproj --no-build --no-restore --configuration Release -o tmp/report-published -m:1 *> "$taskEvidence/candidate-publish.log"
if ($LASTEXITCODE -ne 0) { Get-Content "$taskEvidence/candidate-publish.log" -Tail 30; exit $LASTEXITCODE }
Write-Output 'Isolated web publication passed.'
