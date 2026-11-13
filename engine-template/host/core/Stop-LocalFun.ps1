$ErrorActionPreference='Stop'
$dir=Join-Path $PSScriptRoot 'local-fun'
if(-not(Test-Path -LiteralPath $dir)){return}
$lease=Join-Path $dir 'panel-lease.json'
[IO.File]::WriteAllText($lease,'{"enabled":false,"at":0}')
foreach($name in @('local-fun-hit.ini','local-god-v2.ini')){
 [IO.File]::WriteAllText((Join-Path $dir $name),"[LocalFun]`r`nOneHitKill=0`r`nGodMode=0`r`nTick=0`r`nPlayerHex=`r`n")
}
$recordFile=Join-Path $dir 'aim-process.json'
if(Test-Path -LiteralPath $recordFile){
 $r=Get-Content -LiteralPath $recordFile -Raw -Encoding UTF8|ConvertFrom-Json
 $p=Get-Process -Id $r.pid -ErrorAction SilentlyContinue
 $expected=Join-Path $dir 'BF4LocalAim.exe'
 if($p -and $p.Path -eq $expected -and $r.path -eq $expected -and $p.StartTime.ToUniversalTime().Ticks.ToString() -eq $r.startTicks){
  if(-not $p.WaitForExit(1200)){Stop-Process -Id $p.Id -ErrorAction Stop;if(-not $p.WaitForExit(5000)){throw 'Local entertainment helper did not stop.'}}
 }
 Remove-Item -LiteralPath $recordFile
}

$request=Join-Path $dir 'player-effects-request.json'
[IO.File]::WriteAllText($request,'{"id":"shutdown","at":0,"multiplier":1,"noRecoil":false,"noSpread":false,"noBulletDrop":false,"fastBullets":false,"flight":false,"thirdPerson":false,"cameraHotkey":false}')
$effectsRecord=Join-Path $dir 'player-effects-process.json'
if(Test-Path -LiteralPath $effectsRecord){
 $r=Get-Content -LiteralPath $effectsRecord -Raw -Encoding UTF8|ConvertFrom-Json
 $p=Get-Process -Id $r.pid -ErrorAction SilentlyContinue
 $expected=Join-Path $dir 'BF4PlayerEffects.exe'
 if($p -and $p.Path -eq $expected -and $r.path -eq $expected -and $p.StartTime.ToUniversalTime().Ticks.ToString() -eq $r.startTicks){
  if(-not $p.WaitForExit(12000)){Stop-Process -Id $p.Id -ErrorAction Stop;if(-not $p.WaitForExit(5000)){throw 'Player effects helper did not stop.'}}
 }
 Remove-Item -LiteralPath $effectsRecord
}
