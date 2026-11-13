$ErrorActionPreference='Stop'
$installRoot=Split-Path (Split-Path $PSScriptRoot)
$settings=Get-Content -LiteralPath (Join-Path $installRoot 'settings\host-settings.json') -Raw -Encoding UTF8|ConvertFrom-Json
$serverAddress=$null
if(-not [Net.IPAddress]::TryParse([string]$settings.advertiseAddress,[ref]$serverAddress) -or $serverAddress.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork){throw 'Invalid configured IPv4 server address.'}
$exe=Join-Path $PSScriptRoot 'BF4LocalAim.exe'
$recordFile=Join-Path $PSScriptRoot 'aim-process.json'
if(Test-Path -LiteralPath $recordFile){
 try{
  $r=Get-Content -LiteralPath $recordFile -Encoding UTF8 -Raw|ConvertFrom-Json
  $p=Get-Process -Id $r.pid -ErrorAction Stop
  if($p.Path -eq $exe -and $p.StartTime.ToUniversalTime().Ticks.ToString() -eq $r.startTicks){exit 0}
 }catch{}
}
$other=@(Get-Process -Name BF4LocalAim -ErrorAction SilentlyContinue)
if($other.Count){throw 'Another local entertainment helper is running. Close it in its own panel first.'}
$p=Start-Process -FilePath $exe -ArgumentList '--headless' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru
$record=@{pid=$p.Id;startTicks=$p.StartTime.ToUniversalTime().Ticks.ToString();path=$exe}
[IO.File]::WriteAllText($recordFile,($record|ConvertTo-Json))
