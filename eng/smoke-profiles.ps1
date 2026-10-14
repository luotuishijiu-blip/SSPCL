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
 $search=$editor.FindName('BoxModSearch');$status=$editor.FindName('FilterModStatus');$view=$editor.FindName('PanModList')
 $search.Text='mod1999';$editor.GetType().GetMethod('RefreshModFilter',$flags).Invoke($editor,@($false))|Out-Null
 if($view.Items.Count -ne 1){throw 'Search result incorrect.'}
 $rows=$editor.GetType().GetField('_modRows',$flags).GetValue($editor);$rows[2000].IsChecked=$true
 $search.Text='';$status.SelectedIndex=1
 if($view.Items.Count -ne 1 -or -not $enabledField.GetValue($editor).Contains('mod1999')){throw 'Enabled filter lost draft or showed wrong rows.'}
 $status.SelectedIndex=3
 if($view.Items.Count -ne 1){throw 'Missing-ID filter incorrect.'}
 $status.SelectedIndex=0
 $editor.GetType().GetMethod('ClearAllMods',$flags).Invoke($editor,@($null,$null))|Out-Null
 $automatic=$editor.GetType().GetField('_automaticAdded',$flags).GetValue($editor);$automatic.Add('mod0')|Out-Null
 $rows[1].IsChecked=$true
 $previous=[Activator]::CreateInstance($profile.EnabledMods.GetType());$previous.Add('sample')
 $arguments=New-Object object[] 1;$arguments[0]=$previous.PSObject.BaseObject
 $difference=$editor.GetType().GetMethod('DescribeDiff',$flags).Invoke($editor,$arguments)
 if(-not $difference.Contains('mod0') -or -not $difference.Contains('sample')){throw "Save difference preview incorrect: $difference"}
 $editor.GetType().GetMethod('ClearAllMods',$flags).Invoke($editor,@($null,$null))|Out-Null
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
 $editor.Width=640;$editor.Height=620;$editor.Measure([Windows.Size]::new(640,620));$editor.Arrange([Windows.Rect]::new(0,0,640,620));$editor.UpdateLayout()
 $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(640,620,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($editor)
 $png=[Windows.Media.Imaging.PngBitmapEncoder]::new();$png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap));$file=[IO.File]::Create([IO.Path]::ChangeExtension($Output,'-compact.png'));try{$png.Save($file)}finally{$file.Dispose()}
 if($editor.FindName('PanModList').ActualHeight -gt 390){throw 'Compact MOD list failed to adapt height.'}
 [IO.Directory]::CreateDirectory((Join-Path $root 'starsector-core'))|Out-Null
 [IO.File]::WriteAllText((Join-Path $root 'starsector-core/starfarer_obf.jar'),'fixture')
 [IO.Directory]::CreateDirectory((Join-Path $root 'jre/bin'))|Out-Null
 [IO.File]::WriteAllText((Join-Path $root 'jre/bin/javaw.exe'),'fixture, never execute')
 [IO.File]::WriteAllText((Join-Path $root 'vmparams'),'java.exe -Xmx4g -Xms1g com.fs.starfarer.StarfarerLauncher')
 $arguments=New-Object object[] 2;$arguments[0]=[string]$root;$arguments[1]=$profile2.EnabledMods.PSObject.BaseObject
 $diagnostics=$editor.GetType().GetMethod('BuildDiagnosticsWindow',$flags).Invoke($editor,$arguments)
 $report=$core.GetType('Sspcl.Core.Launch.LaunchDiagnostics').GetMethod('Check').Invoke($null,$arguments)
 $diagnostics.Content.Children[$diagnostics.Content.Children.Count-1].Text=$report.ToText()
 $backupId=[Guid]::NewGuid().ToString('N');$backupDir=Join-Path $root ('mods/.sspcl-backups/'+$backupId);[IO.Directory]::CreateDirectory($backupDir)|Out-Null
 [IO.File]::WriteAllText((Join-Path $backupDir 'mod_info.json'),'{"id":"sample","name":"Sample MOD","version":"0.9"}')
 $backupProfile=Join-Path $root ('profiles/'+$profile.Id);[IO.Directory]::CreateDirectory($backupProfile)|Out-Null
 [IO.File]::WriteAllText((Join-Path $backupProfile 'profile.json.bak'),('{"Id":"'+$profile.Id+'","Name":"Exploration","EnabledMods":["sample"]}'))
 $catalog=[Activator]::CreateInstance($core.GetType('Sspcl.Core.Mods.BackupCatalog'),@([string](Join-Path $root 'profiles'),[string](Join-Path $root 'mods')))
 $arguments=New-Object object[] 2;$arguments[0]=[string]$root;$arguments[1]=$catalog.PSObject.BaseObject
 $backups=$editor.GetType().GetMethod('BuildBackupsWindow',$flags).Invoke($editor,$arguments)
 $inventory=$catalog.List();$backups.Content.Children[$backups.Content.Children.Count-1].ItemsSource=$inventory
 if($inventory.Count -ne 2){throw 'Backup UI fixture inventory incorrect.'}
 foreach($item in @(@($diagnostics,'-diagnostics.png'),@($backups,'-backups.png'))) {
  $body=$item[0].Content;$item[0].Content=$null
  $content=[Windows.Controls.Border]::new();$content.Child=$body;$content.Width=720;$content.Height=470;$content.Background=[Windows.Media.Brushes]::WhiteSmoke
  $content.Measure([Windows.Size]::new(720,470));$content.Arrange([Windows.Rect]::new(0,0,720,470));$content.UpdateLayout()
  $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(720,470,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($content)
  $png=[Windows.Media.Imaging.PngBitmapEncoder]::new();$png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap));$file=[IO.File]::Create([IO.Path]::ChangeExtension($Output,$item[1]));try{$png.Save($file)}finally{$file.Dispose()}
 }
 Write-Output 'PASS: real diagnostic and backup window content rendered from isolated fixtures.'
 Write-Output 'PASS: search, enabled/missing filters, preserved drafts, difference preview and compact layout.'
 Write-Output "PASS: 2002 MOD rows, $realized realized containers; 100 toggles $($watch.ElapsedMilliseconds) ms; select/clear all and visible save."
 Write-Output 'PASS: real profile cards, missing-MOD display, saved-list editing action, explicit save control and draft-only checkbox changes.'
} finally { $application.Shutdown();[IO.Directory]::Delete($root,$true) }
