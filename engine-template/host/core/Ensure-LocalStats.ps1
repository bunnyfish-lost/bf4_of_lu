$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$s=Get-Settings
$statePath=Join-Path $hostRoot 'runtime\processes.json'
$statusPath=Join-Path $hostRoot 'runtime\local-stats.json'
$state=Read-Json $statePath
$games=@($state.processes|Where-Object role -eq 'game')
if($games.Count -ne 1){throw 'Expected one tracked game for local statistics.'}
$game=Get-Owned $games[0]
if(-not $game -or $game.Path -ne (Join-Path $s.gameServerDirectory 'bf4_Server_Final.exe')){throw 'Game process identity mismatch.'}
$guards=@($state.processes|Where-Object role -eq 'stats')
if($guards.Count -gt 1){throw 'Multiple tracked statistics monitors.'}
$guard=$null
if($guards.Count){
 $guard=Get-Owned $guards[0]
 if($guard -and ($guards[0].gamePid -ne $game.Id -or $guards[0].gameStartTicks -ne $games[0].startTicks)){
  Stop-Process -Id $guard.Id
  if(-not $guard.WaitForExit(5000)){throw 'Previous statistics monitor did not stop.'}
  $guard=$null
 }
}
if(-not $guard){
 $exe=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $arguments='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "{0}" -GamePid {1} -GameStartTicks {2}' -f (Join-Path $PSScriptRoot 'Watch-LocalStats.ps1'),$game.Id,$games[0].startTicks
 $guard=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $hostRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $hostRoot 'runtime\local-stats.stdout.log') -RedirectStandardError (Join-Path $hostRoot 'runtime\local-stats.stderr.log') -PassThru
 $state.processes=@($state.processes|Where-Object role -ne 'stats')+@([pscustomobject]@{role='stats';pid=$guard.Id;path=$exe;startTicks=$guard.StartTime.ToUniversalTime().Ticks.ToString();gamePid=$game.Id;gameStartTicks=$games[0].startTicks})
 Write-Json $statePath $state
}
$deadline=[DateTime]::UtcNow.AddSeconds(15)
do {
 $guard.Refresh()
 if($guard.HasExited){throw 'Statistics monitor exited. Inspect runtime\local-stats.stdout.log and local-stats.stderr.log.'}
 if(Test-Path -LiteralPath $statusPath){
  $status=Read-Json $statusPath
  if($status.pid -eq $game.Id -and $status.startTicks -eq $games[0].startTicks -and $status.guardPid -eq $guard.Id){
   if($status.state -eq 'error'){throw ('Local statistics: '+$status.error)}
   if($status.enabled -and $status.state -eq 'active'){Write-Output 'Local player statistics monitor active.';return}
  }
 }
 Start-Sleep -Milliseconds 200
}while([DateTime]::UtcNow -lt $deadline)
throw 'Statistics monitor startup timed out. Game retained for diagnosis.'
