using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
namespace BF4Launcher;
public partial class Backend {
 readonly Repository repo;readonly SemaphoreSlim gate=new(1,1);Process? watcher;string? watcherStopFile;
 public event Action<string>? Output;public event Action<OperationProgress>? Progress;
 void Report(string text,int step=0,int total=0)=>Progress?.Invoke(new OperationProgress{Detail=text,Step=step,Total=total});
 public string Host=>Path.Combine(repo.Root,"host");public bool Initialized { get { try { var p=JsonNode.Parse(RuntimeFileIdentity.ReadShared(Path.Combine(repo.Root,"data/environment.json")));return p?["runtimeVersion"]?.ToString()=="v2-1107"&&p?["complete"]?.GetValue<bool>()==true;}catch{return false;} } }
 public Backend(Repository repo){this.repo=repo;}
 public static bool IsAdmin{get{using var i=WindowsIdentity.GetCurrent();return new WindowsPrincipal(i).IsInRole(WindowsBuiltInRole.Administrator);}}
 public string PowerShellPath {get{if(repo.Config.PowerShell=="7"){if(!File.Exists(repo.Config.PowerShell7Path))throw new Exception("请选择 PowerShell 7 的 pwsh.exe");return repo.Config.PowerShell7Path;}return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");}}
 public async Task<string> Run(string exe,IEnumerable<string> args,int seconds=180,bool encode=true){
  var arguments=args.ToArray();var taskRoot=Path.Combine(repo.Root,"logs","tasks");Directory.CreateDirectory(taskRoot);
  var requestFile=Path.Combine(taskRoot,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".json");
  Repository.Atomic(requestFile,JsonSerializer.Serialize(new{exe,args=arguments,cwd=repo.Root,timeoutMs=seconds*1000}));
  var start=new ProcessStartInfo(Path.Combine(Host,"node","node.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=repo.Root,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
  start.ArgumentList.Add(Path.Combine(repo.Root,"engine","process-runner.mjs"));start.ArgumentList.Add(requestFile);
  start.Environment["BF4_LAUNCHER_PS"]=PowerShellPath;
  foreach(var key in start.Environment.Keys.Where(k=>k.Equals("PSModulePath",StringComparison.OrdinalIgnoreCase)).ToList())start.Environment.Remove(key);
  using var p=new Process{StartInfo=start};var frames=new StringBuilder();var errText=new StringBuilder();
  bool quiet=arguments.LastOrDefault()=="snapshot";
  p.OutputDataReceived+=(s,e)=>{if(e.Data==null)return;if(e.Data.StartsWith("@OUT ")){try{var line=JsonSerializer.Deserialize<string>(e.Data[5..]);if(line!=null&&!line.StartsWith("@RESULT ")&&!quiet)Output?.Invoke(line);}catch{}}else if(e.Data.StartsWith("@RUNNER ")){lock(frames)frames.AppendLine(e.Data[8..]);}};
  p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null){lock(errText)errText.AppendLine(e.Data);}};
  p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(seconds+15));
  try{await p.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){try{p.Kill();}catch{}throw new Exception("任务没有返回结果，请查看 logs/tasks 中的运行记录。");}
  if(p.ExitCode!=0)throw new Exception(errText.Length>0?errText.ToString().Trim():"运行任务未完成，退出码 "+p.ExitCode);
  var frame=frames.ToString().Split('\n',StringSplitOptions.RemoveEmptyEntries).LastOrDefault();if(frame==null)throw new Exception("运行任务没有返回状态。");
  var info=JsonNode.Parse(frame)!;var stdoutFile=requestFile+".stdout.log";var stderrFile=requestFile+".stderr.log";
  var stdout=await ReadSharedText(stdoutFile);var stderr=await ReadSharedText(stderrFile);
  if(info["timedOut"]?.GetValue<bool>()==true)throw new Exception("运行任务超时，服务状态可在首页查看。");
  var exitCode=info["exitCode"]!.GetValue<int>();if(exitCode!=0)throw new Exception((stderr.Length>0?stderr:stdout).Trim() is string text&&text.Length>0?text:"脚本退出码 "+exitCode);
  if(quiet)foreach(var file in new[]{requestFile,stdoutFile,stderrFile})try{File.Delete(file);}catch{}
  return stdout;
 }
 static async Task<string> ReadSharedText(string path){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);using var reader=new StreamReader(stream,Encoding.UTF8,true);return await reader.ReadToEndAsync();}
 public async Task<JsonNode?> Bridge(string action){var raw=await Run(Path.Combine(Host,"node","node.exe"),new[]{Path.Combine(repo.Root,"engine","bridge.mjs"),action},240);var line=raw.Split('\n',StringSplitOptions.RemoveEmptyEntries).LastOrDefault(x=>x.StartsWith("@RESULT "));return line==null?null:JsonNode.Parse(line[8..]);}
 public async Task Initialize(){await gate.WaitAsync();try{Report("检查来源目录与运行环境",1,5);await Task.Run(()=>EngineBootstrap.Ensure(repo.Root));repo.Save();if(!Directory.Exists(repo.Config.SourceServer))throw new Exception("请选择原版 Server 文件夹");if(!Directory.Exists(repo.Config.GameDirectory))throw new Exception("请选择 BF4 游戏目录");var result=await Bridge("initialize");Report("v2 运行环境已就绪，处理 "+(result?["repaired"]?.ToString()??"0")+" 项",5,5);}finally{gate.Release();}}
 public async Task SaveRotation(IReadOnlyList<RotationEntry> rows){await gate.WaitAsync();try{Report("保存轮换配置",1,2);repo.SetRotation(rows);if(Initialized){try{await Bridge("save-rotation");Report("地图轮换文件已保存",2,2);}catch(Exception ex){throw new Exception("轮换已保存；本实例地图文件未写入："+ex.Message);}}}finally{gate.Release();}}
 public async Task Prepare(){repo.Plan();repo.Save();await Bridge("prepare");}
 public async Task Control(string action){await gate.WaitAsync();try{LastLogCleanupMessage="";if(!Initialized&&action!="Stop")throw new Exception("先完成 v2 初始化或检查并修复运行环境。");if(action!="Stop"){Report("应用基础设置与模组配置",1,4);await Prepare();Report("启动服务，等待游戏服加载",2,4);}else Report("保存玩家与 BOT 进度",1,3);if(action is "Stop" or "RestartGame" or "Restart")await StopExternalModsAsync();bool restart=action is "RestartGame" or "Restart";bool resumeWatcher=restart&&watcher!=null;if(action=="Stop"||restart)await StopWatcherAsync();try{await Run(PowerShellPath,new[]{"-NoLogo","-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",Path.Combine(Host,"core","Host-Control.ps1"),"-Action",action},240);if(action=="Stop")await CleanupLogsAsync("all");else if(restart)await CleanupLogsAsync("launcher");Report(action=="Stop"?"服务已停止":"读取服务端状态",action=="Stop"?3:4,action=="Stop"?3:4);}finally{if(resumeWatcher)EnsureWatcher();if(action!="Stop"){EnsureWatcher();EnsureExternalMods();}}}finally{gate.Release();}}
 public async Task<JsonNode?> Snapshot(){EnsureExternalMods();if(!Initialized)return null;return await Bridge("snapshot");}
 public void EnsureWatcher(){if(watcher is {HasExited:false})return;var s=new ProcessStartInfo(Path.Combine(Host,"node","node.exe")){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=repo.Root};s.ArgumentList.Add(Path.Combine(repo.Root,"engine","watch.mjs"));s.ArgumentList.Add(Environment.ProcessId.ToString());s.Environment["BF4_LAUNCHER_PS"]=PowerShellPath;Directory.CreateDirectory(Path.Combine(repo.Root,"data"));watcherStopFile=Path.Combine(repo.Root,"data","watch-stop-"+Guid.NewGuid().ToString("N")+".request");s.Environment["BF4_WATCH_STOP_FILE"]=watcherStopFile;s.Environment.Remove("PSModulePath");watcher=Process.Start(s);}
 public void StopWatcher(){if(watcher is {HasExited:false}){try{watcher.Kill(true);}catch{}}watcher?.Dispose();watcher=null;}
 public async Task StopWatcherAsync(){if(watcher is {HasExited:false}&&watcherStopFile!=null){try{File.WriteAllText(watcherStopFile,"stop");using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(12));await watcher.WaitForExitAsync(limit.Token);}catch{}}StopWatcher();if(watcherStopFile!=null){try{File.Delete(watcherStopFile);}catch{}watcherStopFile=null;}}
 public async Task<JsonNode?> Launch(bool recover=false){await gate.WaitAsync();bool restartWatcher=false;try{
  Report("检查服务端连接与客户端权限",1,4);if(!Initialized)throw new Exception("先完成初始化并启动 BOT 服务端");if(!IsAdmin)throw new Exception("请使用右上角的“管理员运行”，再启动游戏和客户端模组。");
  var snapshot=await Snapshot();if(snapshot?["online"]?.GetValue<bool>()!=true)throw new Exception("先启动 BOT 服务端");
  Report("准备玩家配置与客户端模组",2,4);await Bridge("client-config");if(recover){restartWatcher=true;await StopWatcherAsync();}else EnsureWatcher();Report("打开 BF4 启动器并等待游戏进程",3,4);
  var req=Path.Combine(repo.Root,"data","client-launch.json");Directory.CreateDirectory(Path.GetDirectoryName(req)!);Repository.Atomic(req,JsonSerializer.Serialize(new{action=recover?"Recover":"Launch",confirmed=recover,directory=Path.Combine(Host,"mods","client"),playerId=repo.Config.PlayerId,address="127.0.0.1"}));
  string output;try{output=await Run(PowerShellPath,new[]{"-NoProfile","-NonInteractive","-STA","-ExecutionPolicy","Bypass","-File",Path.Combine(Host,"core","Launch-Client.ps1"),"-RequestFile",req},90);}catch(Exception e){SaveClientDiagnostic(new JsonObject{["state"]="error",["message"]=e.Message});throw;}var line=output.Split('\n',StringSplitOptions.RemoveEmptyEntries).LastOrDefault(l=>l.TrimStart().StartsWith("{"));var result=line==null?null:JsonNode.Parse(line);SaveClientDiagnostic(result);if(result?["error"]!=null)throw new Exception(result["error"]!.ToString());Report(ClientLaunchFeedback.From(result).Title,4,4);return result;
 }finally{try{if(restartWatcher)EnsureWatcher();}finally{gate.Release();}}}
 public async Task Shutdown(){await gate.WaitAsync();try{Report("保存进度并停止本实例服务",1,3);await CloseExternalModsAsync();await StopWatcherAsync();if(File.Exists(Path.Combine(Host,"runtime/processes.json"))){var running=await Bridge("owned-running");if(running?["running"]?.GetValue<bool>()==true)await Run(PowerShellPath,new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",Path.Combine(Host,"core","Host-Control.ps1"),"-Action","Stop"},180);await CleanupLogsAsync("all");}StopWatcher();}finally{gate.Release();}}
}

