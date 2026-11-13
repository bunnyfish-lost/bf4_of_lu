function Initialize-BF4ClientInterop {
 if('BF4ClientInterop' -as [type]){return}
 Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public sealed class BF4ClientWindowInfo { public long handle; public bool responding; public string title=""; }
public static class BF4ClientInterop {
 delegate bool EnumProc(IntPtr hwnd,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr hwnd,EnumProc callback,IntPtr data);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr hwnd);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int size);
 [DllImport("user32.dll",SetLastError=true)] static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp);
 [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp,uint flags,uint timeout,out IntPtr result);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int command);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
 public static BF4ClientWindowInfo Window(int processId){
  var found=new BF4ClientWindowInfo();
  EnumWindows(delegate(IntPtr hwnd,IntPtr data){uint pid;GetWindowThreadProcessId(hwnd,out pid);if(pid!=(uint)processId||!IsWindowVisible(hwnd))return true;var title=new StringBuilder(512);GetWindowText(hwnd,title,title.Capacity);if(title.Length==0)return true;found.handle=hwnd.ToInt64();found.title=title.ToString();IntPtr result;found.responding=SendMessageTimeout(hwnd,0,IntPtr.Zero,IntPtr.Zero,2,400,out result)!=IntPtr.Zero;return false;},IntPtr.Zero);
  return found;
 }
 public static bool QueueStart(int processId){
  IntPtr target=IntPtr.Zero;
  EnumWindows(delegate(IntPtr hwnd,IntPtr data){uint pid;GetWindowThreadProcessId(hwnd,out pid);if(pid!=(uint)processId)return true;
   EnumChildWindows(hwnd,delegate(IntPtr child,IntPtr unused){var title=new StringBuilder(256);GetWindowText(child,title,title.Capacity);string label=title.ToString().Replace("&","").Trim();if((label=="启动游戏"||label=="Start Game"||label=="Launch Game")&&IsWindowEnabled(child)){target=child;return false;}return true;},IntPtr.Zero);return target==IntPtr.Zero;},IntPtr.Zero);
  return target!=IntPtr.Zero&&PostMessage(target,0x00F5,IntPtr.Zero,IntPtr.Zero);
 }
 public static void Focus(long handle){if(handle==0)return;var hwnd=new IntPtr(handle);ShowWindow(hwnd,9);SetForegroundWindow(hwnd);}
}
"@
}
function Get-BF4ProcessSnapshot {
 param([Parameter(Mandatory=$true)][int]$ProcessId)
 $p=$null
 try {
  $p=[Diagnostics.Process]::GetProcessById($ProcessId)
  if($p.HasExited){return [pscustomobject]@{pid=$ProcessId;hasExited=$true;threads=0;unreadable=$false}}
  $image=$p.MainModule.FileName;$ticks=$p.StartTime.ToUniversalTime().Ticks.ToString();$count=$p.Threads.Count
  Initialize-BF4ClientInterop;$window=[BF4ClientInterop]::Window($p.Id)
  return [pscustomobject]@{pid=$p.Id;name=$p.ProcessName;path=$image;startTicks=$ticks;hasExited=$false;threads=$count;ageSeconds=([DateTime]::UtcNow-$p.StartTime.ToUniversalTime()).TotalSeconds;cpuMs=$p.TotalProcessorTime.TotalMilliseconds;hasWindow=($window.handle -ne 0);responding=$window.responding;windowHandle=$window.handle;windowTitle=$window.title;unreadable=$false}
 } catch {
  $still=Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
  if(-not $still){return [pscustomobject]@{pid=$ProcessId;hasExited=$true;threads=0;unreadable=$false}}
  return [pscustomobject]@{pid=$ProcessId;name=$still.ProcessName;path='';hasExited=$false;unreadable=$true;error=$_.Exception.Message}
 } finally {if($p){$p.Dispose()}}
}
function Get-BF4ClientPhase {
 param([Parameter(Mandatory=$true)]$Process)
 if($Process.unreadable){return 'inaccessible'}
 if($Process.hasExited -or $Process.threads -le 0){return 'stale'}
 if($Process.hasWindow){if($Process.responding){return 'window-ready'};return 'unresponsive'}
 if($Process.ageSeconds -ge 60){return 'no-window'}
 return 'starting'
}
function Get-BF4SessionProcesses {
 param([Parameter(Mandatory=$true)][string]$Directory)
 $expected=@('bf4_no_drm.exe','bf4.exe','BF4Launcher.exe' | ForEach-Object {[IO.Path]::GetFullPath((Join-Path $Directory $_))})
 $owned=@();$foreign=@();$stale=@()
 foreach($item in @(Get-Process -Name bf4_no_drm,bf4,BF4Launcher -ErrorAction SilentlyContinue)){
  $info=Get-BF4ProcessSnapshot -ProcessId $item.Id;$phase=Get-BF4ClientPhase -Process $info
  if($phase -eq 'stale'){$stale+=$info;continue}
  if($info.path -and $expected -contains [IO.Path]::GetFullPath($info.path)){$owned+=$info}else{$foreign+=$info}
 }
 return [pscustomobject]@{owned=@($owned);foreign=@($foreign);stale=@($stale)}
}
function Stop-BF4OwnedClient {
 param([Parameter(Mandatory=$true)]$Snapshot,[Parameter(Mandatory=$true)][string]$Directory,[bool]$Confirmed=$false)
 if(-not $Confirmed){throw '重启客户端需要确认。'}
 $allowed=@('bf4_no_drm.exe','bf4.exe','BF4Launcher.exe'|ForEach-Object {[IO.Path]::GetFullPath((Join-Path $Directory $_))})
 if(-not $Snapshot.path -or $allowed -notcontains [IO.Path]::GetFullPath($Snapshot.path)){throw '不处理服务端或其他目录的进程。'}
 $live=Get-Process -Id ([int]$Snapshot.pid) -ErrorAction SilentlyContinue
 if(-not $live -or $live.HasExited){return}
 try{
  if($live.MainModule.FileName -ne $Snapshot.path -or $live.StartTime.ToUniversalTime().Ticks.ToString() -ne [string]$Snapshot.startTicks){throw '客户端身份已变化，请重新检查。'}
  Stop-Process -Id $live.Id -Force -ErrorAction Stop
  if(-not $live.WaitForExit(6000)){throw '客户端尚未退出。'}
 }finally{$live.Dispose()}
}
