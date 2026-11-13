using System;using System.Diagnostics;using System.IO;using System.Text.Json.Nodes;
namespace BF4Launcher;
public partial class Backend {
 void SaveClientDiagnostic(JsonNode? result){try{var directory=Path.Combine(repo.Root,"data","client-diagnostics");Directory.CreateDirectory(directory);var file=Path.Combine(directory,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".json");var record=new JsonObject{["at"]=DateTimeOffset.UtcNow.ToString("o"),["result"]=result?.DeepClone()};Repository.Atomic(file,record.ToJsonString(Repository.Json));Repository.Atomic(Path.Combine(repo.Root,"data","client-startup.latest.json"),record.ToJsonString(Repository.Json));}catch{}}
}
