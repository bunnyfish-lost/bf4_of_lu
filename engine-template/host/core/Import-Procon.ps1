param([string]$Source)
. (Join-Path $PSScriptRoot 'Common.ps1')
try{
    if(-not $Source){$Source=(Read-Host 'Folder containing PRoCon.exe').Trim('"')}
    $dest=Join-Path $hostRoot 'procon-source'
    if(Test-Path -LiteralPath $dest){throw 'Procon was already imported. Existing files were not overwritten.'}
    $files=@('PRoCon.exe','PRoCon.exe.config','PRoCon.Core.dll','Ionic.Zip.Reduced.dll','MySql.Data.dll','PRoCon.xml')
    foreach($f in $files){if(-not(Test-Path -LiteralPath (Join-Path $Source $f))){throw "Missing Procon file: $f"}}
    foreach($d in @('Localization','Media','docs_licenses','Configs')){if(-not(Test-Path -LiteralPath (Join-Path $Source $d) -PathType Container)){throw "Missing Procon folder: $d"}}
    New-Item -ItemType Directory -Path $dest,(Join-Path $dest 'Configs') -Force|Out-Null
    foreach($f in $files){Copy-Item -LiteralPath (Join-Path $Source $f) -Destination $dest}
    foreach($d in @('Localization','Media','docs_licenses')){Copy-Item -LiteralPath (Join-Path $Source $d) -Destination (Join-Path $dest $d) -Recurse}
    Get-ChildItem -LiteralPath (Join-Path $Source 'Configs') -Filter '*.def' -File|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $dest 'Configs')}
    Write-Output 'Imported Procon without old accounts, connection passwords or plugins. Open it from the panel.'
}catch{Write-Output ('ERROR: '+$_.Exception.Message);exit 1}
