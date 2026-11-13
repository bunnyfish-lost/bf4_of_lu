$ErrorActionPreference='Stop'
$exe=Join-Path $PSScriptRoot 'BF4PlayerEffects.exe'
$state=Join-Path $PSScriptRoot 'player-effects-process.json'
if(Test-Path -LiteralPath $state){$r=Get-Content -LiteralPath $state -Raw|ConvertFrom-Json;$p=Get-Process -Id $r.pid -ErrorAction SilentlyContinue;if($p -and $p.Path -eq $exe -and $p.StartTime.ToUniversalTime().Ticks.ToString() -eq $r.startTicks){exit 0}}
$p=Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru
[IO.File]::WriteAllText($state,([pscustomobject]@{pid=$p.Id;path=$exe;startTicks=$p.StartTime.ToUniversalTime().Ticks.ToString()}|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
