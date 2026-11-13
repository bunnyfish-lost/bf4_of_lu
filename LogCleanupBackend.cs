using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
namespace BF4Launcher;
public partial class Backend {
 public string LastLogCleanupMessage{get;private set;}="";
 public async Task CleanupLogsAsync(string scope="all"){
  if(!repo.Config.AutoCleanLogs)return;
  Report("清理本实例运行日志");
  try{
   var start=new ProcessStartInfo(Path.Combine(Host,"node","node.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=repo.Root,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
   start.ArgumentList.Add(Path.Combine(repo.Root,"engine","log-cleanup.mjs"));start.ArgumentList.Add("--scope");start.ArgumentList.Add(scope);
   using var process=new Process{StartInfo=start};process.Start();var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
   try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){try{process.Kill();}catch{}throw new Exception("日志清理超时，未完成的文件已保留。");}
   var output=await stdout;var error=await stderr;if(process.ExitCode!=0)throw new Exception(string.IsNullOrWhiteSpace(error)?"日志清理程序未完成。":error.Trim());
   var line=output.Split('\n',StringSplitOptions.RemoveEmptyEntries).LastOrDefault();var result=line==null?null:JsonNode.Parse(line);if(result==null)throw new Exception("日志清理没有返回结果。");
   var state=result["state"]?.ToString();if(state=="cleaned"){
    int count=result["deletedFiles"]?.GetValue<int>()??0;long bytes=result["freedBytes"]?.GetValue<long>()??0;int skipped=result["skippedFiles"]?.GetValue<int>()??0;int errors=result["errors"]?.AsArray().Count??0;
    LastLogCleanupMessage=$"自动清理日志：{count} 个文件，释放 {bytes/1024d/1024d:0.0} MiB。";if(skipped>0||errors>0)LastLogCleanupMessage+=" 部分文件已保留。";
   }else LastLogCleanupMessage=result["reason"]?.ToString()??"日志文件已保留。";
  }catch(Exception ex){LastLogCleanupMessage="日志清理未完成："+ex.Message;}
  Output?.Invoke(LastLogCleanupMessage);
 }
}
