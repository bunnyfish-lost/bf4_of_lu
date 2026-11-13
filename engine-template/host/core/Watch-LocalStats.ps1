param([Parameter(Mandatory=$true)][int]$GamePid,[Parameter(Mandatory=$true)][string]$GameStartTicks)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$mutex=$null;$locked=$false;$session=$null;$status=$null
$statusPath=Join-Path $hostRoot 'runtime\local-stats.json'
try {
 $s=Get-Settings
 $records=@((Read-Json (Join-Path $hostRoot 'runtime\processes.json')).processes|Where-Object role -eq 'game')
 if($records.Count -ne 1 -or $records[0].pid -ne $GamePid -or $records[0].startTicks -ne $GameStartTicks){throw 'Tracked game changed before statistics monitor startup.'}
 $p=Get-Owned $records[0]
 if(-not $p -or $p.Path -ne (Join-Path $s.gameServerDirectory 'bf4_Server_Final.exe')){throw 'Game process identity mismatch.'}
 if((Get-FileHash -LiteralPath $p.Path -Algorithm SHA256).Hash -ne '160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba'){throw 'Unsupported server binary for local statistics.'}
 $mutex=New-Object Threading.Mutex($false,('Local\BF4Host-Stats-'+$s.installationId))
 try{$locked=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$locked=$true}
 if(-not $locked){throw 'A statistics monitor already owns this installation.'}
 Add-Type -Path (Join-Path $PSScriptRoot 'NativeLocalStats.cs')
 $session=New-Object 'NativeLocalStats+Session' -ArgumentList $GamePid,$p.MainModule.BaseAddress.ToInt64(),([long]$GameStartTicks),$true
 $status=[pscustomobject]@{state='starting';enabled=$false;pid=$GamePid;startTicks=$GameStartTicks;guardPid=$PID;startedAt=[DateTime]::UtcNow.ToString('o');checkedAt=$null;lastRestoreAt=$null;corrections=0;error=$null}
 $lastState='';$nextHeartbeat=[DateTime]::MinValue
 while(-not $session.HasExited){
  try{$result=$session.Check()|ConvertFrom-Json}catch{if($session.HasExited){break};throw}
  $now=[DateTime]::UtcNow
  $status.enabled=($result.ready -and $result.after -eq 1)
  $status.state=if($status.enabled){'active'}else{'waiting'}
  if($result.ready -and $result.before -eq 0 -and $result.after -eq 1){
   $status.corrections++;$status.lastRestoreAt=$now.ToString('o')
   Write-Output ($status.lastRestoreAt+' Restored local statistics for game '+$GamePid)
   $nextHeartbeat=[DateTime]::MinValue
  }
  if($status.state -ne $lastState -or $now -ge $nextHeartbeat){
   $status.checkedAt=$now.ToString('o');Write-Json $statusPath $status
   $lastState=$status.state;$nextHeartbeat=$now.AddSeconds(30)
  }
  Start-Sleep -Milliseconds 500
 }
 $status.state='stopped';$status.enabled=$false;$status.checkedAt=[DateTime]::UtcNow.ToString('o');Write-Json $statusPath $status
}catch{
 if($status){$status.state='error';$status.enabled=$false;$status.error=$_.Exception.Message;$status.checkedAt=[DateTime]::UtcNow.ToString('o');Write-Json $statusPath $status}
 Write-Output ('ERROR: '+$_.Exception.Message);exit 1
}finally{
 if($session){$session.Dispose()}
 if($locked){$mutex.ReleaseMutex()}
 if($mutex){$mutex.Dispose()}
}
