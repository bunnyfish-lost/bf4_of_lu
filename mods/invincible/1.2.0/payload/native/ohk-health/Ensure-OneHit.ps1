$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -eq 'Desktop'){Import-Module -Name (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop}
$installRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$environment=Get-Content -LiteralPath (Join-Path (Split-Path -Parent $installRoot) 'data\environment.json') -Raw -Encoding UTF8|ConvertFrom-Json
if($environment.complete -ne $true -or $environment.runtimeVersion -ne 'v2-1107' -or [IO.Path]::GetFullPath($environment.root) -ne [IO.Path]::GetFullPath((Split-Path -Parent $installRoot))){throw 'Initialize this v2 installation first'}
$settings=Get-Content -LiteralPath (Join-Path $installRoot 'settings\host-settings.json') -Raw -Encoding UTF8|ConvertFrom-Json
$serverAddress=$null
if(-not [Net.IPAddress]::TryParse([string]$settings.advertiseAddress,[ref]$serverAddress) -or $serverAddress.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork){throw 'Invalid configured IPv4 server address.'}
$expected=Join-Path $installRoot 'runtime\GameServer\bf4_Server_Final.exe'
$records=Get-Content -LiteralPath (Join-Path $installRoot 'runtime\processes.json') -Raw -Encoding UTF8|ConvertFrom-Json
$owned=@($records.processes|Where-Object role -eq 'game')
if($owned.Count -ne 1){throw 'No tracked game server in this installation.'}
$matches=@(Get-Process -Name 'bf4_Server_Final' -ErrorAction SilentlyContinue | Where-Object Path -eq $expected)
if($matches.Count -ne 1){throw 'Expected exactly one local game server'}
$p=$matches[0]
if($p.Id -ne $owned[0].pid -or $p.Path -ne $owned[0].path -or $p.StartTime.ToUniversalTime().Ticks.ToString() -ne $owned[0].startTicks){throw 'Game server ownership mismatch.'}
if($p.Path -ne $expected){throw 'Unexpected process path'}
if((Get-FileHash -LiteralPath $expected -Algorithm SHA256).Hash -ne '160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba'){throw 'Unsupported server'}
if((Get-FileHash -LiteralPath (Join-Path (Split-Path $expected) 'dinput8.dll') -Algorithm SHA256).Hash -ne 'b96f7bd37a4b62090e13c3a13cda5b2b213deabfc16f4c49c8c5da7d535e0428'){throw 'Unsupported AI module'}

$stateFile=Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'local-fun-hit-state.json'
function Assert-OneHitState {for($attempt=0;$attempt -lt 30;$attempt++){try{$state=Get-Content -LiteralPath $stateFile -Raw -Encoding UTF8|ConvertFrom-Json;if($state.pid -eq $p.Id -and $state.installed -and ((Get-Date)-(Get-Item -LiteralPath $stateFile).LastWriteTime).TotalSeconds -lt 5){return}}catch{};Start-Sleep -Milliseconds 100};throw 'One-hit hook was not confirmed; leave it disabled.'}

if(@($p.Modules|Where-Object {$_.ModuleName -eq 'BF4LocalFun.dll' -and $_.FileName -ne (Join-Path $PSScriptRoot 'BF4LocalFun.dll')}).Count){throw 'A different local module is already loaded; restart the game server.'}
if(@($p.Modules|Where-Object ModuleName -eq 'BF4LocalFun.dll').Count){Assert-OneHitState;Write-Output 'One-hit extension already loaded';return}
$dll=Join-Path $PSScriptRoot 'BF4LocalFun.dll'
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class LoadLocalFun {
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint a,bool b,int id);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string s);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr h,string s);
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr a,UIntPtr n,uint t,uint p);
 [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr h,IntPtr a,UIntPtr n,uint t);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr n,out UIntPtr w);
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr CreateRemoteThread(IntPtr h,IntPtr a,UIntPtr n,IntPtr start,IntPtr param,uint flags,IntPtr id);
 [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h,uint ms);
 public static void Load(int pid,string path) {
  var h=OpenProcess(0x43a,false,pid);if(h==IntPtr.Zero)throw new Win32Exception();
  IntPtr memory=IntPtr.Zero,thread=IntPtr.Zero;bool finished=false;
  try {var bytes=Encoding.Unicode.GetBytes(path+"\0");memory=VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)bytes.Length,0x3000,4);if(memory==IntPtr.Zero)throw new Win32Exception();
   UIntPtr written;if(!WriteProcessMemory(h,memory,bytes,(UIntPtr)bytes.Length,out written)||written.ToUInt64()!=(ulong)bytes.Length)throw new Win32Exception();
   thread=CreateRemoteThread(h,IntPtr.Zero,UIntPtr.Zero,GetProcAddress(GetModuleHandle("kernel32.dll"),"LoadLibraryW"),memory,0,IntPtr.Zero);if(thread==IntPtr.Zero)throw new Win32Exception();
   finished=WaitForSingleObject(thread,10000)==0;if(!finished)throw new Exception("Local extension load timed out; allocation retained until process exits.");
  } finally {if(thread!=IntPtr.Zero)CloseHandle(thread);if(memory!=IntPtr.Zero&&(finished||thread==IntPtr.Zero))VirtualFreeEx(h,memory,UIntPtr.Zero,0x8000);CloseHandle(h);}
 }
}
'@
[LoadLocalFun]::Load($p.Id,$dll)
Assert-OneHitState

Write-Output 'Local one-hit health handler loaded; inspect local-fun-hit-state.json.'
