using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Threading;
namespace BF4SupplyMod;
public sealed class SupplyMemory:ISupplyMemory,IDisposable {
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll")]static extern bool ReadProcessMemory(IntPtr h,IntPtr p,byte[] b,UIntPtr n,out UIntPtr done);
 [DllImport("kernel32.dll")]static extern bool WriteProcessMemory(IntPtr h,IntPtr p,byte[] b,UIntPtr n,out UIntPtr done);
 [DllImport("kernel32.dll")]static extern UIntPtr VirtualQueryEx(IntPtr h,IntPtr p,out Region r,UIntPtr n);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetFinalPathNameByHandle(SafeFileHandle h,StringBuilder path,uint length,uint flags);
 [StructLayout(LayoutKind.Explicit,Size=48)]struct Region{[FieldOffset(0)]public ulong Base;[FieldOffset(24)]public ulong Size;[FieldOffset(32)]public uint State;[FieldOffset(36)]public uint Protect;[FieldOffset(40)]public uint Type;}
 readonly IntPtr handle;public long Image{get;}public int Pid{get;}
 public SupplyMemory(Process process){Pid=process.Id;Image=process.MainModule!.BaseAddress.ToInt64();handle=OpenProcess(0x438,false,Pid);if(handle==IntPtr.Zero)throw new Exception("无法读取本实例服务端的补给资源。");}
 public static string Canonical(string file){using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);var path=new StringBuilder(32768);uint n=GetFinalPathNameByHandle(stream.SafeFileHandle,path,(uint)path.Capacity,0);if(n==0||n>=path.Capacity)throw new Exception("未能读取服务端路径。");return path.ToString().Replace(@"\\?\","");}
 public byte[]? Read(long address,int size){var bytes=new byte[size];return ReadProcessMemory(handle,(IntPtr)address,bytes,(UIntPtr)size,out var done)&&done.ToUInt64()==(ulong)size?bytes:null;}
 public bool KnownUnmapped(long address)=>VirtualQueryEx(handle,(IntPtr)address,out var r,(UIntPtr)48)!=UIntPtr.Zero&&r.State!=0x1000;
 public bool WriteFloat(long address,float value){if(!float.IsFinite(value))return false;if(VirtualQueryEx(handle,(IntPtr)address,out var r,(UIntPtr)48)==UIntPtr.Zero||(r.Protect&0xff) is not (4 or 8 or 0x40 or 0x80)||(r.Protect&0x101)!=0||(r.Type!=0x20000&&(r.Protect&0xff) is not (8 or 0x80))||address+4>(long)(r.Base+r.Size))return false;var bytes=BitConverter.GetBytes(value);return WriteProcessMemory(handle,(IntPtr)address,bytes,(UIntPtr)4,out var done)&&done.ToUInt64()==4&&Read(address,4)?.SequenceEqual(bytes)==true;}
 public void VerifyLayout(){
  foreach(var kind in Enum.GetValues<SupplyKind>()){
   var table=Read(Image+SupplyAsset.Vtable(kind),8);if(table==null)throw new Exception("此服务端的补给数据布局不匹配。");long fn=BitConverter.ToInt64(table);var code=Read(fn,8);
   if(code==null||code[0]!=0x48||code[1]!=0x8d||code[2]!=0x05||code[7]!=0xc3)throw new Exception("此服务端的补给类型信息不匹配。");
   long info=fn+7+BitConverter.ToInt32(code,3);var infoBytes=Read(info,8);var metadata=infoBytes==null?null:Read(BitConverter.ToInt64(infoBytes),32);var nameBytes=metadata==null?null:Read(BitConverter.ToInt64(metadata),96);
   string expected=kind switch{SupplyKind.HealingUnit=>"HealingSupplyUnitSphereData",SupplyKind.AmmoUnit=>"AmmoSupplyUnitSphereData",_=>"MedicBagHealingSphereEntityData"};
   if(metadata==null||nameBytes==null||Encoding.ASCII.GetString(nameBytes).Split('\0')[0]!=expected||BitConverter.ToUInt16(metadata,10)!=SupplyAsset.Size(kind))throw new Exception("补给类型名称或结构尺寸不匹配。");
  }
 }
 public IEnumerable<SupplyAsset> Scan(Func<bool> stopping,Action<long>? progress=null){
  var kinds=Enum.GetValues<SupplyKind>().ToDictionary(k=>Image+SupplyAsset.Vtable(k));ulong cursor=0x10000,total=0;int found=0;
  while(!stopping()&&VirtualQueryEx(handle,(IntPtr)(long)cursor,out var region,(UIntPtr)48)!=UIntPtr.Zero){if(region.Size==0||region.Base+region.Size<=cursor)yield break;cursor=region.Base+region.Size;if(region.State!=0x1000||(region.Protect&0x101)!=0||region.Type==0x1000000)continue;
   for(ulong at=region.Base;at<cursor&&!stopping();){int n=(int)Math.Min(1024UL*1024,cursor-at);var bytes=new byte[n];ReadProcessMemory(handle,(IntPtr)(long)at,bytes,(UIntPtr)n,out var got);int length=(int)got.ToUInt64();total+=(ulong)length;
    for(int offset=0;offset+8<=length;offset+=8){long table=BitConverter.ToInt64(bytes,offset);if(!kinds.TryGetValue(table,out var kind))continue;long address=(long)at+offset;var data=Read(address,SupplyAsset.Size(kind));if(data!=null&&SupplyAsset.Valid(kind,data)){yield return new SupplyAsset(kind,address,data);if(++found>1024)yield break;}}
    progress?.Invoke((long)total);if(total>4UL*1024*1024*1024)yield break;at+=(ulong)n;
   }
  }
 }
 public void Dispose()=>CloseHandle(handle);
}

