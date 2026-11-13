using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text.Json;using System.Text.Json.Nodes;using System.Threading.Tasks;
namespace BF4Launcher;
public sealed class ModToolSpec {public string Id{get;set;}="";public string Title{get;set;}="";public List<FieldDef> Fields{get;set;}=new();public List<ModToolAction> Actions{get;set;}=new();public ExternalRuntimeSpec? LocalRuntime{get;set;}}
public sealed class ModToolAction {public string Id{get;set;}="";public string Label{get;set;}="";public string Confirm{get;set;}="";public string Execution{get;set;}="runtime";[System.Text.Json.Serialization.JsonIgnore]public bool IsLocal=>Execution=="local";}
public sealed partial class ExternalModHost {
 public async Task<JsonNode?> InvokeActionAsync(string key,string action,JsonNode? body=null){
  Running current;string dir,requestId=Guid.NewGuid().ToString("N");
  await gate.WaitAsync();try{
   var config=JsonSerializer.Deserialize<LauncherConfig>(RuntimeFileIdentity.ReadShared(Path.Combine(root,"config.json")),Repository.Json)!;ModSelectionPolicy.Apply(config);var item=config.Mods.FirstOrDefault(m=>m.Key==key)??throw new Exception("模组不存在。");if(!item.Enabled)throw new Exception("请先启用该模组。");
   string group=ExternalModSpec.Group(item);if(suspended||!running.TryGetValue(group,out current!))throw new Exception("模组尚未启动，请检查服务端、客户端与模组状态。");
   var provider=config.Mods.FirstOrDefault(m=>m.Key==current.Context["providerKey"]?.ToString())??throw new Exception("当前模组版本已变化。");if(provider.Key!=key)throw new Exception("此版本被后方版本覆盖，请选择当前生效版本进行操作。");if(provider.Manifest.Tool?.Actions.Any(a=>a.Id==action)!=true)throw new Exception("当前生效版本不支持此操作。");
   dir=current.Context["actionDirectory"]!.ToString();Directory.CreateDirectory(dir);Repository.Atomic(Path.Combine(dir,requestId+".request.json"),JsonSerializer.Serialize(new{apiVersion=1,instanceId=current.Token,requestId,action,body=body?.DeepClone(),at=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},Repository.Json));
  }finally{gate.Release();}
  string reply=Path.Combine(dir,requestId+".response.json");var deadline=DateTime.UtcNow.AddSeconds(35);
  while(DateTime.UtcNow<deadline){if(File.Exists(reply)){var result=JsonNode.Parse(RuntimeFileIdentity.ReadShared(reply))!;if(result["instanceId"]?.ToString()!=current.Token||result["requestId"]?.ToString()!=requestId)throw new Exception("模组操作回执不匹配。");if(result["ok"]?.GetValue<bool>()!=true)throw new Exception(result["error"]?.ToString()??"模组操作未完成。");return result["value"]?.DeepClone();}if(File.Exists(current.Context["stopFile"]!.ToString())||current.Wrapper.HasExited)throw new Exception("模组已停止，操作未完成。");await Task.Delay(100);}
  throw new Exception("模组操作超时，请查看模组状态。");
 }
}
public partial class Backend {
 public async Task<JsonNode?> ModActionAsync(string key,string action,JsonNode? body=null){ModSelectionPolicy.Apply(repo.Config);var item=repo.Config.Mods.FirstOrDefault(m=>m.Key==key&&m.HasFiles)??throw new Exception("模组不存在。");var definition=item.Manifest.Tool?.Actions.FirstOrDefault(a=>a.Id==action)??throw new Exception("此模组未提供该操作。");if(definition.IsLocal)return await RunLocalModActionAsync(item,definition,body);if(!item.Enabled)throw new Exception("请先启用该模组。");EnsureExternalMods();return await externalMods!.InvokeActionAsync(key,action,body);}
}
