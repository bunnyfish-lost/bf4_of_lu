using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
namespace BF4Launcher;
public static class LocalModActionTests {
 public static void Run(Action<bool,string> check) {
  var root=Path.Combine(Path.GetTempPath(),"BF4-LocalActions-"+Guid.NewGuid().ToString("N"));EngineBootstrap.Ensure(root);
  var directory=Path.Combine(root,"mods","local-tool-test","1");Directory.CreateDirectory(directory);
  var manifest=new ModManifest{Id="local-tool-test",Name="本地操作测试",Version="1",Adapter="external",Kind="runtime",Runtime=new(){Type="node",StartWhen="server",EntryPoint="runtime.mjs"},Tool=new(){LocalRuntime=new(){Type="node",EntryPoint="tools.mjs"},Actions=new(){new(){Id="preview",Label="试听",Execution="local"},new(){Id="fail",Label="错误测试",Execution="local"},new(){Id="play",Label="对局操作"}}}};
  File.WriteAllText(Path.Combine(directory,"runtime.mjs"),"import fs from 'node:fs';fs.writeFileSync('should-not-start.txt','started');");
  File.WriteAllText(Path.Combine(directory,"tools.mjs"),"import fs from 'node:fs';import path from 'node:path';const c=JSON.parse(fs.readFileSync(process.argv[process.argv.indexOf('--context')+1],'utf8'));const fail=c.action==='fail';fs.writeFileSync(c.resultFile,JSON.stringify({apiVersion:1,requestId:c.requestId,ok:!fail,error:fail?'test failure':undefined,value:{message:'local complete',body:c.body,hasServerSecrets:fs.existsSync(path.join(c.launcherRoot,'host/runtime/private.json'))}}));if(fail)process.exitCode=1;");
  Repository.Atomic(Path.Combine(directory,"mod.json"),JsonSerializer.Serialize(manifest,Repository.Json));var repo=new Repository(root);repo.Load();repo.Config.SourceServer="";repo.Config.GameDirectory="";repo.Config.PowerShell="7";repo.Config.PowerShell7Path="";repo.Save();var item=repo.ModCards.Single();var backend=new Backend(repo);string before=File.ReadAllText(repo.ConfigPath);
  check(!backend.Initialized&&!item.Enabled,"local-action-fresh-instance-needs-no-initialization-or-enable");
  var reply=Task.Run(()=>backend.ModActionAsync(item.Key,"preview",new JsonObject{["draft"]=7})).GetAwaiter().GetResult();
  check(reply?["body"]?["draft"]?.GetValue<int>()==7,"local-action-receives-current-draft");check(repo.Config.PowerShell7Path=="","node-local-action-does-not-require-powershell-setup");check(reply?["hasServerSecrets"]?.GetValue<bool>()==false,"local-action-does-not-read-or-create-server-secrets");check(!item.Enabled&&File.ReadAllText(repo.ConfigPath)==before,"local-action-does-not-save-config-or-enable-mod");
  check(typeof(Backend).GetField("externalMods",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(backend)==null,"local-action-does-not-start-external-runtime-host");check(!File.Exists(Path.Combine(directory,"should-not-start.txt")),"local-action-does-not-launch-gameplay-entry");check(!Directory.GetDirectories(Path.Combine(root,"data","mod-tools")).Any(),"local-action-cleans-one-shot-context");
  bool failed=false;try{Task.Run(()=>backend.ModActionAsync(item.Key,"fail")).GetAwaiter().GetResult();}catch(Exception ex){failed=ex.Message=="test failure";}check(failed,"local-action-returns-meaningful-errors");
  failed=false;try{Task.Run(()=>backend.ModActionAsync(item.Key,"play")).GetAwaiter().GetResult();}catch(Exception ex){failed=ex.Message.Contains("启用");}check(failed,"runtime-action-still-requires-enable");
  manifest.Tool.LocalRuntime=null;failed=false;try{ExternalModSpec.Validate(manifest);}catch{failed=true;}check(failed,"local-action-manifest-requires-explicit-entry");manifest.Tool.LocalRuntime=new(){Type="node",EntryPoint="../outside.mjs"};failed=false;try{ExternalModSpec.Validate(manifest);}catch{failed=true;}check(failed,"local-action-entry-traversal-is-rejected");
  manifest.Adapter="config";failed=false;try{ExternalModSpec.Validate(manifest);}catch{failed=true;}check(failed,"local-action-requires-external-adapter");
 }
}
