using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
namespace BF4Launcher;
public partial class Backend {
 readonly SemaphoreSlim localToolGate=new(1,1);
 async Task<JsonNode?> RunLocalModActionAsync(ModItem item,ModToolAction action,JsonNode? body) {
  await localToolGate.WaitAsync().ConfigureAwait(false);
  string? session=null;
  try {
   ExternalModSpec.Validate(item.Manifest);var runtime=item.Manifest.Tool?.LocalRuntime??throw new Exception("本地操作缺少运行入口。");
   string directory=Path.GetFullPath(Path.Combine(repo.Root,item.Directory));string expected=Path.GetFullPath(Path.Combine(repo.Root,"mods",item.Manifest.Id,item.Version));if(!string.Equals(directory,expected,StringComparison.OrdinalIgnoreCase))throw new Exception("模组目录不正确。");
   string entry=ExternalModSpec.EntryPath(directory,runtime),requestId=Guid.NewGuid().ToString("N");
   session=Path.Combine(repo.Root,"data","mod-tools",requestId);Directory.CreateDirectory(session);
   string context=Path.Combine(session,"context.json"),resultFile=Path.Combine(session,"result.json");
   string group=ExternalModSpec.Group(item),id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(group)))[..20].ToLowerInvariant();var state=Path.Combine(repo.Root,"data","mod-state",id);Directory.CreateDirectory(state);
   Repository.Atomic(context,JsonSerializer.Serialize(new{apiVersion=1,mode="local-action",requestId,action=action.Id,launcherRoot=repo.Root,moduleDirectory=directory,modKey=item.Key,stateDirectory=state,resultFile,body=body?.DeepClone()??new JsonObject()},Repository.Json));
   string executable=runtime.Type switch{"node"=>Path.Combine(Host,"node","node.exe"),"powershell"=>PowerShellPath,_=>entry};
   var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
   if(runtime.Type=="node")start.ArgumentList.Add(entry);
   if(runtime.Type=="powershell")foreach(var arg in new[]{"-NoLogo","-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",entry})start.ArgumentList.Add(arg);
   var arguments=runtime.Arguments.Count>0?runtime.Arguments:runtime.Type=="powershell"?new(){"-ContextFile","{context}"}:new System.Collections.Generic.List<string>{"--context","{context}"};
   foreach(var arg in arguments)start.ArgumentList.Add(arg.Replace("{context}",context));
   foreach(var key in start.Environment.Keys.Where(k=>k.Equals("PSModulePath",StringComparison.OrdinalIgnoreCase)).ToArray())start.Environment.Remove(key);
   using var process=new Process{StartInfo=start};process.Start();var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
   using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
   try{await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);}catch(OperationCanceledException){try{process.Kill(true);}catch{}throw new Exception("本地操作超时，请重试。");}
   string output=await stdout.ConfigureAwait(false),error=await stderr.ConfigureAwait(false);
   var result=File.Exists(resultFile)?JsonNode.Parse(RuntimeFileIdentity.ReadShared(resultFile)):null;
   if(result?["requestId"]?.ToString()!=requestId||result?["apiVersion"]?.GetValue<int>()!=1)throw new Exception(error.Length>0?error:output.Length>0?output:"本地操作没有返回结果。");
   if(process.ExitCode!=0||result["ok"]?.GetValue<bool>()!=true)throw new Exception(result["error"]?.ToString()??"本地操作未完成。");
   return result["value"]?.DeepClone();
  }finally{
   if(session!=null){foreach(var name in new[]{"context.json","result.json"})try{File.Delete(Path.Combine(session,name));}catch{}try{Directory.Delete(session,false);}catch{}}
   localToolGate.Release();
  }
 }
}
