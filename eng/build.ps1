param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [ValidateSet('All', 'Desktop', 'Legacy')][string]$Target = 'All'
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

& (Join-Path $PSScriptRoot 'check-structure.ps1')

Push-Location $root
try {
    & dotnet restore 'Sspcl.sln'
    if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }

    if ($Target -in @('All', 'Desktop')) {
        & dotnet build 'src/Sspcl.Desktop/Sspcl.Desktop.csproj' -c $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
        & dotnet build 'src/Sspcl.Cli/Sspcl.Cli.csproj' -c $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'CLI build failed.' }
    }

    if ($Target -in @('All', 'Legacy')) {
        $msbuild = (Get-Command MSBuild.exe -ErrorAction SilentlyContinue | Select-Object -First 1).Source
        if (-not $msbuild) {
            $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
            if (Test-Path -LiteralPath $vswhere) {
                $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
            }
        }
        if (-not $msbuild) { throw 'Visual Studio MSBuild is required for the .NET Framework 4.8 app.' }
        & $msbuild 'src/Sspcl.App/Sspcl.App.vbproj' -t:Build "-p:Configuration=$Configuration" -v:minimal
        if ($LASTEXITCODE -ne 0) { throw 'Legacy app build failed.' }
    }
    & dotnet run --project 'tests/Sspcl.Core.Forum.Tests/Sspcl.Core.Forum.Tests.csproj' -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Forum contract checks failed.' }
}
finally {
    Pop-Location
}
