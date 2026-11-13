$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = $PSScriptRoot
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$sdks = @(& $dotnet --list-sdks)
if ($LASTEXITCODE -ne 0 -or $sdks.Count -eq 0) { throw '请先安装 .NET SDK。' }
$line = $sdks[-1]
if ($line -notmatch '^([^ ]+) \[(.+)\]$') { throw '无法定位 .NET SDK。' }
$compiler = Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs = @('mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Security.dll' | ForEach-Object { '/r:' + (Join-Path $framework $_) })
$refs += @(Get-ChildItem -LiteralPath (Join-Path $root 'bin') -Filter 'SharpDX*.dll' | Sort-Object Name | ForEach-Object { '/r:' + $_.FullName })
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'source') -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName })
$exe = Join-Path $root 'bin\BF4WeaponTuning.exe'
& $dotnet $compiler /nologo /target:winexe /platform:x64 /nostdlib+ /optimize+ /deterministic+ ('/pathmap:' + $root + '=/mod/weapon-tuning') ('/out:' + $exe) @refs @sources
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
$check = Start-Process -FilePath $exe -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($check.ExitCode -ne 0) { throw '枪械参数检查失败。' }
Write-Output '编译和 枪械参数检查通过。对局效果请在游戏中确认。'
