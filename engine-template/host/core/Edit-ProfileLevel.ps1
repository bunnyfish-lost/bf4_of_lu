param([Parameter(Mandatory=$true)][string]$RequestPath,[Parameter(Mandatory=$true)][string]$ResultPath)
. (Join-Path $PSScriptRoot 'Common.ps1')
$mutex=$null;$locked=$false
try {
 $settings=Get-Settings
 if($settings.sharedBlaze){throw '请在登录服务所属的管理面板修改等级。'}
 $mutex=New-Object Threading.Mutex($false,('Local\BF4Host-'+$settings.installationId));$locked=$mutex.WaitOne(5000)
 if(-not $locked){throw '正在执行开停服操作，请稍后再试。'}
 $state=Read-Json (Join-Path $hostRoot 'runtime\processes.json')
 foreach($record in $state.processes){if($record.role -in @('game','blaze','stats') -and $null -ne (Get-Owned $record)){throw '请先停止服务器和登录服务，再修改等级。'}}
 $expected=@((Join-Path $settings.serverDirectory 'BF4BlazeServer.exe'),(Join-Path $settings.gameServerDirectory 'bf4_Server_Final.exe'))
 foreach($process in @(Get-Process -Name BF4BlazeServer,bf4_Server_Final -ErrorAction SilentlyContinue)){if(-not $process.Path -or $process.Path -in $expected){throw '服务仍在运行，请停止后再修改等级。'}}
 foreach($port in @($settings.rconPort,10040)){if(Test-Port $port){throw '服务端口仍在监听，请确认游戏服和登录服务已停止。'}}
 & (Get-Node) (Join-Path $PSScriptRoot 'profile-level.mjs') --apply $RequestPath $ResultPath
 if($LASTEXITCODE -ne 0){exit 1}
}catch{[IO.File]::WriteAllText($ResultPath,(@{ok=$false;error=$_.Exception.Message}|ConvertTo-Json -Compress),$utf8);exit 1}finally{if($locked){$mutex.ReleaseMutex()};if($mutex){$mutex.Dispose()}}
