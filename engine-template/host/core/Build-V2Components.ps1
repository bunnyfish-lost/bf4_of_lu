$ErrorActionPreference='Stop'
$base=$PSScriptRoot
$line=(& dotnet --list-sdks|Select-Object -Last 1)
if($line -notmatch '^([^ ]+) \[(.+)\]$'){throw 'Install a .NET SDK to build the components.'}
$compiler=Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Security.dll'|ForEach-Object {'/r:'+(Join-Path $framework $_)})
foreach($item in @(@('local-fun\source\player-effects','local-fun\BF4PlayerEffects.exe','PlayerEffects'),@('local-assist\source','local-assist\BF4VisionAssist.exe','BF4LocalAssist'))){
 $source=Join-Path $base $item[0]
 $sources=@(Get-ChildItem -LiteralPath $source -Filter '*.cs'|Sort-Object Name|ForEach-Object {$_.FullName})
 & dotnet $compiler /nologo /target:exe /platform:x64 /nostdlib+ /optimize+ /deterministic+ ('/main:'+$item[2]) ('/pathmap:'+$source+'=/bf4/'+$item[2]) ('/out:'+(Join-Path $base $item[1])) @refs @sources
 if($LASTEXITCODE -ne 0){throw ('Build failed: '+$item[2])}
}
Write-Output 'V2 shared components built.'
