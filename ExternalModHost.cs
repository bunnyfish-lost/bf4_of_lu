using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
namespace BF4Launcher;
public sealed record RuntimeProcessInfo(int Pid,string StartTicks,string Executable);
public sealed partial class ExternalModHost {
 sealed class Running {public string Group="",Signature="",Token="",ContextFile="",Directory="",StateDirectory="",Configuration="";public Process Wrapper=null!;public JsonObject Context=new();public DateTime Started=DateTime.UtcNow;}
 readonly string root;readonly Action<string> log;readonly Dictionary<string,Running> running=new();readonly ConcurrentDictionary<string,string> feedback=new();readonly SemaphoreSlim gate=new(1,1);CancellationTokenSource? cancellation;Task? loop;volatile bool suspended;
 public ExternalModHost(string root,Action<string>? log=null){this.root=Path.GetFullPath(root);this.log=log??(_=>{});}
 public void Ensure(){suspended=false;if(loop is {IsCompleted:false})return;cancellation=new CancellationTokenSource();loop=Task.Run(()=>Loop(cancellation.Token));}
 public string? Feedback(string key)=>feedback.TryGetValue(key,out var value)?value:null;
 public async Task StopAllAsync(){suspended=true;await gate.WaitAsync();try{await Task.WhenAll(running.Values.ToArray().Select(Stop));running.Clear();feedback.Clear();}finally{gate.Release();}}
 public async Task CloseAsync(){cancellation?.Cancel();if(loop!=null)await loop;await StopAllAsync();cancellation?.Dispose();cancellation=null;loop=null;}
 async Task Loop(CancellationToken token){while(!token.IsCancellationRequested){try{await TickAsync();}catch(Exception ex){log("外部模组："+ex.Message);}try{await Task.Delay(750,token);}catch(OperationCanceledException){break;}}}
 public async Task TickAsync(){
  await gate.WaitAsync();try{
   if(suspended)return;
   var config=JsonSerializer.Deserialize<LauncherConfig>(RuntimeFileIdentity.ReadShared(Path.Combine(root,"config.json")),Repository.Json)??throw new Exception("配置内容为空。");ModSelectionPolicy.Apply(config);
   var active=config.Mods.Where(m=>m.Enabled).ToList();
   JsonNode? environment=null;try{environment=JsonNode.Parse(RuntimeFileIdentity.ReadShared(Path.Combine(root,"data/environment.json")));}catch{}
   if(environment?["runtimeVersion"]?.ToString()!="v2-1107"||environment?["complete"]?.GetValue<bool>()!=true){foreach(var old in running.Values.ToArray())await Stop(old);running.Clear();foreach(var item in active.Where(m=>m.Manifest.Adapter=="external"))feedback[item.Key]="请先准备 v2 运行环境";return;}
var external=active.Where(m=>m.Manifest.Adapter=="external").ToList();var groups=new HashSet<string>();
   foreach(var group in external.GroupBy(ExternalModSpec.Group)){
    var items=group.ToList();var provider=items[^1];string name=group.Key;groups.Add(name);
    try{
     foreach(var item in items){ExternalModSpec.Validate(item.Manifest);Repository.Validate(item.Config,item.Manifest.Fields);foreach(var dep in item.Manifest.Dependencies)if(!active.TakeWhile(m=>m!=item).Any(m=>m.Key==dep||m.Manifest.Id==dep))throw new Exception(item.Name+" 缺少前置模组 "+dep);}
     var spec=provider.Manifest.Runtime!;var server=OwnedServer();var client=spec.StartWhen=="client"?OwnedClient():null;bool ready=spec.StartWhen=="launcher"||spec.StartWhen=="server"&&server!=null||spec.StartWhen=="client"&&client!=null;
     if(!ready){if(running.Remove(name,out var old))await Stop(old);foreach(var item in items)feedback[item.Key]=spec.StartWhen=="client"?"等待本实例客户端":"等待本实例服务端";continue;}
     var directory=ModDirectory(provider);string entry=ExternalModSpec.EntryPath(directory,spec);var values=ExternalModSpec.Merge(items);
     string signature=JsonSerializer.Serialize(new{provider.Key,entry,spec.Type,spec.StartWhen,spec.Arguments,server,client},Repository.Json);
     if(running.TryGetValue(name,out var current)&&current.Signature==signature&&current.Wrapper.HasExited&&current.Configuration==JsonSerializer.Serialize(values,Repository.Json)){foreach(var item in items)feedback[item.Key]="模组程序已退出；重新启用后加载，运行记录见日志。";continue;}
     if(current!=null&&(current.Signature!=signature||current.Wrapper.HasExited)){await Stop(current);running.Remove(name);current=null;}
     if(current==null){current=Start(name,signature,provider,items,values,directory,entry,config,server,client);running[name]=current;}
     UpdateContext(current,provider,items,values,server,client);
     string message=ReadFeedback(current,values);foreach(var item in items){bool overridden=item.Key!=provider.Key;feedback[item.Key]=overridden?"运行程序被同组后方版本覆盖（配置按顺序合并）":message;}
    }catch(Exception ex){if(running.Remove(name,out var old))await Stop(old);foreach(var item in items)feedback[item.Key]=ex.Message;}
   }
   foreach(var key in running.Keys.Where(k=>!groups.Contains(k)).ToArray()){await Stop(running[key]);running.Remove(key);}
   foreach(var key in feedback.Keys.Where(k=>!external.Any(m=>m.Key==k)).ToArray())feedback.TryRemove(key,out _);
  }finally{gate.Release();}
 }
 string ModDirectory(ModItem mod){string path=Path.GetFullPath(Path.Combine(root,mod.Directory)),prefix=Path.GetFullPath(Path.Combine(root,"mods"))+Path.DirectorySeparatorChar;if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new Exception("模组目录不正确。");return path;}
 RuntimeProcessInfo? OwnedServer(){
  var file=Path.Combine(root,"host","runtime","processes.json");if(!File.Exists(file))return null;try{var node=JsonNode.Parse(RuntimeFileIdentity.ReadShared(file));var record=node?["processes"]?.AsArray().FirstOrDefault(p=>p?["role"]?.ToString()=="game");if(record==null)return null;int pid=record["pid"]!.GetValue<int>();string ticks=record["startTicks"]?.ToString()??"",path=record["path"]!.ToString();using var p=Process.GetProcessById(pid);if(p.HasExited||p.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture)!=ticks)return null;string image=RuntimeFileIdentity.Canonical(p.MainModule!.FileName),expected=RuntimeFileIdentity.Canonical(path);var canonicalRoot=Path.GetDirectoryName(RuntimeFileIdentity.Canonical(Path.Combine(root,"config.json")))!;if(!string.Equals(image,expected,StringComparison.OrdinalIgnoreCase)||!image.StartsWith(Path.Combine(canonicalRoot,"host","runtime")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))return null;return new(pid,ticks,image);}catch{return null;}
 }
 RuntimeProcessInfo? OwnedClient(){var expected=Path.Combine(root,"host","mods","client","bf4_no_drm.exe");if(!File.Exists(expected))return null;foreach(var p in Process.GetProcessesByName("bf4_no_drm")){using(p)try{if(!p.HasExited&&p.Threads.Count>0&&p.MainWindowHandle!=IntPtr.Zero&&p.Responding&&(DateTime.UtcNow-p.StartTime.ToUniversalTime()).TotalSeconds>=8&&string.Equals(RuntimeFileIdentity.Canonical(p.MainModule!.FileName),RuntimeFileIdentity.Canonical(expected),StringComparison.OrdinalIgnoreCase))return new(p.Id,p.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),p.MainModule.FileName);}catch{}}return null;}
 Running Start(string group,string signature,ModItem provider,List<ModItem> items,Dictionary<string,JsonNode?> values,string directory,string entry,LauncherConfig config,RuntimeProcessInfo? server,RuntimeProcessInfo? client){
  string id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(group)))[..20].ToLowerInvariant(),token=Guid.NewGuid().ToString("N"),session=Path.Combine(root,"data","mod-runtime",id,"session-"+token),state=Path.Combine(root,"data","mod-state",id);Directory.CreateDirectory(session);Directory.CreateDirectory(state);
  string contextFile=Path.Combine(session,"context.json");var spec=provider.Manifest.Runtime!;var arguments=spec.Arguments.Count>0?spec.Arguments:spec.Type=="powershell"?new List<string>{"-ContextFile","{context}"}:new List<string>{"--context","{context}"};string executable=entry;var args=arguments.Select(a=>a.Replace("{context}",contextFile)).ToList();
  if(spec.Type=="powershell"){executable=config.PowerShell=="7"?config.PowerShell7Path:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");args=new[]{"-NoLogo","-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",entry}.Concat(args).ToList();}
  else if(spec.Type=="node"){executable=Path.Combine(root,"host","node","node.exe");args.Insert(0,entry);}
  if(!File.Exists(executable))throw new Exception("找不到模组运行程序。");
  var task=new Running{Group=group,Signature=signature,Token=token,ContextFile=contextFile,Directory=directory,StateDirectory=state};task.Context=new JsonObject{["apiVersion"]=1,["instanceId"]=token,["launcherRoot"]=root,["modDirectory"]=directory,["stateDirectory"]=state,["statusFile"]=Path.Combine(session,"status.json"),["stopFile"]=Path.Combine(session,"stop.request"),["logFile"]=Path.Combine(session,"runtime.log"),["actionDirectory"]=Path.Combine(session,"actions"),["launcher"]=JsonSerializer.SerializeToNode(new{pid=Environment.ProcessId,startTicks=Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture)},Repository.Json),["launch"]=JsonSerializer.SerializeToNode(new{executable,arguments=args,workingDirectory=directory},Repository.Json)};
  UpdateContext(task,provider,items,values,server,client);var start=new ProcessStartInfo(Path.Combine(root,"host","node","node.exe")){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root};start.ArgumentList.Add(Path.Combine(root,"engine","external-runtime.mjs"));start.ArgumentList.Add(contextFile);start.Environment["BF4_LAUNCHER_PS"]=config.PowerShell=="7"?config.PowerShell7Path:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");start.Environment.Remove("PSModulePath");task.Wrapper=Process.Start(start)??throw new Exception("模组程序没有启动。");log("加载外部模组："+provider.Name+" "+provider.Version);return task;
 }
 void UpdateContext(Running task,ModItem provider,List<ModItem> items,Dictionary<string,JsonNode?> values,RuntimeProcessInfo? server,RuntimeProcessInfo? client){task.Configuration=JsonSerializer.Serialize(values,Repository.Json);task.Context["heartbeatAt"]=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();task.Context["providerKey"]=provider.Key;task.Context["group"]=task.Group;task.Context["contributors"]=JsonSerializer.SerializeToNode(items.Select(m=>m.Key),Repository.Json);task.Context["config"]=JsonSerializer.SerializeToNode(values,Repository.Json);task.Context["server"]=JsonSerializer.SerializeToNode(server,Repository.Json);task.Context["client"]=JsonSerializer.SerializeToNode(client,Repository.Json);Repository.Atomic(task.ContextFile,task.Context.ToJsonString(Repository.Json));}
 string ReadFeedback(Running task,Dictionary<string,JsonNode?> values){
  var status=(string)task.Context["statusFile"]!;if(File.Exists(status))try{var node=JsonNode.Parse(RuntimeFileIdentity.ReadShared(status));if(node?["instanceId"]?.ToString()==task.Token&&Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-(node["at"]?.GetValue<long>()??0))<15000){if(node["state"]?.ToString()=="error")return node["message"]?.ToString()??"模组运行未完成";if(node["config"]!=null&&!JsonNode.DeepEquals(node["config"],JsonSerializer.SerializeToNode(values,Repository.Json)))return "正在应用模组配置";return node["message"]?.ToString()??"外部模组已运行";}}catch{}
  var processFile=Path.Combine(Path.GetDirectoryName(task.ContextFile)!,"process.json");if(File.Exists(processFile))try{var node=JsonNode.Parse(RuntimeFileIdentity.ReadShared(processFile));if(node?["instanceId"]?.ToString()==task.Token&&node["phase"]?.ToString() is "exited" or "error")return node["message"]?.ToString()??"模组程序已退出，查看运行日志。";}catch{}
  return "外部模组加载中";
 }
 async Task Stop(Running task){try{Repository.Atomic((string)task.Context["stopFile"]!,"stop");if(task.Wrapper!=null&&!task.Wrapper.HasExited){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(9));try{await task.Wrapper.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){task.Wrapper.Kill(entireProcessTree:true);await task.Wrapper.WaitForExitAsync();}}}finally{task.Wrapper?.Dispose();}log("停止外部模组："+task.Group);}
}
