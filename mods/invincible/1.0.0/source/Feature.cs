using System;
public sealed class Feature:IFeature {
 string root="";long next;public void Stop(){if(root.Length>0)Native.Lease(root,"local-god-v2.ini","GodMode=0","");}public void Dispose(){Stop();}
 public string Tick(Frame f){root=f.Input.launcherRoot;if(f.Now<next)return "无敌等待服务端回读";next=f.Now+500;string name=f.Reader.ReadOneHitIdentity(f.Roster,true);Native.Lease(root,"local-god-v2.ini","GodMode="+(string.IsNullOrEmpty(name)?"0":"1"),name);return string.IsNullOrEmpty(name)?"等待存活角色部署":"已请求本机角色无敌 · "+name;}
 public static void Test(){if(Values.Text(null,"not-used","self")!="self")throw new Exception("Self policy");}
}
