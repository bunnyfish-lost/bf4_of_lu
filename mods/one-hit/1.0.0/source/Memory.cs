using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Security.Cryptography;
public sealed class Mem:IDisposable {
[DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
[DllImport("kernel32.dll")]static extern bool ReadProcessMemory(IntPtr handle,IntPtr address,byte[] data,UIntPtr size,out UIntPtr got);
[DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
public IntPtr H; public long Base; public Process Process;
public Mem(Process p){Process=p;Base=p.MainModule.BaseAddress.ToInt64();using(var f=File.OpenRead(p.MainModule.FileName))using(var s=SHA256.Create()){var hash=BitConverter.ToString(s.ComputeHash(f)).Replace("-","").ToLowerInvariant();if(hash!="0f3b4ca5e4b9642413b8df5d869d94337a8bb690238ae2b2e5a780a68942aeb4"&&hash!="968d96948c1306c9218a5123f788f18d0b909db30cdf4d673df720f45995a57b")throw new Exception("Unsupported offline client");}H=OpenProcess(0x1010,false,p.Id);if(H==IntPtr.Zero)throw new Exception("Cannot open offline client read-only");}
public static bool Ptr(long p){return p>=0x10000&&p<0x7fffffffffff;}
public byte[] R(long a,int n){if(!Ptr(a))throw new Exception("Pointer unavailable");byte[] b=new byte[n];UIntPtr got;if(!ReadProcessMemory(H,new IntPtr(a),b,(UIntPtr)n,out got)||got.ToUInt64()!=(ulong)n)throw new Exception("Read unavailable at "+a.ToString("x"));return b;}
public long Q(long a){return BitConverter.ToInt64(R(a,8),0);}public int I(long a){return BitConverter.ToInt32(R(a,4),0);}public float F(long a){return BitConverter.ToSingle(R(a,4),0);}public byte B(long a){return R(a,1)[0];}
public string S(long a,int n){var b=R(a,n);var z=Array.IndexOf(b,(byte)0);return Encoding.UTF8.GetString(b,0,z<0?n:z);}
public float[] V(long a,int n){var b=R(a,n*4);var v=new float[n];for(int i=0;i<n;i++)v[i]=BitConverter.ToSingle(b,i*4);return v;}
public void Dispose(){if(H!=IntPtr.Zero)CloseHandle(H);H=IntPtr.Zero;}
}
