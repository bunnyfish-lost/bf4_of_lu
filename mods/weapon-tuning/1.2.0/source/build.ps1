$ErrorActionPreference='Stop'
$source=$PSScriptRoot
$mod=Split-Path -Parent $source
$dotnet=(Get-Command dotnet -ErrorAction Stop).Source
$line=(& $dotnet --list-sdks | Select-Object -Last 1)
if($line -notmatch '^([^ ]+) \[(.+)\]$'){throw 'Install a .NET SDK to build the source.'}
$csc=Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$framework=Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Security.dll')|ForEach-Object {'/r:'+(Join-Path $framework $_)}
if(Test-Path -LiteralPath (Join-Path $mod 'bin')){$refs+=Get-ChildItem -LiteralPath (Join-Path $mod 'bin') -Filter 'SharpDX*.dll' -File|ForEach-Object {'/r:'+$_.FullName}}
$refs+=@(('/r:'+(Join-Path $framework 'WPF\PresentationCore.dll')),('/r:'+(Join-Path $framework 'WPF\WindowsBase.dll')))
$output=Join-Path $mod 'bin\BF4WeaponTuning.exe'
& $dotnet $csc /nologo /platform:x64 /nostdlib+ /optimize+ /deterministic+ /debug- /target:winexe ('/out:'+$output) @refs @(Get-ChildItem -LiteralPath $source -Filter '*.cs' -File|ForEach-Object FullName)
if($LASTEXITCODE -ne 0){throw 'Build failed'}
Write-Host $output
