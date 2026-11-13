$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$s=Get-Settings
$state=Read-Json (Join-Path $hostRoot 'runtime\processes.json')
$records=@($state.processes|Where-Object role -eq 'game')
if($records.Count -ne 1){throw 'Expected one tracked game for local statistics'}
$p=Get-Owned $records[0]
if(-not $p -or $p.Path -ne (Join-Path $s.gameServerDirectory 'bf4_Server_Final.exe')){throw 'Game process identity mismatch'}
if((Get-FileHash -LiteralPath $p.Path -Algorithm SHA256).Hash -ne '160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba'){throw 'Unsupported server binary for local statistics'}
Add-Type -Path (Join-Path $PSScriptRoot 'NativeLocalStats.cs')
$session=New-Object 'NativeLocalStats+Session' -ArgumentList $p.Id,$p.MainModule.BaseAddress.ToInt64(),([long]$records[0].startTicks),$true
try{$result=$session.Check()|ConvertFrom-Json}finally{$session.Dispose()}
if($result.after -ne 1){throw 'Local statistics did not enable'}
Write-Json (Join-Path $hostRoot 'runtime\local-stats-once.json') ([pscustomobject]@{enabled=$true;pid=$p.Id;startTicks=$records[0].startTicks;enabledAt=[DateTime]::UtcNow.ToString('o')})
Write-Output 'Local player statistics enabled.'
