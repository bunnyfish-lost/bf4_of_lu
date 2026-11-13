param([Parameter(Mandatory=$true)][string]$RequestFile)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
. (Join-Path $PSScriptRoot 'Client-Session.ps1')
try {
 $request=Get-Content -LiteralPath $RequestFile -Raw -Encoding UTF8|ConvertFrom-Json
 if($request.action -eq 'Pick'){
  Add-Type -AssemblyName System.Windows.Forms
  $dialog=New-Object Windows.Forms.FolderBrowserDialog
  $dialog.Description='选择包含 BF4Launcher.exe 的游戏文件夹';$dialog.ShowNewFolderButton=$false
  if($request.directory -and [IO.Directory]::Exists($request.directory)){$dialog.SelectedPath=$request.directory}
  try{if($dialog.ShowDialog() -eq [Windows.Forms.DialogResult]::OK){@{directory=$dialog.SelectedPath}|ConvertTo-Json -Compress}else{@{cancelled=$true}|ConvertTo-Json -Compress}}finally{$dialog.Dispose()}
  exit 0
 }
 if($request.action -notin @('Launch','Inspect','Recover')){throw '未知启动操作'}
 $directory=[IO.Path]::GetFullPath([string]$request.directory)
 $launcher=Join-Path $directory 'BF4Launcher.exe';$game=Join-Path $directory 'bf4_no_drm.exe'
 foreach($file in @($launcher,$game)){if(-not(Test-Path -LiteralPath $file -PathType Leaf)){throw ('找不到 '+$file)}}
 $session=Get-BF4SessionProcesses -Directory $directory
 if($request.action -eq 'Inspect'){@{state='inspection';session=$session}|ConvertTo-Json -Depth 6 -Compress;exit 0}
 if($request.action -eq 'Recover'){
  if($request.confirmed -ne $true){throw '重启客户端需要确认。'}
  foreach($process in @($session.owned)){Stop-BF4OwnedClient -Snapshot $process -Directory $directory -Confirmed $true}
  $session=Get-BF4SessionProcesses -Directory $directory
 }

 $clients=@($session.owned|Where-Object {$_.name -ne 'BF4Launcher'})
 if($clients.Count){
  $client=$clients[0];$phase=Get-BF4ClientPhase -Process $client
  if($phase -eq 'window-ready'){
   [BF4ClientInterop]::Focus($client.windowHandle)
   @{started=$true;state='window-ready';existing=$true;gamePid=$client.pid;message='游戏窗口已打开，已尝试切换到游戏。'}|ConvertTo-Json -Compress;exit 0
  }
  @{started=$false;state=$phase;gamePid=$client.pid;process=$client;message='客户端进程存在，但游戏窗口尚未就绪。若持续没有窗口或无响应，请关闭卡住的客户端后重试；无需停止 BOT 服务端。'}|ConvertTo-Json -Depth 5 -Compress;exit 0
 }
 if($session.foreign.Count){throw '检测到其他目录或无法读取的 BF4 客户端，请先关闭该客户端；没有操作其他实例。'}
 if(@($session.owned|Where-Object {$_.name -eq 'BF4Launcher'}).Count){throw 'BF4 客户端启动窗口仍在运行，请在该窗口继续，或关闭后重试。'}
 $playerId=([string]$request.playerId).Trim();$address=[string]$request.address
 if(-not $playerId -or $playerId.Length -gt 64 -or $playerId -match '[=\r\n\x00-\x1f]'){throw '玩家 ID 无效'}
 $parsed=$null;if(-not [Net.IPAddress]::TryParse($address,[ref]$parsed) -or $parsed.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork){throw 'Blaze IP 无效'}
 $config=Join-Path $directory 'config.txt';$text=''
 if(Test-Path -LiteralPath $config){$text=[IO.File]::ReadAllText($config);Copy-Item -LiteralPath $config -Destination ($config+'.before-panel-'+[guid]::NewGuid().ToString('N')+'.bak')}
 $text=$text -replace '(?im)^[ \t]*(BlazeIP|UserId|AuthCode)[ \t]*=[^\r\n]*(\r?\n|$)',''
 $text=$text.TrimEnd()+[Environment]::NewLine+'BlazeIP='+$address+[Environment]::NewLine+'UserId='+$playerId+[Environment]::NewLine+'AuthCode='+$playerId+[Environment]::NewLine
 $temp=$config+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
 try{[IO.File]::WriteAllText($temp,$text,(New-Object Text.UTF8Encoding($false)));Move-Item -LiteralPath $temp -Destination $config -Force}finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp}}
 $listener=New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback,3216)
 try{$listener.ExclusiveAddressUse=$true;$listener.Start()}catch{throw '3216 端口被占用，请关闭旧的 BF4 客户端启动窗口后再试。'}finally{$listener.Stop()}
 Initialize-BF4ClientInterop
 $p=Start-Process -FilePath $launcher -WorkingDirectory $directory -WindowStyle Normal -PassThru
 $clicked=$false
 for($i=0;$i -lt 32;$i++){
  $p.Refresh();if($p.HasExited){throw '启动器已退出，请查看启动器提示或端口占用'}
  if([BF4ClientInterop]::QueueStart($p.Id)){$clicked=$true;break};Start-Sleep -Milliseconds 250
 }
 if(-not $clicked){@{started=$false;launcherPid=$p.Id;message='启动器已打开，请点击其中的“启动游戏”。此启动器版本未能自动操作。'}|ConvertTo-Json -Compress;exit 0}
 Write-Output '已发送一次启动请求，正在等待游戏窗口。'
 $seen=$false;$last=$null;$deadline=[DateTime]::UtcNow.AddSeconds(45)
 do{
  $live=Get-BF4SessionProcesses -Directory $directory
  $found=@($live.owned|Where-Object {$_.name -ne 'BF4Launcher'})
  if($found.Count){
   $seen=$true;$last=$found[0]
   if((Get-BF4ClientPhase -Process $last) -eq 'window-ready'){
    [BF4ClientInterop]::Focus($last.windowHandle)
    @{started=$true;state='window-ready';launcherPid=$p.Id;gamePid=$last.pid;startTicks=$last.startTicks;message='游戏窗口已打开，正在加载；客户端模组将在窗口稳定后加载。'}|ConvertTo-Json -Compress;exit 0
   }
  }elseif($seen){@{started=$false;state='exited';message='客户端在游戏窗口就绪前退出，请查看客户端诊断。'}|ConvertTo-Json -Compress;exit 0}
  Start-Sleep -Milliseconds 600
 }while([DateTime]::UtcNow -lt $deadline)
 $state=if($last -and $last.hasWindow){'unresponsive'}else{'no-window'}
 @{started=$false;state=$state;process=$last;launcherPid=$p.Id;message='客户端未能打开可响应的游戏窗口。请关闭卡住的客户端后重试；BOT 服务无需重启。'}|ConvertTo-Json -Depth 5 -Compress

} catch {@{error=$_.Exception.Message}|ConvertTo-Json -Compress;exit 1}
