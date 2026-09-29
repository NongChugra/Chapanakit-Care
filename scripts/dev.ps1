[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet("web", "desktop", "test", "publish", "clean")]
    [string] $Command = "web"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "ChapanakitCare.sln"
$webProject = Join-Path $repoRoot "src\ChapanakitCare.Web\ChapanakitCare.Web.csproj"
$desktopProject = Join-Path $repoRoot "src\ChapanakitCare.Desktop\ChapanakitCare.Desktop.csproj"

function Resolve-ProjectDotNet {
    $sdkVersion = (Get-Content -LiteralPath (Join-Path $repoRoot "global.json") -Raw | ConvertFrom-Json).sdk.version
    $candidates = [System.Collections.Generic.List[string]]::new()

    if ($env:DOTNET_ROOT) {
        $candidates.Add((Join-Path $env:DOTNET_ROOT "dotnet.exe"))
    }

    if ($env:USERPROFILE) {
        $candidates.Add((Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"))
    }

    $pathDotNet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($pathDotNet) {
        $candidates.Add($pathDotNet.Source)
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            continue
        }

        $installedSdks = & $candidate --list-sdks 2>$null
        if ($installedSdks | Where-Object { $_ -like "$sdkVersion *" }) {
            return $candidate
        }
    }

    throw "The .NET SDK $sdkVersion is required. Install it globally or in $env:USERPROFILE\.dotnet."
}

function Invoke-CheckedDotNet {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    & $dotnet @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $exitCode."
    }
}

function Assert-RepositoryPath {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPrefix = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the repository: $fullPath"
    }

    return $fullPath
}

function Remove-GeneratedPath {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $fullPath = Assert-RepositoryPath -Path $Path
    if ((Test-Path -LiteralPath $fullPath) -and $PSCmdlet.ShouldProcess($fullPath, "Remove generated path")) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

Set-Location -LiteralPath $repoRoot

if ($Command -eq "clean") {
    $generatedRoots = @(
        (Join-Path $repoRoot "artifacts"),
        (Join-Path $repoRoot "App_Data"),
        (Join-Path $repoRoot "output"),
        (Join-Path $repoRoot "tmp")
    )

    foreach ($generatedRoot in $generatedRoots) {
        Remove-GeneratedPath -Path $generatedRoot
    }

    Get-ChildItem -LiteralPath (Join-Path $repoRoot "src"), (Join-Path $repoRoot "tests") -Directory -Recurse -Force |
        Where-Object { $_.Name -in @("bin", "obj", "TestResults", "__pycache__") } |
        Sort-Object FullName -Descending |
        ForEach-Object { Remove-GeneratedPath -Path $_.FullName }

    Get-ChildItem -LiteralPath $repoRoot -File -Force |
        Where-Object { $_.Name -match "\.(stdout|stderr)\.log$" -or $_.Name -in @("build.log", "publish.log") } |
        ForEach-Object { Remove-GeneratedPath -Path $_.FullName }

    if ($WhatIfPreference) {
        Write-Host "Clean preview completed; no files were removed."
    }
    else {
        Write-Host "Generated project files cleaned."
    }
    exit 0
}

$dotnet = Resolve-ProjectDotNet

switch ($Command) {
    "web" {
        Invoke-CheckedDotNet -Arguments @("watch", "--project", $webProject, "run")
    }
    "desktop" {
        Invoke-CheckedDotNet -Arguments @("run", "--project", $desktopProject)
    }
    "test" {
        Invoke-CheckedDotNet -Arguments @("restore", $solution, "--locked-mode")
        Invoke-CheckedDotNet -Arguments @("build", $solution, "--no-restore", "--configuration", "Release")
        Invoke-CheckedDotNet -Arguments @("test", $solution, "--no-build", "--configuration", "Release")
    }
    "publish" {
        $publishDirectory = Join-Path $repoRoot "artifacts\publish\development"
        Invoke-CheckedDotNet -Arguments @("restore", $solution, "--locked-mode")
        Invoke-CheckedDotNet -Arguments @(
            "publish",
            $desktopProject,
            "--configuration", "Release",
            "--no-restore",
            "--output", $publishDirectory
        )
        Write-Host "Published application: $publishDirectory"
    }
}
