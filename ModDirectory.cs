using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace BF4Launcher;
public partial class Repository {
 public List<string> ModDirectoryMessages{get;}=new();readonly HashSet<string> invalidExternalConfigs=new();
 public int RefreshModDirectory(bool save=true){
  var previousIds=Config.Mods.Select(m=>m.Manifest.Id).ToHashSet(StringComparer.Ordinal);
  ModDirectoryMessages.Clear();invalidExternalConfigs.Clear();string mods=Path.Combine(Root,"mods");if(!Directory.Exists(mods)){if(UpdateModFileAvailability()&&save)Save();return 0;}int added=0;bool changed=false;
  foreach(var id in Directory.GetDirectories(mods).OrderBy(p=>p,StringComparer.Ordinal)){
   if(Path.GetFileName(id).StartsWith('.'))continue;
   foreach(var version in Directory.GetDirectories(id).OrderBy(p=>p,StringComparer.Ordinal)){
    var file=Path.Combine(version,"mod.json");if(!File.Exists(file))continue;
    try{
     var m=JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(file),Json)??throw new Exception("mod.json 内容为空");ValidateManifest(m);
     if(m.Id!=Path.GetFileName(id)||m.Version!=Path.GetFileName(version))throw new Exception("模组 ID、版本与目录名称不一致");
     if(m.Adapter=="external")ExternalModSpec.EntryPath(version,m.Runtime!);if(m.Tool?.LocalRuntime is {} localRuntime)ExternalModSpec.EntryPath(version,localRuntime);
     Dictionary<string,JsonNode?>? local=null;
     {var configFile=Path.Combine(version,"config.json");if(File.Exists(configFile)){local=JsonSerializer.Deserialize<Dictionary<string,JsonNode?>>(File.ReadAllText(configFile),Json)??throw new Exception("模组 config.json 内容为空");Validate(local,m.Fields);}}
     string key=m.Id+"@"+m.Version;var item=Config.Mods.FirstOrDefault(x=>x.Key==key);
     if(item==null){item=new ModItem{Key=key,Directory=Path.GetRelativePath(Root,version),Manifest=m};foreach(var f in m.Fields)item.Config[f.Id]=f.DefaultValue?.DeepClone();Config.Mods.Add(item);added++;changed=true;}
     else if(m.Adapter=="external"||item.Manifest.Adapter=="external"){
      if(JsonSerializer.Serialize(item.Manifest,Json)!=JsonSerializer.Serialize(m,Json)){item.Manifest=m;changed=true;}
      item.Directory=Path.GetRelativePath(Root,version);foreach(var f in m.Fields)if(!item.Config.ContainsKey(f.Id))item.Config[f.Id]=f.DefaultValue?.DeepClone();
     }
     if(local!=null){if(!JsonNode.DeepEquals(JsonSerializer.SerializeToNode(item.Config,Json),JsonSerializer.SerializeToNode(local,Json)))changed=true;item.Config=local;}var versionConfig=Path.Combine(version,"config.json");RememberVersion(item,File.Exists(versionConfig)?RuntimeFileIdentity.ReadShared(versionConfig):null);
    }catch(Exception ex){invalidExternalConfigs.Add(Path.GetFileName(id)+"@"+Path.GetFileName(version));ModDirectoryMessages.Add(Path.GetRelativePath(mods,version)+"："+ex.Message);}
   }
  }
  changed|=UpdateModFileAvailability();ModSelectionPolicy.Apply(Config);changed|=ApplyExternalLibraryDefaults(false,previousIds,false);if(save&&(changed||Config.Mods.Any(m=>m.HasFiles&&!File.Exists(Path.Combine(Root,m.Directory,"config.json")))))Save();return added;
 }
 static void ValidateManifest(ModManifest m){
  if(m.FormatVersion!=1||!System.Text.RegularExpressions.Regex.IsMatch(m.Id,"^[a-z0-9][a-z0-9.-]{0,63}$")||!System.Text.RegularExpressions.Regex.IsMatch(m.Version,"^[a-zA-Z0-9][a-zA-Z0-9.+_-]{0,39}$")||string.IsNullOrWhiteSpace(m.Name)||m.Name.Length>120)throw new Exception("模组名称、ID 或版本格式不正确");
  if(!new[]{"config","death-fast","player-effects","vision","external"}.Contains(m.Adapter))throw new Exception("当前版本尚未接入此模组类型："+m.Adapter);
  if(m.Fields.Select(f=>f.Id).Distinct().Count()!=m.Fields.Count)throw new Exception("模组配置项重复");ExternalModSpec.Validate(m);
 }
}
