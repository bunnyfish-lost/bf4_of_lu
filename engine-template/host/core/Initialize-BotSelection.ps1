$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$gatePath=Join-Path $hostRoot 'runtime\bot-start.json'
try {
 $s=Get-Settings
 $gate=Read-Json $gatePath
 if($gate.phase -ne 'loading' -or $gate.installationId -ne $s.installationId -or $gate.session -notmatch '^[a-f0-9]{32}$'){throw 'Invalid Bot startup session.'}
 $state=Read-Json (Join-Path $hostRoot 'runtime\processes.json')
 $rec=@($state.processes|Where-Object role -eq 'game')
 if($rec.Count -ne 1){throw 'Expected one tracked game process.'}
 $p=Get-Owned $rec[0];if($null -eq $p){throw 'Game stopped during Bot initialization.'}
 $exe=Join-Path $s.gameServerDirectory 'bf4_Server_Final.exe'
 if($p.Path -ne $exe){throw 'Game process path mismatch.'}
 if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne '160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba'){throw 'Unsupported server binary.'}
 if($true){ # Always initialize the fixed rail startup policy.
  & (Join-Path $PSScriptRoot 'Load-BotSelection.ps1')
  $ready=$false
  for($i=0;$i -lt 40;$i++){
   $lines=Get-Content -LiteralPath (Join-Path $s.gameServerDirectory 'bot-selection.log') -Tail 16 -ErrorAction SilentlyContinue
   $vehicleReady=if($gate.disableVehicles){'1'}else{'0'}
   if(($lines -join "`n") -match ('selection_ready=1 pid='+$p.Id+' session='+$gate.session+' weapons=[01] vehicles='+$vehicleReady+'(?:\s|$)')){$ready=$true;break}
   Start-Sleep -Milliseconds 250
  }
  if(-not $ready){throw 'Bot selection filter did not initialize; Bots remain undeployed.'}
 }
 & (Join-Path $PSScriptRoot 'Load-BotProgress.ps1')
 $progressReady=$false
 for($i=0;$i -lt 40;$i++){
  $lines=Get-Content -LiteralPath (Join-Path $s.gameServerDirectory 'bot-progress.log') -Tail 8 -ErrorAction SilentlyContinue
  if(($lines -join "`n") -match ('progress_ready=1 session='+$gate.session)){$progressReady=$true;break}
  Start-Sleep -Milliseconds 250
 }
 if(-not $progressReady){throw 'Bot progress did not initialize; Bots remain undeployed. Inspect bot-progress.log.'}
 & (Join-Path $PSScriptRoot 'Load-BotDamage.ps1')
 $damageReady=$false
 for($i=0;$i -lt 40;$i++){
  try{$damage=Read-Json (Join-Path $s.gameServerDirectory 'bot-damage-state.json');if($damage.version -eq 1 -and $damage.build -in @('damage-1','damage-2') -and $damage.ready -and $damage.configValid -and $damage.pid -eq $p.Id -and $damage.session -eq $gate.session){$damageReady=$true;break}}catch{}
  Start-Sleep -Milliseconds 250
 }
 if(-not $damageReady){throw 'Bot damage module did not initialize; Bots remain undeployed. Inspect bot-damage.log.'}
 $damageBinding=Read-Json (Join-Path $s.gameServerDirectory 'BF4BotDamage.dll.binding.json')
 if($damageBinding.pid -ne $p.Id -or $damageBinding.startTicks -ne $p.StartTime.ToUniversalTime().Ticks -or $damageBinding.bindingRevision -ne 'v2-damage-context-2'){throw 'V2 damage context binding did not initialize.'}
 $lightsReady=$false
 for($i=0;$i -lt 40;$i++){
  try{$lights=Read-Json (Join-Path $s.gameServerDirectory 'bot-lights-state.json');if($lights.version -eq 1 -and $lights.build -eq 'lights-4' -and $lights.ready -and $lights.configValid -and $lights.pid -eq $p.Id -and $lights.session -eq $gate.session){$lightsReady=$true;break}}catch{}
  Start-Sleep -Milliseconds 250
 }
 if(-not $lightsReady){throw 'Bot lights module did not initialize; Bots remain undeployed. Inspect bot-lights.log.'}
 $author=@($p.Modules|Where-Object {[IO.Path]::GetFullPath($_.FileName) -eq [IO.Path]::GetFullPath((Join-Path $s.gameServerDirectory 'dinput8.dll'))})
 if($author.Count -ne 1){throw 'Expected the original BOT module in this game server.'}
 Write-Output 'V2 native ammo handling; v1 reserve patch not installed.'
 Add-Type -Path (Join-Path $PSScriptRoot 'NativeBotCount.cs')
 $observed=[NativeBotCount]::Apply($p.Id,$p.MainModule.BaseAddress.ToInt64(),[int]$s.initialBots,0)
 Write-Json $gatePath ([pscustomobject]@{phase='ready';progress=$true;damage=$true;damageBinding=$damageBinding.bindingRevision;lights=$true;nativeAmmo=$true;session=$gate.session;installationId=$s.installationId;policyHash=$gate.policyHash;restricted=$gate.restricted;disableVehicles=[bool]$gate.disableVehicles;pid=$p.Id;startTicks=$rec[0].startTicks})
 Write-Output ('Bot selection ready. Initial Bots: '+$observed)
}catch{
 if($gate){$gate.phase='failed';Write-Json $gatePath $gate}
 throw
}
