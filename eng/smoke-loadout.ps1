param([Parameter(Mandatory=$true)][string]$Executable, [string]$GamePath = 'D:/Starsector', [string]$Output = 'release/loadout-preview.png')
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run with Windows PowerShell -STA.' }
Add-Type -AssemblyName PresentationFramework
$assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Executable)))
$application = [Activator]::CreateInstance($assembly.GetType('Sspcl.Application', $true), $true)
try {
    $foundation = [Reflection.Assembly]::Load('Sspcl.Foundation')
    $foundation.GetType('Sspcl.Foundation.Main').GetMethod('Init').Invoke($null, @('Sspcl')) | Out-Null
    $wpf = [Reflection.Assembly]::Load('Sspcl.Foundation.Wpf')
    $wpf.GetType('Sspcl.Foundation.Wpf.Main').GetMethod('Init').Invoke($null, @()) | Out-Null
    $application.InitializeComponent()
    $core = [Reflection.Assembly]::Load('Sspcl.Core')
    $catalog = $core.GetType('Sspcl.Core.Loadouts.LoadoutCatalogReader').GetMethod('Read').Invoke($null, @($GamePath, [Threading.CancellationToken]::None))
    $window = [Activator]::CreateInstance($assembly.GetType('Sspcl.LoadoutWorkbench', $true), @($GamePath))
    $flags = [Reflection.BindingFlags]'Instance,NonPublic'
    $window.GetType().GetMethod('ApplyCatalog', $flags).Invoke($window, @($catalog)) | Out-Null
    foreach ($name in @('HullList','SlotList','WeaponList','ShipCanvas','BtnGenerate','BtnAi','BtnExport','BtnUndo','BoxVents','CheckPin')) {
        if ($null -eq $window.FindName($name)) { throw "Missing loadout control: $name" }
    }
    $hull = $catalog.Hulls | Where-Object Id -eq 'onslaught' | Select-Object -First 1
    $window.FindName('HullList').SelectedItem = $hull
    $window.FindName('BtnGenerate').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $canvas = $window.FindName('ShipCanvas')
    $marker = $canvas.Children | Where-Object Tag -eq 'WS 001' | Select-Object -First 1
    if ($null -eq $marker) { throw 'Slot marker not rendered.' }
    if ([Math]::Abs(([Windows.Controls.Canvas]::GetLeft($marker) + $marker.Width / 2) - (73 + 56)) -gt 0.01) { throw 'Slot X drift.' }
    if ([Math]::Abs(([Windows.Controls.Canvas]::GetTop($marker) + $marker.Height / 2) - (86 + 56)) -gt 0.01) { throw 'Slot Y drift.' }
    $click = [Windows.Input.MouseButtonEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice, 0, [Windows.Input.MouseButton]::Left)
    $click.RoutedEvent = [Windows.UIElement]::MouseLeftButtonDownEvent
    $marker.RaiseEvent($click)
    if ($window.FindName('SlotList').SelectedItem.Tag.Id -ne 'WS 001') { throw 'Visual marker selects incorrect slot.' }
    $layers = @($canvas.Children | Where-Object { $_.Tag -like 'weapon:*' })
    if ($layers.Count -lt 3) { throw 'Weapon layers not rendered.' }
    foreach ($layer in $layers) {
        $slotId = $layer.Tag.Substring(7)
        $slot = $hull.Slots | Where-Object Id -eq $slotId | Select-Object -First 1
        $pivotY = $layer.Height * $(if ($slot.Mount -eq 'HARDPOINT') { 0.75 } else { 0.5 })
        if ([Math]::Abs(([Windows.Controls.Canvas]::GetLeft($layer) + $layer.Width / 2) - ($hull.CenterX - $slot.Y + 56)) -gt 0.01 -or
            [Math]::Abs(([Windows.Controls.Canvas]::GetTop($layer) + $pivotY) - ($hull.Height - $hull.CenterY - $slot.X + 56)) -gt 0.01 -or
            $layer.RenderTransform.Angle -ne -$slot.Angle) { throw "Weapon layer misaligned: $slotId" }
    }
    if (-not $window.FindName('BtnExport').IsEnabled) { throw 'Valid loadout cannot export.' }
    $rows = $window.FindName('SlotList')
    $editable = $rows.Items | Where-Object { $_.Tag.Id -eq 'WS 001' } | Select-Object -First 1
    $rows.SelectedItem = $editable
    $weapons = $window.FindName('WeaponList')
    if ($weapons.Items.Count -eq 0) { throw 'Compatible list empty.' }
    $weapons.SelectedItem = $weapons.Items | Where-Object Id -eq 'lightmg' | Select-Object -First 1
    $window.FindName('BtnInstall').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $plan = $window.GetType().GetField('_plan', $flags).GetValue($window)
    if ($plan.Weapons['WS 001'] -ne 'lightmg' -or -not $plan.PinnedSlots.Contains('WS 001')) { throw 'Manual installation/pin failed.' }
    $window.FindName('BtnUndo').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $window.FindName('BtnRedo').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $plan = $window.GetType().GetField('_plan', $flags).GetValue($window)
    if ($plan.Weapons['WS 001'] -ne 'lightmg') { throw 'Undo/redo failed.' }
    $window.FindName('BtnGenerate').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $plan = $window.GetType().GetField('_plan', $flags).GetValue($window)
    if ($plan.Weapons['WS 001'] -ne 'lightmg') { throw 'Generator replaced pinned slot.' }
    $content = $window.Content
    $content.Width = 1276; $content.Height = 790
    $content.Measure([Windows.Size]::new(1320,834))
    $content.Arrange([Windows.Rect]::new(0,0,1320,834))
    $content.UpdateLayout()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(1320,834,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($content)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $outputPath = [IO.Path]::GetFullPath($Output)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputPath)) | Out-Null
    $stream = [IO.File]::Create($outputPath)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
    $variant = $core.GetType('Sspcl.Core.Loadouts.LoadoutVariant').GetMethod('Serialize').Invoke($null, @($hull.PSObject.BaseObject, $catalog.Weapons.PSObject.BaseObject, $plan.PSObject.BaseObject, 'onslaught_sspcl_smoke'))
    [IO.File]::WriteAllText([IO.Path]::ChangeExtension($outputPath, '.variant'), $variant, [Text.UTF8Encoding]::new($false))
    Write-Output "PASS: embedded workbench, actual hull/weapon assets, stable coordinates, manual install, pins, undo/redo and export. Preview: $outputPath"
    $window.Close()
} finally { $application.Shutdown() }
