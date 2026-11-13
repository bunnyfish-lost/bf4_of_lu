using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace BF4SupplyMod;
public static class Program {
 static Process? OwnedServer(JsonNode context,out string ticks){
  ticks="";var record=context["server"];if(record==null)return null;var expected=Path.Combine(context["launcherRoot"]!.ToString(),"host","runtime","GameServer","bf4_Server_Final.exe");
  Process? p=null;try{int pid=record["pid"]!.GetValue<int>();ticks=record["startTicks"]!.ToString();p=Process.GetProcessById(pid);if(p.HasExited||p.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture)!=ticks){p.Dispose();return null;}
   var file=SupplyMemory.Canonical(expected);if(!string.Equals(SupplyMemory.Canonical(record["executable"]!.ToString()),file,StringComparison.OrdinalIgnoreCase)||!string.Equals(SupplyMemory.Canonical(p.MainModule!.FileName),file,StringComparison.OrdinalIgnoreCase))throw new Exception("服务端实例路径不匹配。");return p;
  }catch(ArgumentException){p?.Dispose();return null;}catch(InvalidOperationException){p?.Dispose();return null;}catch{p?.Dispose();throw;}
 }
 static void VerifyBuild(string modDirectory,Process game){var expected=JsonNode.Parse(ModJson.ReadShared(Path.Combine(modDirectory,"supported-server.json")))!["sha256"]!.ToString();using var file=new FileStream(game.MainModule!.FileName,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);if(!string.Equals(Convert.ToHexString(SHA256.HashData(file)),expected,StringComparison.OrdinalIgnoreCase))throw new Exception("本模组尚未适配这个服务端版本。");}
 public static int Main(string[] args){
  if(args.Length!=2||args[0]!="--context")return 2;
  JsonNode context=JsonNode.Parse(ModJson.ReadShared(args[1]))!;if(context["apiVersion"]?.GetValue<int>()!=1)return 2;
  string token=context["instanceId"]!.ToString(),root=Path.GetFullPath(context["launcherRoot"]!.ToString()),directory=context["modDirectory"]!.ToString(),stateDirectory=context["stateDirectory"]!.ToString(),status=context["statusFile"]!.ToString(),stop=context["stopFile"]!.ToString();
  string own=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","mod.json"));if(!string.Equals(SupplyMemory.Canonical(own),SupplyMemory.Canonical(Path.Combine(directory,"mod.json")),StringComparison.OrdinalIgnoreCase))return 2;
  int parentPid=context["launcher"]!["pid"]!.GetValue<int>();using var parent=Process.GetProcessById(parentPid);if(parent.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture)!=context["launcher"]!["startTicks"]!.ToString())return 2;
  using var mutex=new Mutex(true,"Local\\BF4SupplyMod-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant())))[..20],out bool first);if(!first)return 0;
  Process? game=null;SupplyMemory? memory=null;SupplySession? session=null;string ticks="";int blockedPid=0;string blockedTicks="",blockedMessage="";var scan=Stopwatch.StartNew();long lastProgress=-2000;
  bool Stopping()=>File.Exists(stop)||parent.HasExited||DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-(context["heartbeatAt"]?.GetValue<long>()??0)>15000;
  void Status(string phase,string message,SupplySettings? settings=null){ModJson.Atomic(status,JsonSerializer.Serialize(new{apiVersion=1,instanceId=token,at=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),workerPid=Environment.ProcessId,state=phase,message,config=context["config"]?.DeepClone(),details=new{pid=game?.Id??0,healingFields=session?.HealingFields??0,ammoFields=session?.AmmoFields??0,assets=session?.AssetCount??0,scannedBytes=session?.ScannedBytes??0}},ModJson.Options));}
  void Close(){session?.Dispose();session=null;memory?.Dispose();memory=null;game?.Dispose();game=null;ticks="";}
  try{
   try{Process.GetCurrentProcess().PriorityClass=ProcessPriorityClass.BelowNormal;}catch{}
   while(!Stopping()){
    try{
     var latest=JsonNode.Parse(ModJson.ReadShared(args[1]))!;if(latest["instanceId"]?.ToString()!=token)break;context=latest;var settings=SupplyPolicy.Parse(context["config"]??new JsonObject());
     using var candidate=OwnedServer(context,out var startTicks);if(candidate==null){Close();Status("waiting","等待本实例服务端。",settings);Thread.Sleep(500);continue;}
     if(candidate.Id==blockedPid&&startTicks==blockedTicks){Status("error",blockedMessage,settings);Thread.Sleep(500);continue;}
     if(game==null||game.Id!=candidate.Id||ticks!=startTicks){Close();VerifyBuild(directory,candidate);game=Process.GetProcessById(candidate.Id);ticks=startTicks;memory=new SupplyMemory(game);session=new SupplySession(memory,stateDirectory,ticks);Status("loading","正在读取补给资源。",settings);session.Scan(Stopping,_=>{if(scan.ElapsedMilliseconds-lastProgress>1000){lastProgress=scan.ElapsedMilliseconds;Status("loading","正在读取补给资源。",settings);}});scan.Restart();lastProgress=-2000;}
     else if(scan.Elapsed.TotalSeconds>15){session!.Scan(Stopping);scan.Restart();}
     if(Stopping())break;session!.Apply(settings);
     if(settings.Healing==1&&settings.Ammo==1)Status("default","补给倍率 1×，使用原始参数。",settings);
     else if((settings.Healing==1||session.HealingFields>0)&&(settings.Ammo==1||session.AmmoFields>0))Status("applied",$"补给数据已应用 · 医疗 {settings.Healing:0.##}× / 弹药 {settings.Ammo:0.##}×",settings);
     else Status("waiting","等待医疗包或弹药包资源加载。",settings);
    }catch(SupplyConflictException ex){blockedPid=game?.Id??0;blockedTicks=ticks;blockedMessage=ex.Message;Close();Status("error",ex.Message);}catch(Exception ex){Close();Status("error",ex.Message);}
    Thread.Sleep(500);
   }
  }finally{Close();Status("stopped","补给模块已停止，已撤回可用资源上的修改。");}
  return 0;
 }
}
