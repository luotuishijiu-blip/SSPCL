param([string]$Executable,[string]$Output)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework
$assembly=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Executable)))
$application=[Activator]::CreateInstance($assembly.GetType('Sspcl.Application',$true),$true)
$root=Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Output))) ('profiles-smoke-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
try {
 $foundation=[Reflection.Assembly]::Load('Sspcl.Foundation');$foundation.GetType('Sspcl.Foundation.Main').GetMethod('Init').Invoke($null,@('Sspcl'))|Out-Null
 $wpf=[Reflection.Assembly]::Load('Sspcl.Foundation.Wpf');$wpf.GetType('Sspcl.Foundation.Wpf.Main').GetMethod('Init').Invoke($null,@())|Out-Null
 $application.InitializeComponent();$core=[Reflection.Assembly]::Load('Sspcl.Core');$flags=[Reflection.BindingFlags]'Instance,NonPublic'
 $mod=[IO.Path]::Combine($root,'mods','sample');[IO.Directory]::CreateDirectory($mod)|Out-Null
 [IO.File]::WriteAllText((Join-Path $mod 'mod_info.json'),'{"id":"sample","name":"Sample MOD","version":"1.0"}')
 $mods=$core.GetType('Sspcl.Core.Mods.ModScanner').GetMethod('Scan').Invoke($null,@([string](Join-Path $root 'mods')))
 $profileType=$core.GetType('Sspcl.Core.Mods.ModProfile');$profile=[Activator]::CreateInstance($profileType);$profile.Id=[Guid]::NewGuid().ToString('N');$profile.Name='Exploration';$profile.EnabledMods.Add('sample')
 [Console]::WriteLine('Creating profile page')
 $page=[Activator]::CreateInstance($assembly.GetType('Sspcl.PageStarsectorSelectRight'))
 $card=$page.GetType().GetMethod('BuildProfileCard',$flags).Invoke($page,@($profile.PSObject.BaseObject,[string]$root,$mods.PSObject.BaseObject,$true))
 $profile2=[Activator]::CreateInstance($profileType);$profile2.Id=[Guid]::NewGuid().ToString('N');$profile2.Name='Battle';$profile2.EnabledMods.Add('sample');$profile2.EnabledMods.Add('missing-mod')
 $card2=$page.GetType().GetMethod('BuildProfileCard',$flags).Invoke($page,@($profile2.PSObject.BaseObject,[string]$root,$mods.PSObject.BaseObject,$false))
 $null=$card.MainTextBlock;$null=$card2.MainTextBlock
 $stack=$page.FindName('PanMain');$stack.Children.Add($card)|Out-Null;$stack.Children.Add($card2)|Out-Null
 $page.Child.Background=[Windows.Media.Brushes]::WhiteSmoke
 $content=$page;$content.Width=780;$content.Height=550;$content.Measure([Windows.Size]::new(780,550));$content.Arrange([Windows.Rect]::new(0,0,780,550));$content.UpdateLayout()
 $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(780,550,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($content)
 $png=[Windows.Media.Imaging.PngBitmapEncoder]::new();$png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap));$file=[IO.File]::Create([IO.Path]::GetFullPath($Output));try{$png.Save($file)}finally{$file.Dispose()}
 $editor=[Activator]::CreateInstance($assembly.GetType('Sspcl.PageStarsectorInstanceRight'))
 if($null -eq $editor.FindName('BtnSaveMods')){throw 'Save action missing.'}
 $modsField=$editor.GetType().GetField('_mods',$flags);$list=[Activator]::CreateInstance($modsField.FieldType);$list.Add($mods[0].PSObject.BaseObject);$modsField.SetValue($editor,$list)
 $enabledField=$editor.GetType().GetField('_enabled',$flags);$enabled=[Activator]::CreateInstance($enabledField.FieldType);$enabled.Add('sample')|Out-Null;$enabled.Add('uninstalled')|Out-Null;$enabledField.SetValue($editor,$enabled)
 $box=[Activator]::CreateInstance($assembly.GetType('Sspcl.MyCheckBox'));$box.Tag='sample';$box.Checked=$false
 $editor.GetType().GetMethod('ModCheck_Changed',$flags).Invoke($editor,@($box.PSObject.BaseObject,$true))|Out-Null
 $result=$enabledField.GetValue($editor)
 if($result.Contains('sample') -or -not $result.Contains('uninstalled') -or -not $editor.GetType().GetField('_draftDirty',$flags).GetValue($editor)){throw 'Draft selection failed.'}
 if([IO.File]::Exists((Join-Path $root 'mods/enabled_mods.json'))){throw 'Checking MODs wrote an enabled file.'}
 for($i=0;$i -lt 2000;$i++) { $spec=[Activator]::CreateInstance($core.GetType('Sspcl.Core.Mods.ModSpec'));$spec.Id='mod'+$i;$spec.Name='MOD '+$i;$spec.Folder='mods';$spec.Path=$root;$list.Add($spec) }
 $editor.GetType().GetMethod('BindModRows',$flags).Invoke($editor,@())|Out-Null
 $editor.GetType().GetMethod('SelectAllMods',$flags).Invoke($editor,@($null,$null))|Out-Null
 if($enabledField.GetValue($editor).Count -ne 2002){throw 'Select all lost items.'}
 $editor.GetType().GetMethod('ClearAllMods',$flags).Invoke($editor,@($null,$null))|Out-Null
 if($enabledField.GetValue($editor).Count -ne 0){throw 'Clear all did not clear selections.'}
 $editor.FindName('PanMod').Visibility='Visible';$editor.Width=940;$editor.Height=690
 foreach($item in $editor.FindName('PanMod').Children){if($item.GetType().Name -eq 'MyCard'){$null=$item.MainTextBlock}}
 $editor.Measure([Windows.Size]::new(940,690));$editor.Arrange([Windows.Rect]::new(0,0,940,690));$editor.UpdateLayout()
 $view=$editor.FindName('PanModList');$realized=0
 for($i=0;$i -lt $view.Items.Count;$i++){if($null -ne $view.ItemContainerGenerator.ContainerFromIndex($i)){$realized++}}
 if($realized -gt 80 -or $realized -eq 0){throw "MOD list not virtualized: $realized"}
 $watch=[Diagnostics.Stopwatch]::StartNew();$rows=$editor.GetType().GetField('_modRows',$flags).GetValue($editor)
 for($i=0;$i -lt 100;$i++){$rows[0].IsChecked=-not $rows[0].IsChecked}
 $watch.Stop()
 if($watch.ElapsedMilliseconds -gt 2500){throw 'Large-list toggles too slow.'}
 if($editor.FindName('BtnSaveMods').ActualWidth -lt 80){throw 'Visible save button missing.'}
 $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(940,690,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($editor)
 $png=[Windows.Media.Imaging.PngBitmapEncoder]::new();$png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap));$file=[IO.File]::Create([IO.Path]::ChangeExtension($Output,'-mods.png'));try{$png.Save($file)}finally{$file.Dispose()}
 Write-Output "PASS: 2002 MOD rows, $realized realized containers; 100 toggles $($watch.ElapsedMilliseconds) ms; select/clear all and visible save."
 Write-Output 'PASS: real profile cards, missing-MOD display, saved-list editing action, explicit save control and draft-only checkbox changes.'
} finally { $application.Shutdown();[IO.Directory]::Delete($root,$true) }
