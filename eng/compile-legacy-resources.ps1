param(
    [Parameter(Mandatory = $true)][string]$ProjectDirectory,
    [Parameter(Mandatory = $true)][string]$OutputFile
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($ProjectDirectory)
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '..')) + [IO.Path]::DirectorySeparatorChar
$resxPath = Join-Path $projectRoot 'My Project/Resources.resx'
$outputPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputFile))
if (-not $outputPath.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resource output must stay inside the application project.'
}

# Read declarative XML only. Never instantiate types or deserialize objects from the resx.
$readerSettings = New-Object System.Xml.XmlReaderSettings
$readerSettings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
$readerSettings.XmlResolver = $null
$reader = [System.Xml.XmlReader]::Create($resxPath, $readerSettings)
try {
    $document = New-Object System.Xml.XmlDocument
    $document.XmlResolver = $null
    $document.Load($reader)
} finally { $reader.Dispose() }

$inputs = @()
foreach ($entry in $document.DocumentElement.SelectNodes('data')) {
    if ($entry.HasAttribute('mimetype') -or $entry.GetAttribute('type') -ne 'System.Resources.ResXFileRef, System.Windows.Forms') {
        throw "Unsupported resource type: $($entry.GetAttribute('name')). Only byte file references are allowed."
    }
    $parts = $entry.SelectSingleNode('value').InnerText.Split(';')
    if ($parts.Count -ne 2 -or -not $parts[1].StartsWith('System.Byte[], mscorlib,')) {
        throw 'Only byte array resources are supported.'
    }
    $inputPath = [IO.Path]::GetFullPath((Join-Path (Split-Path $resxPath) $parts[0]))
    if (-not $inputPath.StartsWith($sourceRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Resource reference leaves the source tree: $inputPath"
    }
    $inputs += [PSCustomObject]@{ Name = $entry.GetAttribute('name'); Path = $inputPath }
}

$dependencies = @($resxPath, $PSCommandPath) + @($inputs | ForEach-Object { $_.Path })
$newestInput = ($dependencies | ForEach-Object { (Get-Item -LiteralPath $_).LastWriteTimeUtc } | Sort-Object -Descending | Select-Object -First 1)
if ((Test-Path -LiteralPath $outputPath) -and (Get-Item -LiteralPath $outputPath).LastWriteTimeUtc -ge $newestInput) { exit 0 }
[IO.Directory]::CreateDirectory((Split-Path $outputPath)) | Out-Null
$temporaryPath = $outputPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
$backupPath = $temporaryPath + '.bak'
try {
    $writer = New-Object System.Resources.ResourceWriter($temporaryPath)
    try {
        foreach ($resourceInput in $inputs) {
            $writer.AddResource([string]$resourceInput.Name, [byte[]][IO.File]::ReadAllBytes($resourceInput.Path))
        }
        $writer.Generate()
    } finally { $writer.Dispose() }
    if (Test-Path -LiteralPath $outputPath) { [IO.File]::Replace($temporaryPath, $outputPath, $backupPath) }
    else { [IO.File]::Move($temporaryPath, $outputPath) }
} finally {
    if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath }
    if (Test-Path -LiteralPath $backupPath) { Remove-Item -LiteralPath $backupPath }
}
Write-Output "Generated $($inputs.Count) byte resources."
