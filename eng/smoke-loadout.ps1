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
    foreach ($name in @('HullList','SlotList','WeaponList','ShipCanvas','BtnGenerate','BtnAi','BtnExport','BtnUndo','BoxVents','CheckPin','BtnToggleHulls','HullSplitter','HullModList','LabShieldDps','LabArmorDps','LabHullDps','BoxWeaponDescription','BoxProjectileSpec')) {
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
    $damage = $core.GetType('Sspcl.Core.Loadouts.LoadoutDetails').GetMethod('Damage').Invoke($null, @($hull.PSObject.BaseObject, $catalog.Weapons.PSObject.BaseObject, $plan.PSObject.BaseObject))
    foreach ($pair in @(@('LabShieldDps','ShieldDps'),@('LabArmorDps','ArmorDps'),@('LabHullDps','HullDps'))) {
        $expected = $core.GetType('Sspcl.Core.Loadouts.LoadoutDetails').GetMethod('Number').Invoke($null, @([double]$damage.($pair[1])))
        if ($window.FindName($pair[0]).Text -ne $expected) { throw 'Damage labels differ from non-missile calculation.' }
    }
    if ($window.FindName('BoxWeaponDescription').Text.Length -lt 30 -or $window.FindName('WeaponParameterList').Items.Count -lt 10 -or $window.FindName('BoxProjectileSpec').Text.Length -lt 30) { throw 'Full weapon description/parameters missing.' }
    if ($window.FindName('HullParameterList').Items.Count -lt 20 -or $hull.Stats['armor rating'] -eq '' -or $hull.Stats['shield efficiency'] -eq '') { throw 'Detailed hull stats missing.' }
    $window.FindName('DetailTabs').SelectedIndex = 2
    $armor = $catalog.HullMods | Where-Object Id -eq 'heavyarmor' | Select-Object -First 1
    $window.FindName('HullModList').SelectedItem = $armor
    $window.FindName('BtnHullModNormal').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $plan = $window.GetType().GetField('_plan', $flags).GetValue($window)
    if (-not $plan.HullMods.Contains('heavyarmor')) { throw 'Normal hullmod installation failed.' }
    $window.FindName('BtnHullModBuiltIn').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $plan = $window.GetType().GetField('_plan', $flags).GetValue($window)
    if ($plan.HullMods.Contains('heavyarmor') -or -not $plan.SMods.Contains('heavyarmor') -or -not $plan.PermaMods.Contains('heavyarmor')) { throw 'Normal to builtin conversion failed.' }
    foreach ($mod in @($catalog.HullMods | Where-Object { $_.Id -ne 'heavyarmor' -and -not $hull.BuiltInHullMods.Contains($_.Id) } | Select-Object -First 7)) {
        $window.FindName('HullModList').SelectedItem = $mod
        $window.FindName('BtnHullModBuiltIn').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    }
    $plan = $window.GetType().GetField('_plan', $flags).GetValue($window)
    if ($plan.SMods.Count -ne 8 -or -not $window.FindName('BtnExport').IsEnabled) { throw 'Built-in upper limit imposed.' }
    $window.FindName('BtnUndo').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $window.FindName('BtnRedo').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $plan = $window.GetType().GetField('_plan', $flags).GetValue($window)
    if ($plan.SMods.Count -ne 8) { throw 'Hullmod undo/redo failed.' }
    # Remove the arbitrary seven test mods; render a useful example with heavy armor only.
    foreach ($modId in @($window.FindName('InstalledHullModList').Items | Where-Object { $_.Tag -ne 'heavyarmor' -and -not $hull.BuiltInHullMods.Contains([string]$_.Tag) } | ForEach-Object { [string]$_.Tag })) {
        $row = $window.FindName('InstalledHullModList').Items | Where-Object Tag -eq $modId | Select-Object -First 1
        $window.FindName('InstalledHullModList').SelectedItem = $row
        $window.FindName('BtnHullModRemove').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    }
    $plan = $window.GetType().GetField('_plan', $flags).GetValue($window)
    if ($plan.SMods.Count -ne 1) { throw 'Removing hullmods failed.' }
    $content = $window.Content
    $content.Width = 1336; $content.Height = 870
    $content.Measure([Windows.Size]::new(1380,914))
    $content.Arrange([Windows.Rect]::new(0,0,1380,914))
    $content.UpdateLayout()
    if ($window.FindName('HullSidebar').Visibility -ne 'Visible') { $window.FindName('BtnToggleHulls').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent)); $content.UpdateLayout() }
    $oldWidth = $window.FindName('HullColumn').ActualWidth
    $splitter = $window.FindName('HullSplitter')
    $start = [Windows.Controls.Primitives.DragStartedEventArgs]::new(0,0); $start.RoutedEvent = [Windows.Controls.Primitives.Thumb]::DragStartedEvent
    $delta = [Windows.Controls.Primitives.DragDeltaEventArgs]::new(45,0); $delta.RoutedEvent = [Windows.Controls.Primitives.Thumb]::DragDeltaEvent
    $end = [Windows.Controls.Primitives.DragCompletedEventArgs]::new(45,0,$false); $end.RoutedEvent = [Windows.Controls.Primitives.Thumb]::DragCompletedEvent
    $splitter.RaiseEvent($start); $splitter.RaiseEvent($delta); $content.UpdateLayout(); $splitter.RaiseEvent($end)
    if ($window.FindName('HullColumn').ActualWidth -le $oldWidth + 10) { throw 'Directory drag resizing failed.' }
    $previewWidth = $window.FindName('PreviewHost').ActualWidth
    $window.FindName('BtnToggleHulls').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent)); $content.UpdateLayout()
    if ($window.FindName('HullColumn').ActualWidth -ne 0 -or $window.FindName('PreviewHost').ActualWidth -le $previewWidth) { throw 'Collapsing directory did not free preview space.' }
    $window.FindName('BtnToggleHulls').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent)); $content.UpdateLayout()
    if ($window.FindName('HullColumn').ActualWidth -le 170) { throw 'Directory resize width not restored.' }
    $window.FindName('BtnToggleHulls').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $window.FindName('DetailTabs').SelectedIndex = 0
    $content.UpdateLayout()
    $hostPanel = $window.FindName('PreviewHost')
    $workspace = $window.FindName('Workspace')
    if ($hostPanel.ActualHeight -lt 750 -or $hostPanel.ActualWidth -ne $workspace.ActualWidth -or $hostPanel.ActualHeight -ne $workspace.ActualHeight) { throw 'Canvas does not fill workspace.' }
    if ($hostPanel.Background.Color.A -ne 0) { throw 'Canvas background is not transparent.' }
    $window.FindName('BtnFit').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $area = $window.GetType().GetMethod('VisibleShipArea', $flags).Invoke($window,@())
    $bounds = $window.GetType().GetField('_drawingBounds', $flags).GetValue($window)
    $zoom = $window.GetType().GetField('_zoom', $flags).GetValue($window)
    $pan = $window.GetType().GetField('_pan', $flags).GetValue($window)
    if ($bounds.Left * $zoom.ScaleX + $pan.X -lt $area.Left - 0.1 -or $bounds.Right * $zoom.ScaleX + $pan.X -gt $area.Right + 0.1 -or $bounds.Top * $zoom.ScaleY + $pan.Y -lt $area.Top - 0.1 -or $bounds.Bottom * $zoom.ScaleY + $pan.Y -gt $area.Bottom + 0.1) { throw 'Fit clips ship/weapons or overlaps floating panels.' }
    $window.GetType().GetField('_manualView', $flags).SetValue($window,$true)
    $savedScale = $zoom.ScaleX; $savedPanX = $pan.X; $savedPanY = $pan.Y
    $window.FindName('BtnUndo').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $window.FindName('BtnRedo').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    if ($zoom.ScaleX -ne $savedScale -or $pan.X -ne $savedPanX -or $pan.Y -ne $savedPanY) { throw 'Editing resets manual camera.' }
    foreach ($button in @('BtnToggleData','BtnToggleFitting')) { $window.FindName($button).RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent)) }
    $content.UpdateLayout()
    if ($window.FindName('DataBody').Visibility -ne 'Collapsed' -or $window.FindName('DetailTabs').Visibility -ne 'Collapsed' -or $window.FindName('OverlayRail').Width -ne 180) { throw 'Floating panels do not collapse compactly.' }
    $window.GetType().GetMethod('SelectSlot', $flags).Invoke($window,@($hull.Slots[0].PSObject.BaseObject)) | Out-Null
    if ($window.FindName('DetailTabs').Visibility -ne 'Visible' -or $window.FindName('DetailTabs').SelectedIndex -ne 0) { throw 'Selecting slot does not open weapons.' }
    $window.FindName('BtnToggleData').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent)); $content.UpdateLayout()
    $window.FindName('BtnFit').RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $content.UpdateLayout()
    $savedScale = $zoom.ScaleX
    $wheel = [Windows.Input.MouseWheelEventArgs]::new([Windows.Input.Mouse]::PrimaryDevice,0,120)
    $wheel.RoutedEvent = [Windows.UIElement]::PreviewMouseWheelEvent
    $window.FindName('WeaponList').RaiseEvent($wheel)
    if ($zoom.ScaleX -ne $savedScale) { throw 'Panel wheel zooms canvas.' }
    # Disconnected offscreen trees need explicit arrangement of children added by undo/redo.
    $canvas.InvalidateMeasure()
    $canvas.Measure([Windows.Size]::new($canvas.Width,$canvas.Height))
    $canvas.Arrange([Windows.Rect]::new(0,0,$canvas.Width,$canvas.Height))
    foreach ($slotMarker in @($canvas.Children | Where-Object { $_ -is [Windows.Shapes.Ellipse] })) {
        if ($slotMarker.ActualWidth -lt 12 -or $slotMarker.RenderedGeometry.Bounds.Width -le 0) { throw 'Slot hit target lost after editing.' }
    }
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(1380,914,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($content)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $outputPath = [IO.Path]::GetFullPath($Output)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputPath)) | Out-Null
    $stream = [IO.File]::Create($outputPath)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
    foreach ($tab in @(1,2)) {
        $window.FindName('DetailTabs').SelectedIndex = $tab; $content.UpdateLayout()
        $detailBitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(1380,914,96,96,[Windows.Media.PixelFormats]::Pbgra32); $detailBitmap.Render($content)
        $detailEncoder = [Windows.Media.Imaging.PngBitmapEncoder]::new(); $detailEncoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($detailBitmap))
        $detailPath = [IO.Path]::Combine([IO.Path]::GetDirectoryName($outputPath), [IO.Path]::GetFileNameWithoutExtension($outputPath) + '-tab' + $tab + '.png')
        $detailStream = [IO.File]::Create($detailPath); try { $detailEncoder.Save($detailStream) } finally { $detailStream.Dispose() }
    }
    $window.FindName('HullDetailsExpander').IsExpanded = $true
    $content.UpdateLayout()
    if ($window.FindName('HullParameterList').ActualHeight -le 0) { throw 'Upper hull details inaccessible.' }
    $window.FindName('DetailTabs').SelectedIndex = 0
    foreach ($dimensions in @(@(1080,720),@(1280,800))) {
        $content.Width = $dimensions[0]; $content.Height = $dimensions[1]
        $content.Measure([Windows.Size]::new($dimensions[0],$dimensions[1])); $content.Arrange([Windows.Rect]::new(0,0,$dimensions[0],$dimensions[1])); $content.UpdateLayout()
        if ($window.FindName('DataPanel').ActualHeight + $window.FindName('FittingPanel').ActualHeight + 10 -gt $window.FindName('PreviewHost').ActualHeight) { throw 'Floating panels overlap at compact dimensions.' }
        if ($window.FindName('WeaponList').ActualHeight -lt 40) { throw 'Weapon list unusable at compact dimensions.' }
    }
    $variant = $core.GetType('Sspcl.Core.Loadouts.LoadoutVariant').GetMethod('Serialize').Invoke($null, @($hull.PSObject.BaseObject, $catalog.Weapons.PSObject.BaseObject, $plan.PSObject.BaseObject, 'onslaught_sspcl_smoke', $catalog.HullMods.PSObject.BaseObject))
    [IO.File]::WriteAllText([IO.Path]::ChangeExtension($outputPath, '.variant'), $variant, [Text.UTF8Encoding]::new($false))
    Write-Output "PASS: embedded workbench, actual hull/weapon assets, stable coordinates, manual install, pins, undo/redo and export. Preview: $outputPath"
    Write-Output 'PASS: directory drag/collapse/restore, ordinary and unlimited built-in hullmods, full hull/weapon data, projectile spec and non-missile damage labels.'
    Write-Output 'PASS: full-height transparent canvas, unobstructed fit, floating panel collapse, slot reopen, manual camera preservation and compact layouts.'
    $window.Close()
} finally { $application.Shutdown() }
