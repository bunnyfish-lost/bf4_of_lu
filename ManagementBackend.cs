using System;using System.IO;using System.Linq;using System.Text.Json;using System.Text.Json.Nodes;using System.Threading.Tasks;
namespace BF4Launcher;
public partial class Backend {
 public async Task<JsonNode?> ManagementAsync(string action,JsonNode? body=null){
  if(!Initialized&&action=="state")return new JsonObject{["initialized"]=false,["online"]=false,["players"]=new JsonArray(),["maps"]=new JsonArray()};
  var directory=Path.Combine(repo.Root,"data","management-requests");Directory.CreateDirectory(directory);var file=Path.Combine(directory,Guid.NewGuid().ToString("N")+".json");Repository.Atomic(file,JsonSerializer.Serialize(new{action,body},Repository.Json));
  try{var output=await Run(Path.Combine(Host,"node","node.exe"),action=="state"?new[]{Path.Combine(repo.Root,"engine","management.mjs"),file,"snapshot"}:new[]{Path.Combine(repo.Root,"engine","management.mjs"),file},90);var line=output.Split('\n',StringSplitOptions.RemoveEmptyEntries).LastOrDefault(x=>x.StartsWith("@RESULT "));if(line==null)throw new Exception("管理操作没有返回结果。");return JsonNode.Parse(line[8..]);}finally{try{File.Delete(file);}catch{}}
 }
}
