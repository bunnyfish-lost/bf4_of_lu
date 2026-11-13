using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace BF4Launcher;
public static class RuntimeFileIdentity {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,StringBuilder path,uint length,uint flags);
 public static string Canonical(string file){using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);var path=new StringBuilder(32768);uint n=GetFinalPathNameByHandle(stream.SafeFileHandle,path,(uint)path.Capacity,0);if(n==0||n>=path.Capacity)throw new IOException("未能读取运行文件路径。");return path.ToString().Replace(@"\\?\","");}
 public static string ReadShared(string file){using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);using var reader=new StreamReader(stream);return reader.ReadToEnd();}
}
