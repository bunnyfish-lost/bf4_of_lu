using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace BF4Launcher;
public sealed class ExternalLibraryCatalog {
 public int FormatVersion {get;set;}=1;
 public Dictionary<string,string> DefaultVersions {get;set;}=new(StringComparer.Ordinal);
}
public partial class Repository {
 bool UpdateModFileAvailability() {
  bool changed=false;
  foreach(var mod in Config.Mods) {
   string expected=Path.GetFullPath(Path.Combine(Root,"mods",mod.Manifest.Id,mod.Version));
   string actual=Path.GetFullPath(Path.Combine(Root,mod.Directory));
   bool available=string.Equals(expected,actual,StringComparison.OrdinalIgnoreCase)&&File.Exists(Path.Combine(actual,"mod.json"));
   if(mod.HasFiles!=available){mod.HasFiles=available;changed=true;}
  }
  ModSelectionPolicy.Apply(Config);
  foreach(var group in Config.ModGroups!) {
   var selected=Config.Mods.FirstOrDefault(m=>m.Manifest.Id==group.Id&&m.Version==group.SelectedVersion);
   if(selected?.HasFiles==true)continue;
   if(group.Enabled){group.Enabled=false;changed=true;}
   var available=Config.Mods.FirstOrDefault(m=>m.Manifest.Id==group.Id&&m.HasFiles);
   if(available!=null&&group.SelectedVersion!=available.Version){group.SelectedVersion=available.Version;changed=true;}
  }
  ModSelectionPolicy.Apply(Config);return changed;
 }
 bool ApplyExternalLibraryDefaults(bool fresh,HashSet<string> originalIds,bool save=true) {
  var file=Path.Combine(Root,"mods","catalog.json");if(!File.Exists(file))return false;
  try {
   var catalog=JsonSerializer.Deserialize<ExternalLibraryCatalog>(File.ReadAllText(file),Json);
   if(catalog==null||catalog.FormatVersion!=1||catalog.DefaultVersions==null)throw new JsonException("清单格式不正确");
   bool changed=false;ModSelectionPolicy.Apply(Config);
   foreach(var entry in catalog.DefaultVersions) {
    if(!fresh&&originalIds.Contains(entry.Key))continue;
    var item=VersionsOf(entry.Key).FirstOrDefault(m=>m.Version==entry.Value);if(item==null)continue;
    var group=Config.ModGroups!.First(g=>g.Id==entry.Key);
    if(group.SelectedVersion!=entry.Value||group.Enabled){group.SelectedVersion=entry.Value;group.Enabled=false;changed=true;}
   }
   if(changed){ModSelectionPolicy.Apply(Config);if(save)Save();}
   return changed;
  }catch(JsonException ex){var message="mods/catalog.json："+ex.Message;if(!ModDirectoryMessages.Contains(message))ModDirectoryMessages.Add(message);return false;}
 }
}
