using System;
public sealed class Feature:IFeature {
 string root="";long next;public void Stop(){if(root.Length>0)Native.Lease(root,"local-fun-hit.ini","OneHitKill=0\r\nExcludeHumans=1","");}public void Dispose(){Stop();}
 public string Tick(Frame f){root=f.Input.launcherRoot;if(f.Now<next)return "一击必杀等待服务端回读";next=f.Now+500;string name=f.Reader.ReadOneHitIdentity(f.Roster);bool exclude=Values.Flag(f.Input.config,"excludeHumans",true);Native.Lease(root,"local-fun-hit.ini","OneHitKill="+(string.IsNullOrEmpty(name)?"0":"1")+"\r\nExcludeHumans="+(exclude?"1":"0"),name);return string.IsNullOrEmpty(name)?"等待本机角色部署":"已请求一击必杀 · "+name;}
 public static void Test(){if(!Values.Flag(null,"excludeHumans",true))throw new Exception("Target policy");}
}
