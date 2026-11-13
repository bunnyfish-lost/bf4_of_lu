param([ValidateSet('Start','Stop','Restart','RestartGame','Status','Check')][string]$Action='Status')
. (Join-Path $PSScriptRoot 'Common.ps1')
$mutex=$null;$locked=$false
try {
    $settings=Get-Settings
    $runtime=Join-Path $hostRoot 'runtime';$statePath=Join-Path $runtime 'processes.json'
    $mutex=New-Object Threading.Mutex($false,('Local\BF4Host-'+$settings.installationId));$locked=$mutex.WaitOne(5000)
    if(-not $locked){throw 'Another server operation is running.'}
    function Read-State { Read-Json $statePath }
    function Remove-StaleProcessRecords {
        $state=Read-State
        $current=@($state.processes)
        $live=@(foreach($record in $current){if($null -ne (Get-Owned $record)){$record}})
        # Finish every identity check before saving; unreadable identities still block.
        if($live.Count -ne $current.Count){$state.processes=$live;Write-Json $statePath $state}
    }
    function Save-Process($Role,$Process,$Exe){$st=Read-State;$st.processes=@($st.processes|Where-Object role -ne $Role)+@([pscustomobject]@{role=$Role;pid=$Process.Id;path=$Exe;startTicks=$Process.StartTime.ToUniversalTime().Ticks.ToString()});Write-Json $statePath $st}
    function Find-Role($Role){$records=@((Read-State).processes|Where-Object role -eq $Role);if($records.Count -gt 1){throw "Multiple tracked $Role processes."};if($records.Count){return Get-Owned $records[0]};return $null}
    function Stop-Role($Role){$p=Find-Role $Role;if($null -ne $p){Stop-Process -Id $p.Id;if(-not $p.WaitForExit(5000)){throw "$Role process did not stop."}};$st=Read-State;$st.processes=@($st.processes|Where-Object role -ne $Role);Write-Json $statePath $st}
    function Check-Ports($Ports,[string]$Protocol,[string]$ListenAddress=''){
        if($Protocol -eq 'TCP'){$occupied=@([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()|Where-Object Port -in $Ports)}
        else{$occupied=@([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveUdpListeners()|Where-Object Port -in $Ports)}
        # Local Blaze binds only IPv4 loopback. Another concrete address (such as
        # Steam302 on 127.0.0.2) can use the same port. Keep wildcard conflicts,
        # and leave LAN, game/RCON and UDP checks conservative.
        if($Protocol -eq 'TCP' -and $ListenAddress -eq '127.0.0.1'){
            $occupied=@($occupied | Where-Object {
                $address=$_.Address
                if($address.IsIPv4MappedToIPv6){$address=$address.MapToIPv4()}
                $address.Equals([Net.IPAddress]::Loopback) -or
                $address.Equals([Net.IPAddress]::Any) -or
                $address.Equals([Net.IPAddress]::IPv6Any)
            })
        }
        if($occupied.Count){throw ("$Protocol ports already in use: "+(($occupied|ForEach-Object Port|Select-Object -Unique)-join ', ')+'. Stop the other service or inspect 05-Check-Host.cmd; no unrelated process was stopped.')}
    }
    if($Action -eq 'Status'){(Read-State).processes|ForEach-Object{[pscustomobject]@{role=$_.role;pid=$_.pid;running=($null -ne (Get-Owned $_))}}|ConvertTo-Json;exit 0}
    if($Action -ne 'Check'){Remove-StaleProcessRecords}
    if($Action -in @('Stop','Restart','RestartGame')){
        & (Join-Path $PSScriptRoot 'Stop-LocalFun.ps1')
        $currentGame=Find-Role 'game'
        if($null -ne $currentGame){
            & (Join-Path $PSScriptRoot 'Ensure-LocalStats.ps1')
            & (Get-Node) (Join-Path $PSScriptRoot 'save-session.mjs')
            if($LASTEXITCODE -ne 0){throw 'Current player progress was not confirmed. Services were left running.'}
            & (Get-Node) (Join-Path $PSScriptRoot 'bot-progress.mjs') flush
            if($LASTEXITCODE -ne 0){throw 'Bot progress was not confirmed. Services were left running.'}
        }
        Stop-Role 'game'
        if($null -ne $currentGame){Start-Sleep -Milliseconds 1500}
        Stop-Role 'stats'
    }
    if($Action -in @('Stop','Restart') -and -not $settings.sharedBlaze){Stop-Role 'blaze'}
    if($Action -in @('Stop','Restart') -and -not $settings.sharedBlaze){
        & (Get-Node) (Join-Path $PSScriptRoot 'player-profiles.mjs') backup
        if($LASTEXITCODE -ne 0){throw 'Player data backup failed; services remain stopped.'}
    }
    if($Action -in @('Restart','RestartGame')){
        $launcherRoot=Split-Path -Parent $hostRoot
        $launcherConfig=Join-Path $launcherRoot 'config.json'
        if(Test-Path -LiteralPath $launcherConfig -PathType Leaf){
            try{
                $cleanupConfig=Read-Json $launcherConfig
                if($cleanupConfig.autoCleanLogs -eq $true){
                    $cleanupScope=if($Action -eq 'RestartGame'){'game'}else{'services'}
                    $cleanupRaw=& (Get-Node) (Join-Path $launcherRoot 'engine\log-cleanup.mjs') --scope $cleanupScope
                    if($LASTEXITCODE -eq 0){
                        $cleanup=$cleanupRaw | ConvertFrom-Json
                        if($cleanup.state -eq 'cleaned'){Write-Output ('自动清理日志：{0} 个文件，释放 {1:N1} MiB。' -f $cleanup.deletedFiles,($cleanup.freedBytes/1MB))}
                        elseif($cleanup.reason){Write-Output $cleanup.reason}
                    }else{Write-Output '日志清理未完成，继续重启服务。'}
                }
            }catch{Write-Output ('日志清理未完成：'+$_.Exception.Message)}
        }
    }
    if($Action -eq 'Stop'){Write-Output 'Stopped this installation game and Blaze.';exit 0}
    if($hostRoot -match '[^\x20-\x7e]'){throw 'The server installation must use an English path.'}
    if($settings.serverName.Length -gt 60 -or [string]::IsNullOrWhiteSpace($settings.serverName) -or $settings.serverName -match '["\\\x00-\x1f]'){throw 'Invalid server name.'}
    if($settings.advertiseAddress -ne '127.0.0.1' -and $settings.advertiseAddress -notin @(Get-LocalAddresses|Select-Object -ExpandProperty Address)){throw 'The configured host IP is no longer local. Update the virtual IPv4 in the panel and restart all services.'}
    if($settings.maxSoldiers -ne 64 -or $settings.initialBots -lt 0 -or $settings.initialBots -ge $settings.maxSoldiers){throw 'Soldier capacity is fixed at 64 and cannot be adjusted yet; initial Bot count must leave a human slot.'}
    foreach($port in @($settings.gamePort,$settings.rconPort)){if($port -lt 1024 -or $port -gt 65535){throw 'Invalid game/RCON port.'}}
    if($settings.rconPort -in $settings.blazeTcpPorts -or $settings.rconPort -eq $settings.panelPort){throw 'RCON port conflicts with another host service.'}
    $blaze=Find-Role 'blaze';$game=Find-Role 'game'
    if($settings.sharedBlaze){
        $shared=Read-Json (Join-Path $settings.sharedBlazeRoot 'runtime\processes.json')
        $br=@($shared.processes|Where-Object role -eq 'blaze')
        if($br.Count -ne 1 -or $null -eq (Get-Owned $br[0])){throw 'Shared Blaze must already be running.'}
        $blaze=Get-Owned $br[0]
    }
    if($null -eq $blaze){Check-Ports $settings.blazeTcpPorts 'TCP' '127.0.0.1'}
    if($null -eq $game){Check-Ports @($settings.rconPort) 'TCP';Check-Ports @($settings.gamePort) 'UDP'}
    if($Action -eq 'Check'){Write-Output 'Preflight passed.';exit 0}
    $secret=Read-Json (Join-Path $runtime 'private.json');if($secret.rconPassword -notmatch '^[a-f0-9]{16}$'){throw 'Invalid RCON credential.'}
    if($null -eq $blaze){
        & (Get-Node) (Join-Path $PSScriptRoot 'player-profiles.mjs') prepare
        if($LASTEXITCODE -ne 0){throw 'Player profile preparation failed; Blaze was not started.'}
        $exe=Join-Path $settings.serverDirectory 'BF4BlazeServer.exe'
        $blaze=Start-Process -FilePath $exe -WorkingDirectory $settings.serverDirectory -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtime 'blaze.stdout.log') -RedirectStandardError (Join-Path $runtime 'blaze.stderr.log') -PassThru
        Save-Process 'blaze' $blaze $exe
    }
    $deadline=[DateTime]::UtcNow.AddSeconds(40)
    do{$blaze.Refresh();if($blaze.HasExited){throw 'Blaze exited. Inspect runtime\blaze.stderr.log and port diagnostics.'};$missing=@($settings.blazeTcpPorts|Where-Object{-not(Test-Port $_)});if(-not $missing.Count){break};Start-Sleep -Milliseconds 500}while([DateTime]::UtcNow -lt $deadline)
    if($missing.Count){throw ('Blaze startup timed out; missing ports: '+($missing -join ','))}
    $newGame=$false
    if($null -eq $game){
        & (Get-Node) (Join-Path $PSScriptRoot 'prepare-loadout.mjs')
        if($LASTEXITCODE -ne 0){throw 'Bot selection configuration is invalid.'}
        $newGame=$true
        $exe=Join-Path $settings.gameServerDirectory 'bf4_Server_Final.exe';$instancePath=Join-Path $settings.gameServerDirectory 'Instance'
        $arguments='-serverInstancePath "{0}" -Server.ServerName "{1}" -GamePort {2} -RemoteAdminPort {3} +admin.password "{4}" -PingSite nrt -Region Asia -Country CN -blazeIP 127.0.0.1 -ServerIp {5} -exit' -f $instancePath,$settings.serverName,$settings.gamePort,$settings.rconPort,$secret.rconPassword,$settings.advertiseAddress
        $game=Start-Process -FilePath $exe -WorkingDirectory $settings.gameServerDirectory -ArgumentList $arguments -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtime 'game.stdout.log') -RedirectStandardError (Join-Path $runtime 'game.stderr.log') -PassThru
        Save-Process 'game' $game $exe
    }
    $deadline=[DateTime]::UtcNow.AddSeconds(50)
    do{Start-Sleep -Milliseconds 500;$game.Refresh();if($game.HasExited){throw 'Game server exited. Inspect runtime\GameServer logs.'};$ready=Test-Port $settings.rconPort}while(-not $ready -and [DateTime]::UtcNow -lt $deadline)
    if(-not $ready){throw 'RCON startup timed out. Process retained for diagnosis.'}
    if($newGame){
        & (Join-Path $PSScriptRoot 'Initialize-BotSelection.ps1')
    }
    & (Join-Path $PSScriptRoot 'Ensure-LocalStats.ps1')
    Write-Output 'Services started. Wait for IN_GAME in the panel before joining.'
} catch { Write-Output ('ERROR: '+$_.Exception.Message);exit 1 } finally {if($locked){$mutex.ReleaseMutex()};if($mutex){$mutex.Dispose()}}
