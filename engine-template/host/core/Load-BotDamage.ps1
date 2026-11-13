$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$s=Get-Settings
$state=Read-Json (Join-Path $hostRoot 'runtime\processes.json')
$rec=@($state.processes|Where-Object role -eq 'game')
if($rec.Count -ne 1){throw 'Expected one tracked game.'}
$p=Get-Owned $rec[0];if($null -eq $p){throw 'Game is stopped.'}
$game=$s.gameServerDirectory
if($p.Path -ne (Join-Path $game 'bf4_Server_Final.exe')){throw 'Game process path mismatch.'}
if((Get-FileHash -LiteralPath $p.Path -Algorithm SHA256).Hash -ne '160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba'){throw 'Unsupported server binary.'}
if((Get-FileHash -LiteralPath (Join-Path $game 'dinput8.dll') -Algorithm SHA256).Hash -ne 'b96f7bd37a4b62090e13c3a13cda5b2b213deabfc16f4c49c8c5da7d535e0428'){throw 'Unsupported original Bot module.'}
if(@($p.Modules|Where-Object ModuleName -eq 'BF4BotDamage.dll').Count){throw 'Extension already loaded; restart game before changing it.'}
$dll=Join-Path $game 'BF4BotDamage.dll'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'native\BF4BotDamage.dll') -Destination $dll -Force
if(-not('NativeV2Bindings' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'NativeV2Bindings.cs')}
$binding=[NativeV2Bindings]::Prepare($p.Id,$p.StartTime.ToUniversalTime().Ticks,$p.Path,(Join-Path $PSScriptRoot ('native\'+[IO.Path]::GetFileName($dll))),$dll)
Write-Json ($dll+'.binding.json') $binding
Write-Output ('Prepared verified v2 native binding: '+$binding.module)
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class LoadBotDamage {
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
   finished=WaitForSingleObject(thread,10000)==0;if(!finished)throw new Exception("Test extension load timed out; allocation retained until process exits.");
  } finally {if(thread!=IntPtr.Zero)CloseHandle(thread);if(memory!=IntPtr.Zero&&(finished||thread==IntPtr.Zero))VirtualFreeEx(h,memory,UIntPtr.Zero,0x8000);CloseHandle(h);}
 }
}
'@
[LoadBotDamage]::Load($p.Id,$dll)
Write-Output ('Loaded Bot damage extension into PID '+$p.Id+'. Inspect bot-damage.log for initialization result.')
