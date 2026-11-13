using System;using System.Collections.Generic;
public static class DeathFastRecovery{
 public sealed class Binding{public long Address;public byte[] Original,Patched;public Binding(long a,byte[] o,byte[] p){Address=a;Original=o;Patched=p;}}
 public static bool Same(byte[] a,byte[] b){return a!=null&&b!=null&&BitConverter.ToString(a)==BitConverter.ToString(b);}
 public static bool Recoverable(string s){return s=="prepared"||s=="installed"||s=="restoring"||s=="restore-failed"||s=="rolled-back"||s=="restored";}
 public static void Restore(Binding[] bindings,Func<long,int,byte[]> read,Action<long,byte[],byte[]> patch){
  foreach(var b in bindings){var actual=read(b.Address,b.Original.Length);if(!Same(actual,b.Original)&&!Same(actual,b.Patched))throw new Exception("补丁位置已被其他程序改变，请正常退出 BF4 后重开。");}
  var failures=new List<string>();
  foreach(var b in bindings)try{if(Same(read(b.Address,b.Original.Length),b.Patched))patch(b.Address,b.Patched,b.Original);}catch(Exception e){failures.Add(e.Message);}
  if(failures.Count>0)throw new Exception("部分位置还原失败，请退出 BF4 完整恢复："+String.Join("；",failures.ToArray()));
 }
}
