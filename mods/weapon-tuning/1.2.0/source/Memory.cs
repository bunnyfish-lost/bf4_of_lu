using System;using System.IO;using System.Text;using System.Linq;using System.Diagnostics;using System.Collections.Generic;using System.ComponentModel;using System.Runtime.InteropServices;using System.Security.Cryptography;
public sealed class MemoryReadException:IOException {public long Address;public int Requested,Received,NativeError;public MemoryReadException(long a,int n,int got,int code):base("读取未完成（错误"+code+"，地址0x"+a.ToString("x")+"，"+got+"/"+n+"字节）"){Address=a;Requested=n;Received=got;NativeError=code;}}
public static class ProcessIdentity {
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint a,bool inherit,int id);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr h,uint flags,StringBuilder text,ref int length);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetProcessTimes(IntPtr h,out long creation,out long exit,out long kernel,out long user);
 public static string PathOf(Process process){IntPtr h=OpenProcess(0x1000,false,process.Id);if(h==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());try{var path=new StringBuilder(32768);int length=path.Capacity;if(!QueryFullProcessImageName(h,0,path,ref length))throw new Win32Exception(Marshal.GetLastWin32Error());return System.IO.Path.GetFullPath(path.ToString());}finally{CloseHandle(h);}}
 public static string StartTicks(Process process){IntPtr h=OpenProcess(0x1000,false,process.Id);if(h==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());try{long created,exit,kernel,user;if(!GetProcessTimes(h,out created,out exit,out kernel,out user))throw new Win32Exception(Marshal.GetLastWin32Error());return DateTime.FromFileTimeUtc(created).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);}finally{CloseHandle(h);}}
 public static void Verify(Process process,string expected,string ticks){if(process.HasExited||!String.Equals(PathOf(process),System.IO.Path.GetFullPath(expected),StringComparison.OrdinalIgnoreCase)||StartTicks(process)!=ticks)throw new Exception("本实例进程身份已变化");}
}
public sealed class Mem:IDisposable {
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint a,bool inherit,int pid);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr h,IntPtr address,byte[] data,UIntPtr size,out UIntPtr count);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool WriteProcessMemory(IntPtr h,IntPtr address,byte[] data,UIntPtr size,out UIntPtr count);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool K32EnumProcessModulesEx(IntPtr h,[Out]IntPtr[] modules,uint cb,out uint needed,uint flags);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr at,UIntPtr size,uint type,uint protect);
 public const string ClientSha256="0365a62f197d4544b5326b82a682d77af542e973cb3d9cb6ae582cd543fae2d0";
 public const string ServerSha256="160ec988aad0632946936a21a9aed1c73f5b906161f2d96b2a085ee65ce183ba";
 public const string ClientBuild="v2-1107";
 public static bool AcceptsImageHash(string hash,string expectedHash=null){return expectedHash==null||expectedHash==ClientSha256?hash==ClientSha256:expectedHash==ServerSha256&&hash==ServerSha256;}
 public IntPtr H;public long Base;public Process Process;readonly bool writable;readonly Dictionary<string,long> snapshots=new Dictionary<string,long>();
 public Mem(Process process,string expectedHash=null,bool writable=false){Process=process;this.writable=writable;string path=ProcessIdentity.PathOf(process);using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var sha=SHA256.Create()){string hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(!AcceptsImageHash(hash,expectedHash))throw new Exception("此版本模组支持 v2（1107）客户端，请核对运行文件；检测到 "+hash);;}
  H=OpenProcess(writable?0x438u:0x410u,false,process.Id);if(H==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());try{int error=0;var modules=new IntPtr[1];uint needed;for(int i=0;i<10;i++){if(K32EnumProcessModulesEx(H,modules,(uint)IntPtr.Size,out needed,3)&&modules[0]!=IntPtr.Zero){Base=modules[0].ToInt64();return;}error=Marshal.GetLastWin32Error();if(error!=299&&error!=24)break;System.Threading.Thread.Sleep(20);}throw new Win32Exception(error,"读取客户端模块地址未完成");}catch{Dispose();throw;}
 }
 public static bool Ptr(long p){return p>=0x10000&&p<0x7fffffffffff;}
 public byte[] R(long address,int count){if(!Ptr(address)||count<0||count>1048576)throw new Exception("指针尚未就绪");var bytes=new byte[count];UIntPtr got;if(!ReadProcessMemory(H,new IntPtr(address),bytes,(UIntPtr)count,out got)||got.ToUInt64()!=(ulong)count)throw new MemoryReadException(address,count,(int)got.ToUInt64(),Marshal.GetLastWin32Error());return bytes;}
 public long Q(long a){return BitConverter.ToInt64(R(a,8),0);}public int I(long a){return BitConverter.ToInt32(R(a,4),0);}public float F(long a){return BitConverter.ToSingle(R(a,4),0);}public byte B(long a){return R(a,1)[0];}public string S(long a,int n){byte[] bytes=R(a,n);int end=Array.IndexOf(bytes,(byte)0);return Encoding.UTF8.GetString(bytes,0,end<0?n:end);}public float[] V(long a,int n){var bytes=R(a,n*4);var values=new float[n];for(int i=0;i<n;i++)values[i]=BitConverter.ToSingle(bytes,i*4);return values;}
 public void Write(long a,byte[] bytes){if(!writable)throw new InvalidOperationException("此读取器不允许写入");UIntPtr done;if(!Ptr(a)||!WriteProcessMemory(H,new IntPtr(a),bytes,(UIntPtr)bytes.Length,out done)||done.ToUInt64()!=(ulong)bytes.Length||!R(a,bytes.Length).SequenceEqual(bytes))throw new Exception("参数写入未确认");}public void W(long a,float value){Write(a,BitConverter.GetBytes(value));}
 public long Snapshot(byte[] bytes){string key;using(var sha=SHA256.Create())key=Convert.ToBase64String(sha.ComputeHash(bytes));long address;if(snapshots.TryGetValue(key,out address)){if(!R(address,bytes.Length).SequenceEqual(bytes))throw new Exception("私有参数副本已变化");return address;}address=VirtualAllocEx(H,IntPtr.Zero,(UIntPtr)Math.Max(4096,bytes.Length),0x3000,4).ToInt64();if(!Ptr(address))throw new Win32Exception(Marshal.GetLastWin32Error());Write(address,bytes);snapshots[key]=address;return address;}
 public void Dispose(){if(H!=IntPtr.Zero)CloseHandle(H);H=IntPtr.Zero;}
}
