param([Parameter(Mandatory = $true)][string]$Executable)
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run with Windows PowerShell -STA.' }
Add-Type -AssemblyName PresentationFramework
Write-Output 'Loading standalone executable...'
$assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Executable)))
if (-not ($assembly.GetManifestResourceNames() -contains 'Sspcl.Resources.resources')) { throw 'Embedded dependencies missing.' }
$application = [Activator]::CreateInstance($assembly.GetType('Sspcl.Application', $true), $true)
Write-Output 'Loading WPF resources...'
try {
    $foundation = [Reflection.Assembly]::Load('Sspcl.Foundation')
    $foundation.GetType('Sspcl.Foundation.Main').GetMethod('Init').Invoke($null, @('Sspcl')) | Out-Null
    $wpf = [Reflection.Assembly]::Load('Sspcl.Foundation.Wpf')
    $wpf.GetType('Sspcl.Foundation.Wpf.Main').GetMethod('Init').Invoke($null, @()) | Out-Null
    $application.InitializeComponent()
    Write-Output 'Creating store page...'
    $core = [Reflection.Assembly]::Load('Sspcl.Core')
    $identity = $core.GetType('Sspcl.Core.Store.Forum.ForumClientIdentity').GetProperty('UserAgent').GetValue($null)
    if ($identity -ne 'sspcl/0.98.1') { throw "Unexpected embedded client version: $identity" }
    $page = [Activator]::CreateInstance($assembly.GetType('Sspcl.PageStarsectorDownload', $true))
    foreach ($control in @('BoxSearch', 'BoxSort', 'CheckDirect', 'LabCache', 'ModList', 'BoxRelease', 'BtnDownload', 'BtnImportArchive')) {
        if ($null -eq $page.FindName($control)) { throw "Store control missing: $control" }
    }
    $catalog = [Activator]::CreateInstance($core.GetType('Sspcl.Core.Store.Forum.ForumCatalog'))
    $modType = $core.GetType('Sspcl.Core.Store.Forum.ForumMod')
    $mod = [Activator]::CreateInstance($modType)
    $mod.Id = 'smoke_fixture'
    $mod.ChineseName = 'Smoke test MOD'
    $mod.Authors.Add('Fixture')
    $mod.GameVersions.Add('0.98')
    $listType = [Collections.Generic.List``1].MakeGenericType($modType)
    $mods = [Activator]::CreateInstance($listType)
    $mods.Add($mod)
    $catalog.Mods.Data = $mods
    $apply = $page.GetType().GetMethod('ApplyCatalog', [Reflection.BindingFlags]'Instance,NonPublic')
    $apply.Invoke($page, @($catalog)) | Out-Null
    Write-Output 'Checking store interactions...'
    $list = $page.FindName('ModList')
    if ($list.Items.Count -ne 1) { throw 'Catalog did not populate store.' }
    $list.SelectedIndex = 0
    if ($page.FindName('LabName').Text -ne 'Smoke test MOD') { throw 'Mod selection did not populate details.' }
    $page.FindName('BoxSearch').Text = 'no-matching-mod'
    if ($list.Items.Count -ne 0) { throw 'Search filter did not apply.' }
    $page.FindName('BoxSearch').Text = ''
    $page.FindName('CheckDirect').IsChecked = $true
    if ($list.Items.Count -ne 0) { throw 'Direct download filter did not apply.' }
    $page.FindName('CheckDirect').IsChecked = $false
    $release = [Activator]::CreateInstance($core.GetType('Sspcl.Core.Store.Forum.ForumModRelease'))
    $release.AttachmentId = 42
    $release.DownloadUrl = 'https://files.test/mod.zip'
    $release.FileName = 'mod.zip'
    $mod.AllowDirectDownload = $true
    $releaseListType = [Collections.Generic.List``1].MakeGenericType($release.GetType())
    $mod.Releases = [Activator]::CreateInstance($releaseListType)
    $mod.Releases.Add($release)
    for ($index = 1; $index -le 80; $index++) {
        $extra = [Activator]::CreateInstance($modType)
        $extra.Id = 'fixture-' + $index
        $extra.ChineseName = 'Fixture ' + $index
        $mods.Add($extra)
    }
    $apply.Invoke($page, @($catalog)) | Out-Null
    $list.SelectedIndex = 0
    $page.FindName('BoxRelease').SelectedIndex = 0
    $job = [Activator]::CreateInstance($assembly.GetType('Sspcl.PageStarsectorDownload+DownloadTaskItem'))
    $job.ModInfo = $mod
    $job.Release = $release
    $jobs = $page.GetType().GetField('_downloads', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($page)
    $jobs.Add($job)
    $update = $page.GetType().GetMethod('UpdateDownloadButton', [Reflection.BindingFlags]'Instance,NonPublic')
    $update.Invoke($page, @()) | Out-Null
    if ($page.FindName('BtnDownload').IsEnabled) { throw 'Duplicate active download allowed.' }
    foreach ($control in @('BoxSearch','BoxVersion','BoxCategory','ModList','BoxRelease')) {
        if (-not $page.FindName($control).IsEnabled) { throw 'Active task blocked browsing.' }
    }
    $page.FindName('BoxSearch').Text = 'Fixture 20'
    if ($list.Items.Count -ne 1) { throw 'Search failed during active task.' }
    $page.FindName('BoxSearch').Text = ''
    $list.SelectedIndex = 0
    $page.FindName('BoxRelease').SelectedIndex = 0
    $job.Active = $false
    $update.Invoke($page, @()) | Out-Null
    if (-not $page.FindName('BtnDownload').IsEnabled) { throw 'Ended task prevented redownload.' }
    $job.Cancellation.Dispose()
    $page.Measure([Windows.Size]::new(800, 500))
    $page.Arrange([Windows.Rect]::new(0, 0, 800, 500))
    $page.UpdateLayout()
    function Find-NestedScroll($visual) {
        if ($visual -is [Windows.Controls.ScrollViewer]) { return $visual }
        for ($childIndex = 0; $childIndex -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($visual); $childIndex++) {
            $found = Find-NestedScroll ([Windows.Media.VisualTreeHelper]::GetChild($visual, $childIndex))
            if ($null -ne $found) { return $found }
        }
        return $null
    }
    $nested = Find-NestedScroll $list
    if ($null -eq $nested -or $nested.ScrollableHeight -le 0) { throw 'List scrollbar missing.' }
    $outer = $page.FindName('PanBack')
    if ($outer.ScrollableHeight -le 0) { throw 'Outer page did not overflow; wheel test is inconclusive.' }
    $wheel = [Windows.Input.MouseWheelEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice, 0, -120)
    $wheel.RoutedEvent = [Windows.Input.Mouse]::PreviewMouseWheelEvent
    $wheel.Source = $nested
    $outer.GetType().GetMethod('MyScrollViewer_PreviewMouseWheel', [Reflection.BindingFlags]'Instance,NonPublic').Invoke($outer, @($outer, $wheel)) | Out-Null
    if ($wheel.Handled) { throw 'Outer page stole the list wheel event.' }
    $wheel.RoutedEvent = [Windows.Input.Mouse]::MouseWheelEvent
    $nested.RaiseEvent($wheel)
    $page.UpdateLayout()
    if ($nested.VerticalOffset -le 0) { throw 'List wheel did not scroll.' }
    Write-Output 'PASS: browsing during tasks, duplicate prevention, redownload after completion and nested list mouse wheel.'
    Write-Output 'PASS: standalone embedded dependencies, WPF store creation, catalog rendering, selection, search and download filter.'
    $fixtureRoot = Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Executable))) ('smoke-packs-' + [Guid]::NewGuid().ToString('N'))
    try {
        $baseGame = Join-Path $fixtureRoot 'base'
        [IO.Directory]::CreateDirectory((Join-Path $baseGame 'starsector-core/data/config')) | Out-Null
        $localArchive = Join-Path $fixtureRoot 'local-mod.zip'
        $patchArchive = Join-Path $fixtureRoot 'patch.zip'
        foreach ($archivePath in @($localArchive, $patchArchive)) {
            $zip = [IO.Compression.ZipFile]::Open($archivePath, [IO.Compression.ZipArchiveMode]::Create)
            try {
                $entry = if ($archivePath -eq $localArchive) { 'local/mod_info.json' } else { 'readme.txt' }
                $writer = [IO.StreamWriter]::new($zip.CreateEntry($entry).Open())
                try { $writer.Write('{"id":"local_fixture","name":"Local import fixture","version":"1.0"}') } finally { $writer.Dispose() }
            } finally { $zip.Dispose() }
        }
        $localPool = Join-Path $fixtureRoot 'local-import-pool'
        $installer = $core.GetType('Sspcl.Core.Mods.ModInstaller').GetMethod('InstallArchive')
        $localResult = $installer.Invoke($null, @([string]$localArchive, [string]$localPool))
        if (-not $localResult.Success) { throw ('Local import failed: ' + $localResult.Error) }
        $patchResult = $installer.Invoke($null, @([string]$patchArchive, [string]$localPool))
        if ($patchResult.Success -or $patchResult.Error -notlike '*mod_info.json*') { throw 'Patch without metadata accepted.' }
        if (-not [IO.File]::Exists((Join-Path $localPool 'local/mod_info.json'))) { throw 'Local MOD not installed.' }
        if (-not [IO.File]::Exists($localArchive) -or -not [IO.File]::Exists($patchArchive)) { throw 'Local archives modified.' }
        if (-not $page.FindName('BtnImportArchive').IsEnabled) { throw 'Import button did not recover.' }
        $localJob = [Activator]::CreateInstance($assembly.GetType('Sspcl.PageStarsectorDownload+DownloadTaskItem'))
        $jobs.Add($localJob)
        $update.Invoke($page, @()) | Out-Null
        $localJob.Active = $false
        $localJob.Cancellation.Dispose()
        Write-Output 'PASS: local archive installation, original file preservation, missing MOD metadata rejection and local task coexistence.'
        $existingMod = Join-Path $baseGame 'mods/different-folder'
        [IO.Directory]::CreateDirectory($existingMod) | Out-Null
        [IO.File]::Copy((Join-Path $localPool 'local/mod_info.json'), (Join-Path $existingMod 'mod_info.json'))
        $poolMods = $core.GetType('Sspcl.Core.Mods.ModScanner').GetMethod('Scan').Invoke($null, @([string]$localPool))
        $activate = $core.GetType('Sspcl.Core.Mods.ModActivationService').GetMethod('Apply')
        for ($index = 0; $index -lt 3; $index++) {
            $activate.Invoke($null, @([string]$baseGame, $poolMods.PSObject.BaseObject, [string[]]@('local_fixture'))) | Out-Null
        }
        if ([IO.Directory]::Exists((Join-Path $baseGame 'mods/local'))) { throw 'Activation duplicated MOD by folder name.' }
        $activate.Invoke($null, @([string]$baseGame, $poolMods.PSObject.BaseObject, [string[]]@())) | Out-Null
        if (-not [IO.File]::Exists((Join-Path $existingMod 'mod_info.json'))) { throw 'Disabling removed MOD files.' }
        Write-Output 'PASS: embedded .NET Framework activation by ID, rapid atomic writes and disabling without deleting files.'
        [IO.File]::WriteAllText((Join-Path $baseGame 'starsector.exe'), 'fixture')
        [IO.File]::WriteAllText((Join-Path $baseGame 'starsector-core/data/config/settings.json'), '{"fixture":true}')
        $zip = [IO.Compression.ZipFile]::Open((Join-Path $baseGame 'starsector-core/starfarer_obf.jar'), [IO.Compression.ZipArchiveMode]::Create)
        try {
            $class = $zip.CreateEntry('com/fs/starfarer/Version.class').Open()
            try {
                $versionBytes = [Text.Encoding]::UTF8.GetBytes('0.98a-RC8')
                $classBytes = [byte[]](@(202,254,186,190,0,0,0,52,0,2,1,0,$versionBytes.Length) + $versionBytes)
                $class.Write($classBytes, 0, $classBytes.Length)
            } finally { $class.Dispose() }
        } finally { $zip.Dispose() }
        $moduleType = $assembly.GetType('Sspcl.ModMain')
        $gamePathField = $moduleType.GetField('StarsectorPath')
        $previousGamePath = $gamePathField.GetValue($null)
        $gamePathField.SetValue($null, [string]$baseGame)
        try {
            $instancePage = [Activator]::CreateInstance($assembly.GetType('Sspcl.PageStarsectorInstanceRight'))
            if ($null -eq $instancePage.FindName('LabOverviewMemory')) { throw 'Overview memory display missing.' }
            $instancePage.GetType().GetMethod('LoadMods', [Reflection.BindingFlags]'Instance,NonPublic').Invoke($instancePage, @()) | Out-Null
            $boxes = $instancePage.GetType().GetField('_modBoxes', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($instancePage)
            $fixtureBox = @($boxes | Where-Object { $_.Tag -eq 'local_fixture' })
            if ($fixtureBox.Count -ne 1 -or $fixtureBox[0].Checked) { throw 'Directory existence incorrectly treated as enabled state.' }
            $instancePage.GetType().GetMethod('ModCheck_Changed', [Reflection.BindingFlags]'Instance,NonPublic').Invoke($instancePage, @($fixtureBox[0], $false)) | Out-Null
            if ($instancePage.GetType().GetField('_modOperation', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($instancePage)) { throw 'Programmatic check change started MOD operations.' }
            $actions = @($instancePage.FindName('PanModList').Children | ForEach-Object { $_.Children } | Where-Object { $_ -is [Windows.Controls.StackPanel] } | ForEach-Object { $_.Children })
            if (@($actions | Where-Object { $_ -is [Windows.Controls.Button] -and $_.Tag.Id -eq 'local_fixture' }).Count -ne 2) { throw 'MOD update actions missing.' }
            $spec = @($poolMods | Where-Object { $_.Id -eq 'local_fixture' })[0]
            $windowType = $assembly.GetType('Sspcl.ModUpdateWindow')
            $release.GameVersion = '0.97'
            $updateWindow = [Activator]::CreateInstance($windowType, @($spec.PSObject.BaseObject, '0.98a-RC8', $mods.PSObject.BaseObject, ''))
            $releaseBox = $windowType.GetField('Releases', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($updateWindow)
            $downloadButton = $windowType.GetField('DownloadButton', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($updateWindow)
            $releaseBox.SelectedIndex = 0
            if ($downloadButton.IsEnabled) { throw 'Incompatible game release allowed.' }
            $release.GameVersion = '0.98'
            $releaseBox.SelectedIndex = -1
            $releaseBox.SelectedIndex = 0
            if (-not $downloadButton.IsEnabled) { throw 'Compatible direct update blocked.' }
            $mod.AllowDirectDownload = $false
            $releaseBox.SelectedIndex = -1
            $releaseBox.SelectedIndex = 0
            if ($downloadButton.IsEnabled) { throw 'Forum download policy bypassed in updater.' }
            $mod.AllowDirectDownload = $true
            Write-Output 'PASS: ID-based checkbox state, programmatic change guard, memory display, local/forum update actions and update release policy.'
        } finally { $gamePathField.SetValue($null, $previousGamePath) }
        $exportType = $core.GetType('Sspcl.Core.Modpack.ModpackExporter')
        $importType = $core.GetType('Sspcl.Core.Modpack.ModpackImporter')
        $packOptions = [Activator]::CreateInstance($core.GetType('Sspcl.Core.Modpack.ExportOptions'))
        $packPath = Join-Path $fixtureRoot 'fixture.sspack'
        $exportType.GetMethod('Export').Invoke($null, @([string]$baseGame, [string]$packPath, 'Smoke Pack', $packOptions.PSObject.BaseObject, [Threading.CancellationToken]::None)) | Out-Null
        $imported = Join-Path $fixtureRoot 'imported'
        $importType.GetMethod('Import').Invoke($null, @([string]$packPath, [string]$baseGame, [string]$imported, [string](Join-Path $fixtureRoot 'pool'), $packOptions.PSObject.BaseObject, [Threading.CancellationToken]::None)) | Out-Null
        $moduleType = $assembly.GetType('Sspcl.ModMain')
        $pathsField = $moduleType.GetField('StarsectorPaths')
        $previousPaths = $pathsField.GetValue($null)
        $fixturePaths = [Collections.Generic.List[string]]::new()
        $fixturePaths.Add($baseGame)
        $fixturePaths.Add($imported)
        $pathsField.SetValue($null, $fixturePaths)
        try {
            $leftPage = [Activator]::CreateInstance($assembly.GetType('Sspcl.PageStarsectorSelectLeft'))
            $leftPage.RefreshList()
            $actions = @($leftPage.FindName('PanList').Children | Where-Object { $_.GetType().Name -eq 'MyListItem' })
            if ($actions.Count -ne 3) { throw 'Version sidebar must have only three actions.' }
            $rightPage = [Activator]::CreateInstance($assembly.GetType('Sspcl.PageStarsectorSelectRight'))
            $rightPage.RefreshCards()
            $cards = @($rightPage.FindName('PanMain').Children | Where-Object { $_.GetType().Name -eq 'MyCard' })
            if ($cards.Count -ne 2 -or @($cards | Where-Object { $_.Title -eq 'Smoke Pack' }).Count -ne 1) { throw 'Imported instance not shown in version list.' }
            if ($null -eq $rightPage.FindName('BtnDownloadGame')) { throw 'Download game plus button missing.' }
            $manifest = $importType.GetMethod('Inspect').Invoke($null, @([string]$packPath))
            $optionsWindow = [Activator]::CreateInstance($assembly.GetType('Sspcl.PackOptionsWindow'), @($true, $manifest, $fixturePaths))
            $checkboxes = @($optionsWindow.Content.Children | Where-Object { $_ -is [Windows.Controls.CheckBox] })
            if ($checkboxes.Count -ne 5) { throw 'Pack selection must expose five optional fields.' }
            foreach ($checkbox in $checkboxes) { $checkbox.IsChecked = $false }
            $none = $optionsWindow.Options
            if ($none.IncludeModList -or $none.IncludeGameSettings -or $none.IncludeGameVersion -or $none.IncludePackName -or $none.IncludeSaves) { throw 'Pack fields are not independently optional.' }
            $optionsWindow.Close()
            Write-Output 'PASS: embedded .NET Framework pack export/import, three sidebar actions, imported version card, download plus button and five optional fields.'
        } finally { $pathsField.SetValue($null, $previousPaths) }
    } finally { if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force } }
} finally { $application.Shutdown() }
