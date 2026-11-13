$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -eq 'Desktop'){Import-Module -Name (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop}
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$environment=Get-Content -LiteralPath (Join-Path (Split-Path -Parent $root) 'data\environment.json') -Raw -Encoding UTF8|ConvertFrom-Json
if($environment.complete -ne $true -or $environment.runtimeVersion -ne 'v2-1107' -or [IO.Path]::GetFullPath($environment.root) -ne [IO.Path]::GetFullPath((Split-Path -Parent $root))){throw 'Initialize this v2 installation first'}
$r=(Get-Content -LiteralPath "$root\runtime\processes.json" -Raw|ConvertFrom-Json).processes|Where-Object role -eq 'game'
$sp=Get-Process -Id $r.pid
if($sp.Path -ne "$root\runtime\GameServer\bf4_Server_Final.exe" -or $sp.Path -ne $r.path -or $sp.StartTime.ToUniversalTime().Ticks.ToString() -ne $r.startTicks){throw 'Test process identity mismatch'}
$clientPath=Join-Path $root 'mods\client\bf4_no_drm.exe'
$clients=@(Get-Process -Name bf4_no_drm -ErrorAction SilentlyContinue|Where-Object { $_.Path -and [IO.Path]::GetFullPath($_.Path) -eq [IO.Path]::GetFullPath($clientPath) })
if($clients.Count -ne 1){throw '请先启动一个本机游戏客户端'}
$cp=$clients[0]
if((Get-FileHash -LiteralPath $sp.Path -Algorithm SHA256).Hash -ne '160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba'){throw 'Unsupported server'}
if((Get-FileHash -LiteralPath $cp.Path -Algorithm SHA256).Hash -ne '0365a62f197d4544b5326b82a682d77af542e973cb3d9cb6ae582cd543fae2d0'){throw 'Unsupported client'}
$dll=Join-Path $PSScriptRoot 'BF4Flight2.dll'
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class LoadFlight {
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


foreach($p in @($sp,$cp)){
 $loaded=@($p.Modules|Where-Object ModuleName -eq 'BF4Flight2.dll')
 if($loaded.Count){if($loaded[0].FileName -ne $dll){throw 'Flight module path mismatch'}}else{[LoadFlight]::Load($p.Id,$dll)}
}
Start-Sleep -Milliseconds 500
Write-Output 'Flight modules prepared'
