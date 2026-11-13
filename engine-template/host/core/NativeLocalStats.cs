using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class NativeLocalStats {
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessTimes(IntPtr h,out long created,out long exited,out long kernel,out long user);
 [DllImport("kernel32.dll",SetLastError=true)] static extern uint WaitForSingleObject(IntPtr h,uint milliseconds);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h,IntPtr addr,byte[] data,UIntPtr size,out UIntPtr read);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteProcessMemory(IntPtr h,IntPtr addr,byte[] data,UIntPtr size,out UIntPtr written);
 [DllImport("kernel32.dll",SetLastError=true)] static extern UIntPtr VirtualQueryEx(IntPtr h,IntPtr address,out MemoryInfo info,UIntPtr size);
 [StructLayout(LayoutKind.Sequential)] struct MemoryInfo {
  public IntPtr BaseAddress,AllocationBase; public uint AllocationProtect;
  public UIntPtr RegionSize; public uint State,Protect,Type;
 }
 static byte[] Read(IntPtr h,long address,int count){
  var b=new byte[count];UIntPtr n;
  if(!ReadProcessMemory(h,new IntPtr(address),b,(UIntPtr)count,out n)||n.ToUInt64()!=(ulong)count)throw new Win32Exception();
  return b;
 }
 public sealed class Session:IDisposable {
  IntPtr handle; readonly long module; readonly int pid; readonly bool writable;
  public Session(int processId,long baseAddress,long expectedStartTicks,bool enable){
   if(baseAddress!=0x140000000)throw new Exception("Unexpected base");
   pid=processId;module=baseAddress;writable=enable;
   handle=OpenProcess(enable?0x100438u:0x100410u,false,pid);
   if(handle==IntPtr.Zero)throw new Win32Exception();
   try {
    long created,exited,kernel,user;
    if(!GetProcessTimes(handle,out created,out exited,out kernel,out user))throw new Win32Exception();
    if(expectedStartTicks!=0&&DateTime.FromFileTimeUtc(created).Ticks!=expectedStartTicks)throw new Exception("Game process identity mismatch");
    CheckSignature();
   } catch {Dispose();throw;}
  }
  void CheckSignature(){
   var code=Read(handle,module+0xff50da,7);var expected=new byte[]{0x48,0x8b,0x05,0x6f,0xbf,0x65,0x01};
   for(int i=0;i<code.Length;i++)if(code[i]!=expected[i])throw new Exception("Statistics code signature mismatch");
  }
  public bool HasExited {
   get {if(handle==IntPtr.Zero)return true;uint result=WaitForSingleObject(handle,0);if(result==0xffffffff)throw new Win32Exception();return result==0;}
  }
  public string Check(){
   if(HasExited)throw new Exception("Tracked game exited");
   CheckSignature();
   long settings=BitConverter.ToInt64(Read(handle,module+0x2651050,8),0);
   if(settings==0)return "{\"pid\":"+pid+",\"ready\":false}";
   if(settings<0x10000||settings>0x7ffffffe0000||(settings&7)!=0)throw new Exception("Invalid server settings pointer");
   long address=settings+0x1be;
   MemoryInfo info;
   if(VirtualQueryEx(handle,new IntPtr(address),out info,(UIntPtr)Marshal.SizeOf(typeof(MemoryInfo)))==UIntPtr.Zero||info.State!=0x1000||(info.Protect!=4&&info.Protect!=8))throw new Exception("Statistics flag is not in writable data");
   byte before=Read(handle,address,1)[0];if(before>1)throw new Exception("Invalid ranked flag");
   if(writable&&before==0){
    if(BitConverter.ToInt64(Read(handle,module+0x2651050,8),0)!=settings)return "{\"pid\":"+pid+",\"ready\":false}";
    UIntPtr n;var value=new byte[]{1};
    if(!WriteProcessMemory(handle,new IntPtr(address),value,(UIntPtr)1,out n)||n.ToUInt64()!=1)throw new Win32Exception();
   }
   if(BitConverter.ToInt64(Read(handle,module+0x2651050,8),0)!=settings)return "{\"pid\":"+pid+",\"ready\":false}";
   return "{\"pid\":"+pid+",\"ready\":true,\"before\":"+before+",\"after\":"+Read(handle,address,1)[0]+"}";
  }
  public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
 }
 public static string Apply(int pid,long baseAddress,bool enable){using(var session=new Session(pid,baseAddress,0,enable)){return session.Check();}}
}
