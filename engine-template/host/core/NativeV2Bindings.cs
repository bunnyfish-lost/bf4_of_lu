using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
public sealed class NativeV2BindingReport {public int pid;public long startTicks;public string module,sourceSha256,outputSha256;public long site,entry,resolved,tlsIndexSlot;public int attackerOffset,victimOffset;public string bindingRevision;}
public static class NativeV2Bindings {
 const string ServerHash="160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba";
 const string HookHash="b96f7bd37a4b62090e13c3a13cda5b2b213deabfc16f4c49c8c5da7d535e0428";
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint a,bool inherit,int id);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr h,IntPtr p,byte[] b,UIntPtr n,out UIntPtr done);
 static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
 static bool SamePath(string a,string b){return string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);}
 static byte[] Read(IntPtr h,long address,int count){var b=new byte[count];UIntPtr done;if(!ReadProcessMemory(h,new IntPtr(address),b,(UIntPtr)count,out done)||done.ToUInt64()!=(ulong)count)throw new Win32Exception(Marshal.GetLastWin32Error(),"Cannot read v2 native binding");return b;}
 static void Require(IntPtr h,long address,byte[] expected,string name){if(!Read(h,address,expected.Length).SequenceEqual(expected))throw new Exception("V2 damage "+name+" signature changed");}
 static int Offset(byte[] bytes,int rva){int pe=BitConverter.ToInt32(bytes,0x3c),count=BitConverter.ToUInt16(bytes,pe+6),table=pe+24+BitConverter.ToUInt16(bytes,pe+20);for(int i=0;i<count;i++){int row=table+40*i,va=BitConverter.ToInt32(bytes,row+12),size=BitConverter.ToInt32(bytes,row+16);if(rva>=va&&rva<va+size)return BitConverter.ToInt32(bytes,row+20)+rva-va;}throw new Exception("Invalid native helper RVA");}
 static void Replace(byte[] bytes,int rva,byte[] before,byte[] after){if(before.Length!=after.Length)throw new Exception("Invalid native binding size");int p=Offset(bytes,rva);if(!bytes.Skip(p).Take(before.Length).SequenceEqual(before))throw new Exception("Native helper source bytes changed");Array.Copy(after,0,bytes,p,after.Length);}
 static long Resolve(IntPtr h,long address,long author,long size){for(int i=0;i<5;i++){if(address>=author&&address<author+size)return address;var b=Read(h,address,16);if(b[0]==0xe9){address=address+5+BitConverter.ToInt32(b,1);continue;}if(b[0]==0xff&&b[1]==0x25){address=BitConverter.ToInt64(Read(h,address+6+BitConverter.ToInt32(b,2),8),0);continue;}if(b[0]==0x48&&b[1]==0xb8&&b[10]==0xff&&b[11]==0xe0){address=BitConverter.ToInt64(b,2);continue;}throw new Exception("V2 hook thunk is not recognized; native extension was not loaded");}throw new Exception("V2 hook chain exceeded expected length");}
 public static NativeV2BindingReport Prepare(int pid,long startTicks,string expected,string source,string destination){
  var bytes=File.ReadAllBytes(source);string sourceHash=Hash(bytes),name=Path.GetFileName(source);string want=name=="BF4BotProgress.dll"?"45f93f65644dbc139face80df72d70b68a2a613288cb2935738df88fada0c3f2":name=="BF4BotDamage.dll"?"d052a1e35d242ffc30636be720cad422f1e647ee99ba5e775efe672e5e5cc3d3":null;
  if(want==null||sourceHash!=want)throw new Exception("Unsupported native extension source");if(!SamePath(destination,Path.Combine(Path.GetDirectoryName(expected),name)))throw new Exception("Native extension destination mismatch");
  using(var process=Process.GetProcessById(pid)){
   if(process.HasExited||process.StartTime.ToUniversalTime().Ticks!=startTicks||!SamePath(process.MainModule.FileName,expected)||Hash(File.ReadAllBytes(expected))!=ServerHash)throw new Exception("Managed v2 game identity mismatch");
   var hook=process.Modules.Cast<ProcessModule>().Single(m=>SamePath(m.FileName,Path.Combine(Path.GetDirectoryName(expected),"dinput8.dll")));
   if(process.MainModule.BaseAddress.ToInt64()!=0x140000000||hook.BaseAddress.ToInt64()!=0x180000000||Hash(File.ReadAllBytes(hook.FileName))!=HookHash)throw new Exception("Unsupported v2 Bot module identity");
   var h=OpenProcess(0x1010,false,pid);if(h==IntPtr.Zero)throw new Win32Exception();
   try{
    var report=new NativeV2BindingReport{pid=pid,startTicks=startTicks,module=name,sourceSha256=sourceHash};
    if(name=="BF4BotProgress.dll"){
     var call=Read(h,0x140263972,5);if(!call.SequenceEqual(new byte[]{0xe8,0x39,0x32,0x93,0}))throw new Exception("V2 progress callsite is occupied");
     var original=new byte[]{0x39,0x91,0,3,0,0,0x74,0x14};var live=Read(h,0x1403f5ef0,8);
     if(live[0]!=0xe9||!live.Skip(5).SequenceEqual(original.Skip(5)))throw new Exception("V2 progress identity method does not match the expected detour");
     long resolved=Resolve(h,0x1403f5ef0,hook.BaseAddress.ToInt64(),hook.ModuleMemorySize);
     Replace(bytes,0x3310,original,live);report.site=0x1403f5ef0;report.entry=report.site;report.resolved=resolved;
    }else{
     var call=Read(h,0x14029b731,5);if(call[0]!=0xe8)throw new Exception("V2 damage handler callsite changed");long target=0x14029b736+BitConverter.ToInt32(call,1),resolved=Resolve(h,target,hook.BaseAddress.ToInt64(),hook.ModuleMemorySize);long delta=resolved-hook.BaseAddress.ToInt64();if(delta<int.MinValue||delta>int.MaxValue)throw new Exception("V2 damage thunk is out of range");
     var site=Read(h,0x140b45386,5);if(site[0]!=0xe8||0x140b4538b+BitConverter.ToInt32(site,1)!=0x140d4b8f0)throw new Exception("V2 native damage calculation is occupied");
     if(delta!=0xab590)throw new Exception("Unsupported v2 damage handler");
     Require(h,0x1800ab62c,new byte[]{0x8b,0x0d,0x7e,0x51,0xcb,0,0x65,0x48,0x8b,0x04,0x25,0x58,0,0,0,0x48,0x8b,0x34,0xc8},"TLS lookup");
     Require(h,0x1800acc9b,new byte[]{0xb9,0xc0,2,0,0,0x44,0x0f,0x10,0x14,0x31},"scoped context");
     Require(h,0x1800accbb,new byte[]{0x48,0x8b,0x45,0x60,0x48,0x89,0x04,0x31,0x48,0x8b,0x45,0x30,0x48,0x89,0x44,0x31,8,0x48,0x8b,0x45,0,0x48,0x89,0x44,0x31,0x10},"attacker and victim");
     Require(h,0x1800acd52,new byte[]{0x4c,0x8b,0x15,0xdf,0x60,0xb6,0,0x49,0x81,0xc2,0xa0,0xe3,0xbc,0},"original handler");
     Require(h,0x1800acda5,new byte[]{0xb8,0xc0,2,0,0,0x44,0x0f,0x11,0x14,0x30,0x44,0x0f,0x11,0x4c,0x30,0x10},"context restore");
     long tlsSlot=0x180d607b0;int index=BitConverter.ToInt32(Read(h,tlsSlot,4),0);if(index<0||index>=1024||BitConverter.ToInt32(Read(h,0x1816d2ed0,4),0)!=index||BitConverter.ToInt64(Read(h,0x180c12e38,8),0)!=0x140000000)throw new Exception("V2 damage TLS identity mismatch");
     Replace(bytes,0x1dea,BitConverter.GetBytes(0x6c3e0),BitConverter.GetBytes((int)delta));
     Replace(bytes,0x13f9,BitConverter.GetBytes(0xa8a8d0),BitConverter.GetBytes(0xd607b0));
     Replace(bytes,0x142f,BitConverter.GetBytes(0x210),BitConverter.GetBytes(0x2c0));
     Replace(bytes,0x1436,BitConverter.GetBytes(0x218),BitConverter.GetBytes(0x2c8));
     report.site=0x14029b731;report.entry=target;report.resolved=resolved;report.tlsIndexSlot=tlsSlot;report.attackerOffset=0x2c0;report.victimOffset=0x2c8;report.bindingRevision="v2-damage-context-2";
    }
    report.outputSha256=Hash(bytes);File.WriteAllBytes(destination,bytes);return report;
   }finally{CloseHandle(h);}
  }
 }
}
