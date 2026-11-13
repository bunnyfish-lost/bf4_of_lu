$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -Force
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=[IO.Path]::GetFullPath($PSScriptRoot)
$template=Join-Path $root 'engine-template'
$payload=Join-Path $root 'engine-payload.zip'
$temp=$payload+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
$zip=[IO.Compression.ZipFile]::Open($temp,[IO.Compression.ZipArchiveMode]::Create)
try {
 foreach($file in Get-ChildItem -LiteralPath $template -Recurse -File | Sort-Object FullName){
  if(($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '组件源目录不应包含链接文件。'}
  $relative=$file.FullName.Substring($template.Length+1).Replace('\','/')
  if($relative -match '^(mods/|engine/builtin-mods/|engine/builtin-mod-catalog\.json$)'){throw 'Mod packages must remain outside the embedded engine payload.'}
  [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
 }
} finally {$zip.Dispose()}
Move-Item -LiteralPath $temp -Destination $payload -Force
& dotnet publish (Join-Path $root 'Launcher.csproj') -c Release -o (Join-Path $root 'publish') --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
if($LASTEXITCODE -ne 0){throw '编译失败。'}
Write-Output (Join-Path $root 'publish\BF4Launcher-0.3.0-1107.exe')
