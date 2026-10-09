$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solution = Join-Path $root 'Sspcl.sln'
$appProject = Join-Path $root 'src/Sspcl.App/Sspcl.App.vbproj'

foreach ($match in [regex]::Matches([IO.File]::ReadAllText($solution), 'Project\("\{[^}]+\}"\) = "[^"]+", "([^"]+\.(?:csproj|vbproj))"')) {
    $path = Join-Path $root ($match.Groups[1].Value -replace '\\', '/')
    if (-not [IO.File]::Exists($path)) { throw "Solution project missing: $path" }
}

[xml]$project = Get-Content -LiteralPath $appProject -Raw
$projectDirectory = Split-Path -Parent $appProject
$items = @($project.Project.ItemGroup | ForEach-Object { $_.Compile; $_.Page; $_.ApplicationDefinition }) |
    Where-Object { $_ -and $_.Include }
$included = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($item in $items) {
    $path = [IO.Path]::GetFullPath((Join-Path $projectDirectory $item.Include))
    if (-not [IO.File]::Exists($path)) { throw "Legacy project item missing: $path" }
    [void]$included.Add($path)
}

Get-ChildItem -LiteralPath $projectDirectory -Recurse -File |
    Where-Object { $_.Extension -in @('.vb', '.xaml') -and
                   $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]' -and
                   $_.FullName -notmatch '[\\/]Compatibility[\\/]Uncompiled[\\/]' -and
                   $_.Name -ne 'Custom.xaml' } |
    ForEach-Object {
        if (-not $included.Contains($_.FullName)) { throw "Legacy source is not included in project: $($_.FullName)" }
        if ($_.Name -match '\.xaml\.vb$') {
            $xaml = $_.FullName.Substring(0, $_.FullName.Length - 3)
            if (-not [IO.File]::Exists($xaml)) { throw "XAML partner missing: $xaml" }
        }
    }

$references = @{
    'Sspcl.Foundation' = @()
    'Sspcl.Foundation.Wpf' = @('Sspcl.Foundation')
    'Sspcl.Java' = @('Sspcl.Foundation', 'Sspcl.Foundation.Wpf')
    'Sspcl.Core' = @()
    'Sspcl.Cli' = @('Sspcl.Core')
    'Sspcl.Desktop' = @('Sspcl.Core')
    'Sspcl.App' = @('Sspcl.Foundation', 'Sspcl.Foundation.Wpf', 'Sspcl.Java')
    'Sspcl.Core.Forum.Tests' = @('Sspcl.Core')
}
foreach ($name in $references.Keys) {
    $path = if ($name -eq 'Sspcl.App') { $appProject } elseif ($name -eq 'Sspcl.Core.Forum.Tests') { Join-Path $root "tests/$name/$name.csproj" } else { Join-Path $root "src/$name/$name.csproj" }
    [xml]$xml = Get-Content -LiteralPath $path -Raw
    $actual = @($xml.Project.ItemGroup.ProjectReference | Where-Object { $_ -and $_.Include } |
        ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.Include) } | Sort-Object)
    $expected = @($references[$name] | Sort-Object)
    if (($actual -join ',') -ne ($expected -join ',')) {
        throw "$name dependency boundary changed: expected $($expected -join ','), got $($actual -join ',')"
    }
}

Write-Host 'Structure and dependency boundaries are valid.'
