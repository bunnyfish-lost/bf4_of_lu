param([ValidateRange(-1,64)][int]$Count=-1,[ValidateRange(-1,64)][int]$Expected=-1)
. (Join-Path $PSScriptRoot 'Common.ps1')
try {
    $settings=Get-Settings
    $state=Read-Json (Join-Path $hostRoot 'runtime\processes.json')
    $records=@($state.processes|Where-Object role -eq 'game')
    if($records.Count -ne 1){throw 'Expected one tracked game process.'}
    $p=Get-Owned $records[0];if($null -eq $p){throw 'Game server is stopped.'}
    $exe=Join-Path $settings.gameServerDirectory 'bf4_Server_Final.exe'
    if(-not [string]::Equals($p.Path,$exe,[StringComparison]::OrdinalIgnoreCase)){throw 'Tracked game path mismatch.'}
    if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne '160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba'){throw 'Unsupported server binary. No parameter was changed.'}
    if($Count -gt $settings.maxSoldiers){throw 'Bot target exceeds configured soldier capacity.'}
    if($Count -ge 0){
        $gate=Read-Json (Join-Path $hostRoot 'runtime\bot-start.json')
        if($gate.phase -ne 'ready' -or $gate.installationId -ne $settings.installationId -or $gate.pid -ne $p.Id -or $gate.startTicks -ne $records[0].startTicks){throw 'Waiting for Bot selection initialization.'}
    }
    Add-Type -Path (Join-Path $PSScriptRoot 'NativeBotCount.cs')
    $observed=[NativeBotCount]::Apply($p.Id,$p.MainModule.BaseAddress.ToInt64(),$Count,$Expected)
    [pscustomobject]@{pid=$p.Id;requested=$Count;observed=$observed;parameter='PseudoPlayer.SpawnCount'}|ConvertTo-Json -Compress
} catch { Write-Error $_.Exception.Message;exit 1 }
