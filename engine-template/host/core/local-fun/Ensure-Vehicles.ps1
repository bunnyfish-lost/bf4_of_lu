$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if(-not(Test-Path -LiteralPath (Join-Path $root 'managed-install.json'))){throw 'Managed installation required'}
$r=(Get-Content -LiteralPath "$root\runtime\processes.json" -Raw | ConvertFrom-Json).processes | Where-Object role -eq 'game'
$p=Get-Process -Id $r.pid
if($p.Path -ne "$root\runtime\GameServer\bf4_Server_Final.exe" -or $p.Path -ne $r.path -or $p.StartTime.ToUniversalTime().Ticks.ToString() -ne $r.startTicks){throw 'Test process identity mismatch'}
if((Get-FileHash -LiteralPath $p.Path -Algorithm SHA256).Hash -ne '160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba'){throw 'Unsupported server'}
$loaded=@($p.Modules|Where-Object ModuleName -eq 'BF4VehicleSpawnerV2.dll')
if($loaded.Count){if($loaded[0].FileName -ne (Join-Path $PSScriptRoot 'BF4VehicleSpawnerV2.dll')){throw 'Vehicle module path mismatch'};Write-Output 'Vehicle module already active';exit 0}
$dll=Join-Path $PSScriptRoot 'BF4VehicleSpawnerV2.dll'
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class LoadVehicleProbe {
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

[LoadVehicleProbe]::Load($p.Id,$dll)
Start-Sleep -Milliseconds 1200
Get-Content -LiteralPath (Join-Path $PSScriptRoot 'vehicle-state-v2.json') -Raw
