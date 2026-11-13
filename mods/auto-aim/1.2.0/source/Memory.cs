using System;using System.IO;using System.Text;using System.Linq;using System.Diagnostics;using System.Collections.Generic;using System.ComponentModel;using System.Runtime.InteropServices;using System.Security.Cryptography;
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
 public const string ClientSha256="0365a62f197d4544b5326b82a682d77af542e973cb3d9cb6ae582cd543fae2d0";
 public const string ClientBuild="v2-1107";
 public static bool AcceptsClientHash(string hash){return hash==ClientSha256;}
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint a,bool inherit,int pid);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr h,IntPtr address,byte[] data,UIntPtr size,out UIntPtr count);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool K32EnumProcessModulesEx(IntPtr h,[Out]IntPtr[] modules,uint cb,out uint needed,uint flags);
 public IntPtr H;public long Base;public Process Process;
 public Mem(Process process){Process=process;string path=ProcessIdentity.PathOf(process);using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var sha=SHA256.Create()){string hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(!AcceptsClientHash(hash))throw new Exception("此版本模组支持 v2（1107）客户端，请核对运行文件");}
  H=OpenProcess(0x410,false,process.Id);if(H==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"无法读取游戏，请以与游戏相同权限运行启动器");try{var modules=new IntPtr[1];uint needed;if(!K32EnumProcessModulesEx(H,modules,(uint)IntPtr.Size,out needed,3)||modules[0]==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());Base=modules[0].ToInt64();}catch{Dispose();throw;}}
 public static bool Ptr(long p){return p>=0x10000&&p<0x7fffffffffff;}
 public byte[] R(long address,int count){if(!Ptr(address)||count<0||count>1048576)throw new Exception("角色数据尚未就绪");var bytes=new byte[count];UIntPtr got;if(!ReadProcessMemory(H,new IntPtr(address),bytes,(UIntPtr)count,out got)||got.ToUInt64()!=(ulong)count)throw new IOException("读取游戏数据未完成，地址0x"+address.ToString("x")+"，错误"+Marshal.GetLastWin32Error());return bytes;}
 public long Q(long a){return BitConverter.ToInt64(R(a,8),0);}public int I(long a){return BitConverter.ToInt32(R(a,4),0);}public float F(long a){return BitConverter.ToSingle(R(a,4),0);}public byte B(long a){return R(a,1)[0];}public string S(long a,int n){byte[] b=R(a,n);int end=Array.IndexOf(b,(byte)0);return Encoding.UTF8.GetString(b,0,end<0?n:end);}public float[] V(long a,int n){byte[] b=R(a,n*4);var v=new float[n];for(int i=0;i<n;i++)v[i]=BitConverter.ToSingle(b,i*4);return v;}
 public void Dispose(){if(H!=IntPtr.Zero)CloseHandle(H);H=IntPtr.Zero;}
}
