using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
namespace BF4Launcher;
public class FieldDef {
 public string Id{get;set;}="";public string Label{get;set;}="";public string Group{get;set;}="";public string Type{get;set;}="text";public string Hint{get;set;}="";public string Unit{get;set;}="";
 public double? Min{get;set;}public double? Max{get;set;}public bool Locked{get;set;}public bool Hidden{get;set;}public JsonNode? DefaultValue{get;set;}public List<Choice> Options{get;set;}=new();
}
public class Choice {public string Label{get;set;}="";public JsonNode? Value{get;set;}public override string ToString()=>Label;}
public class Operation { public string Field{get;set;}="";public JsonNode? Value{get;set;}public string When{get;set;}=""; }
public class ModManifest {
 public int FormatVersion{get;set;}=1;public string Id{get;set;}="";public string Name{get;set;}="";public string Version{get;set;}="";public string Author{get;set;}="";public string Side{get;set;}="client";public string Kind{get;set;}="config";public string Adapter{get;set;}="config";public string Description{get;set;}="";public List<FieldDef> Fields{get;set;}=new();public List<Operation> Operations{get;set;}=new();public List<string> Dependencies{get;set;}=new();public ExternalRuntimeSpec? Runtime{get;set;}public ModToolSpec? Tool{get;set;}
}
public class ModItem:INotifyPropertyChanged {
 public string Key{get;set;}="";public string Directory{get;set;}="";public ModManifest Manifest{get;set;}=new();public Dictionary<string,JsonNode?> Config{get;set;}=new();
 private bool enabled;private string? runtimeStatus;public bool Enabled{get=>enabled;set{if(enabled!=value)runtimeStatus=null;enabled=value;Changed(nameof(Enabled));Changed(nameof(Status));}}
 private bool hasFiles=true;[JsonIgnore] public bool HasFiles{get=>hasFiles;set{if(hasFiles==value)return;hasFiles=value;Changed(nameof(HasFiles));}}
 private int versionCount=1;[JsonIgnore] public int VersionCount{get=>versionCount;set{if(versionCount==value)return;versionCount=value;Changed(nameof(VersionCount));Changed(nameof(VersionDisplay));}}[JsonIgnore] public string VersionDisplay=>Version+(VersionCount>1?" · "+VersionCount+" 个版本":"");
 [JsonIgnore] public string Name=>Manifest.Name;[JsonIgnore] public string Version=>Manifest.Version;
 [JsonIgnore] public string Tags=>SideLabel(Manifest.Side)+"  ·  "+(Manifest.Kind=="runtime"?"运行时补丁":Manifest.Kind=="service"?"后台功能":"配置预设 · 覆盖基础设置");
 [JsonIgnore] public string Description=>Manifest.Description;
 [JsonIgnore] public string Status=>!Enabled?(Manifest.Kind=="config"?"未启用 · 使用服务端基础值":"未启用"):runtimeStatus??(Manifest.Kind=="config"?"已启用 · 下次开服覆盖基础值":Manifest.Side=="server"?"已启用 · 随服务端加载":"已启用 · 随游戏加载");
 public void UpdateRuntime(string? value){if(runtimeStatus!=value){runtimeStatus=value;Changed(nameof(Status));}}
 public static string SideLabel(string s)=>s switch{"server"=>"服务端","both"=>"双端","local"=>"本机工具",_=>"客户端"};
 public event PropertyChangedEventHandler? PropertyChanged;void Changed(string p)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(p));
}
public class MapChoice {public string Id{get;set;}="";public string Label{get;set;}="";public List<string> Modes{get;set;}=new();public override string ToString()=>Label;}
public class RotationEntry:INotifyPropertyChanged {
 private string map="MP_Prison",mode="ConquestLarge0";private int rounds=1;
 public string Map{get=>map;set{if(string.IsNullOrWhiteSpace(value)||map==value)return;map=value;Changed(nameof(Map));}}
 [JsonIgnore] public List<MapChoice> AllMaps{get;set;}=new();
 [JsonIgnore] public IEnumerable<MapChoice> AvailableMaps=>AllMaps.Where(m=>m.Modes.Count==0||m.Modes.Contains(Mode));
 public string Mode{get=>mode;set{if(string.IsNullOrWhiteSpace(value)||mode==value)return;mode=value;Changed(nameof(Mode));Changed(nameof(AvailableMaps));if(AllMaps.Count>0&&!AvailableMaps.Any(m=>m.Id==Map))Map=AvailableMaps.FirstOrDefault()?.Id??Map;}}
 public int Rounds{get=>rounds;set{if(rounds==value)return;rounds=value;Changed(nameof(Rounds));}}
 public RotationEntry Clone()=>new(){Map=Map,Mode=Mode,Rounds=Rounds};
 public event PropertyChangedEventHandler? PropertyChanged;void Changed(string name)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
}
public class LauncherConfig {
 public int SchemaVersion{get;set;}=2;public List<ModGroupState>? ModGroups{get;set;}public string SourceServer{get;set;}="";public string GameDirectory{get;set;}="";public string PlayerId{get;set;}="LocalPlayer";public string PowerShell{get;set;}="5.1";public string PowerShell7Path{get;set;}="";public bool AutoCleanLogs{get;set;}=false;
 public List<RotationEntry>? Rotation{get;set;}
 public Dictionary<string,JsonNode?> Server{get;set;}=new();public ObservableCollection<ModItem> Mods{get;set;}=new();
}
public partial class Repository {
 public static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true,WriteIndented=true};
 public string Root{get;}public string ConfigPath=>Path.Combine(Root,"config.json");public LauncherConfig Config{get;private set;}=new();public List<FieldDef> Fields{get;private set;}=new();public List<MapChoice> Maps{get;private set;}=new();public List<MapChoice> Modes{get;private set;}=new();
 public Repository(string root){Root=Path.GetFullPath(root);Directory.CreateDirectory(Root);}
 public void Load(){
  bool migrateGroups=false;bool fresh=!File.Exists(ConfigPath);var originalIds=new HashSet<string>(StringComparer.Ordinal);
  string fields=Path.Combine(Root,"engine","server-fields.json");if(File.Exists(fields))Fields=JsonSerializer.Deserialize<List<FieldDef>>(File.ReadAllText(fields),Json)??new();
  var mapFile=Path.Combine(Root,"engine","map-catalog.json");if(File.Exists(mapFile))Maps=JsonSerializer.Deserialize<List<MapChoice>>(File.ReadAllText(mapFile),Json)??new();
  var modeFile=Path.Combine(Root,"engine","map-modes.json");Modes=File.Exists(modeFile)?JsonSerializer.Deserialize<List<MapChoice>>(File.ReadAllText(modeFile),Json)??new():new(){new MapChoice{Id="ConquestLarge0",Label="大型征服"}};
  if(File.Exists(ConfigPath)){Config=JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(ConfigPath),Json)??throw new Exception("config.json 内容为空");if(Config.SchemaVersion>2)throw new Exception("此配置由更新版本的启动器创建。");originalIds=Config.Mods.Select(m=>m.Manifest.Id).ToHashSet(StringComparer.Ordinal);migrateGroups=Config.SchemaVersion<2||Config.ModGroups==null;PrepareModGroupMigration();ModSelectionPolicy.Apply(Config);}
  else {
   Config.SourceServer=Directory.Exists(@"D:\Battlefield 4\Server")?@"D:\Battlefield 4\Server":"";Config.GameDirectory=Directory.Exists(@"D:\Battlefield 4")?@"D:\Battlefield 4":"";
   Config.PowerShell7Path=FindPs7();
   foreach(var f in Fields)Config.Server[f.Id]=f.DefaultValue?.DeepClone();

   Save();
  }
  if(Fields.Any(f=>f.Id=="serverPreset")&&!Config.Server.ContainsKey("serverPreset")){var startup=Path.Combine(Root,"host","runtime","GameServer","Instance","Admin","Startup.txt");Config.Server["serverPreset"]=JsonValue.Create(ServerPresetLogic.ReadFile(startup));}
  foreach(var f in Fields)if(!Config.Server.ContainsKey(f.Id))Config.Server[f.Id]=f.DefaultValue?.DeepClone();
  LoadRotation();
  bool missingFilesChanged=UpdateModFileAvailability();
  MigrateRedundantPresets();
  UpdateBuiltinDescriptions();
  MergeWeaponModules();
  RefreshModDirectory();
  ApplyExternalLibraryDefaults(fresh,originalIds);
  if(migrateGroups||missingFilesChanged)Save();
 }
 void LoadRotation(){
  if(Config.Rotation!=null)return;
  var latest=Path.Combine(Root,"data","map-rotation.latest.json");
  if(File.Exists(latest)){try{Config.Rotation=JsonSerializer.Deserialize<List<RotationEntry>>(File.ReadAllText(latest),Json);}catch{}}
  if(Config.Rotation==null){var managed=Path.Combine(Root,"host","runtime","GameServer","Instance","Admin","MapList.txt");var template=Path.Combine(Root,"host","templates","MapList.txt");Config.Rotation=ReadRotation(File.Exists(managed)?managed:template);}
  if(Config.Rotation.Count==0)Config.Rotation=new(){new RotationEntry()};Save();
 }
 public static List<RotationEntry> ReadRotation(string file){
  var rows=new List<RotationEntry>();if(!File.Exists(file))return rows;
  foreach(var line in File.ReadAllLines(file)){var text=line.Trim().TrimStart('\uFEFF');if(text.Length==0||text.StartsWith('#')||text.StartsWith("//")||text.StartsWith(';'))continue;var parts=Regex.Split(text,@"\s+");if(parts.Length<3)continue;int.TryParse(parts[2],out int rounds);rows.Add(new RotationEntry{Map=parts[0],Mode=parts[1],Rounds=rounds});}return rows;
 }
 public void ValidateRotation(IReadOnlyList<RotationEntry> rows){
  if(rows.Count<1||rows.Count>32)throw new Exception("地图轮换需要 1～32 项。");
  foreach(var row in rows){if(!Maps.Any(m=>m.Id==row.Map))throw new Exception("请选择地图目录中的地图："+row.Map);if(!Modes.Any(m=>m.Id==row.Mode))throw new Exception("请选择当前支持的模式。");var selected=Maps.First(m=>m.Id==row.Map);if(selected.Modes.Count>0&&!selected.Modes.Contains(row.Mode))throw new Exception(selected.Label+" 不支持所选模式。");if(row.Rounds<1||row.Rounds>10)throw new Exception("每项地图的回合数为 1～10。");}
 }
 public void SetRotation(IReadOnlyList<RotationEntry> rows){ValidateRotation(rows);Config.Rotation=rows.Select(r=>r.Clone()).ToList();Save();Atomic(Path.Combine(Root,"data","map-rotation.latest.json"),JsonSerializer.Serialize(Config.Rotation,Json));}
 void MigrateRedundantPresets(){
  var marker=Path.Combine(Root,"data","migrations","presets-to-settings-0.1.1.json");
  if(!File.Exists(marker)){
   var remove=Config.Mods.Where(m=>m.Manifest.Author=="本地模组库"&&m.Manifest.Adapter=="config"&&new[]{"ammo-mode","quick-respawn"}.Contains(m.Manifest.Id)).ToList();
   var changed=new Dictionary<string,JsonNode?>();
   if(remove.Count>0){
    Repository.Atomic(Path.Combine(Root,"data","migrations","config-before-presets-0.1.1.json"),JsonSerializer.Serialize(Config,Json));
    try{var plan=Plan();var owners=new Dictionary<string,string>();foreach(var mod in Config.Mods.Where(m=>m.Enabled))foreach(var op in mod.Manifest.Operations){if(op.When!=""&&mod.Config.GetValueOrDefault(op.When)?.GetValue<bool>()!=true)continue;owners[op.Field]=mod.Key;}
     foreach(var pair in owners.Where(o=>remove.Any(m=>m.Key==o.Value))){Config.Server[pair.Key]=plan.Values[pair.Key]?.DeepClone();changed[pair.Key]=plan.Values[pair.Key]?.DeepClone();}
    }catch{}
    foreach(var mod in remove)Remove(mod);
   }
   Repository.Atomic(marker,JsonSerializer.Serialize(new{removed=remove.Select(m=>m.Key),preservedBaseValues=changed,at=DateTime.UtcNow},Json));
  }
  var library=Path.Combine(Root,"engine","builtin-mods");if(Directory.Exists(library))foreach(var file in Directory.GetFiles(library,"*.zip").Where(f=>Path.GetFileName(f).Contains("-ammo-mode-")||Path.GetFileName(f).Contains("-quick-respawn-"))){var archive=Path.Combine(Root,"data","removed-builtins");Directory.CreateDirectory(archive);File.Move(file,Path.Combine(archive,Guid.NewGuid().ToString("N")[..8]+"-"+Path.GetFileName(file)));}
 }
 void UpdateBuiltinDescriptions(){
  var file=Path.Combine(Root,"engine","builtin-metadata.json");if(!File.Exists(file))return;
  var metadata=JsonSerializer.Deserialize<List<ModManifest>>(File.ReadAllText(file),Json)??new();bool changed=false;
  foreach(var item in Config.Mods.Where(m=>m.Manifest.Author=="本地模组库")){
   var info=metadata.FirstOrDefault(m=>m.Id==item.Manifest.Id&&m.Version==item.Manifest.Version);if(info==null)continue;
   if(item.Manifest.Name!=info.Name){item.Manifest.Name=info.Name;changed=true;}if(item.Manifest.Description!=info.Description){item.Manifest.Description=info.Description;changed=true;}
   foreach(var field in item.Manifest.Fields){var definition=info.Fields.FirstOrDefault(f=>f.Id==field.Id);if(definition==null)continue;if(field.Label!=definition.Label){field.Label=definition.Label;changed=true;}if(field.Hint!=definition.Hint){field.Hint=definition.Hint;changed=true;}}
  }
  if(changed)Save();
 }
 void MergeWeaponModules(){
  var marker=Path.Combine(Root,"data","migrations","merge-weapon-mods-0.1.3.json");
  if(!File.Exists(marker)){
   var targets=Config.Mods.Where(m=>m.HasFiles&&m.Manifest.Author=="本地模组库"&&m.Manifest.Adapter=="player-effects"&&m.Manifest.Version=="1.0.0"&&new[]{"no-spread","no-recoil","ballistics"}.Contains(m.Manifest.Id)).ToList();
   if(targets.Count>0){
    Atomic(Path.Combine(Root,"data","migrations","config-before-weapon-merge-0.1.3.json"),JsonSerializer.Serialize(Config,Json));
    var keys=new[]{"noSpread","noRecoil","noBulletDrop","fastBullets"};var values=keys.ToDictionary(k=>k,k=>(JsonNode?)JsonValue.Create(false));
    foreach(var item in Config.Mods.Where(m=>m.Enabled&&m.Manifest.Adapter=="player-effects")){var fields=item.Manifest.Fields.Select(f=>f.Id).ToHashSet();foreach(var key in keys)if(fields.Contains(key)&&item.Config.TryGetValue(key,out var value))values[key]=JsonValue.Create(string.Equals(value?.ToString(),"true",StringComparison.OrdinalIgnoreCase));}
    bool enabled=targets.Any(m=>m.Enabled);int last=Config.Mods.IndexOf(targets.Last());int position=Config.Mods.Take(last+1).Count(m=>!targets.Contains(m));
    var existing=Config.Mods.FirstOrDefault(m=>m.Manifest.Id=="weapon-tuning"&&m.Manifest.Version=="1.0.0");
    ModItem? combined=null;
    if(existing==null){
     var template=Path.Combine(Root,"engine","weapon-tuning-manifest.json");
     var manifest=JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(template),Json)??throw new Exception("枪械模组说明没有读取成功");
     if(manifest.Id!="weapon-tuning"||manifest.Version!="1.0.0"||manifest.Adapter!="player-effects"||!keys.All(k=>manifest.Fields.Any(f=>f.Id==k)))throw new Exception("枪械模组说明不完整");
     var relative=Path.Combine("mods",manifest.Id,manifest.Version);var dir=Path.Combine(Root,relative);Directory.CreateDirectory(dir);
     if(!File.Exists(Path.Combine(dir,"mod.json")))File.Copy(template,Path.Combine(dir,"mod.json"));
     combined=new ModItem{Key=manifest.Id+"@"+manifest.Version,Directory=relative,Manifest=manifest,Enabled=enabled,Config=values};
    }
    var moves=targets.Select(target=>{
     var source=Path.GetFullPath(Path.Combine(Root,target.Directory));var mods=Path.GetFullPath(Path.Combine(Root,"mods"))+Path.DirectorySeparatorChar;
     if(!source.StartsWith(mods,StringComparison.OrdinalIgnoreCase))throw new Exception("模组路径不正确");return (target,source);
    }).ToList();
    foreach(var target in targets)Config.Mods.Remove(target);
    if(combined!=null)Config.Mods.Insert(Math.Min(position,Config.Mods.Count),combined);
    Save();
    Atomic(marker,JsonSerializer.Serialize(new{merged=targets.Select(m=>m.Key),enabled,effectiveValues=values,existingCombinedPreserved=existing!=null,at=DateTime.UtcNow},Json));
    foreach(var (target,source) in moves)if(Directory.Exists(source)){
     var archive=Path.Combine(Root,"data","removed-mods",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]);Directory.CreateDirectory(archive);
     Directory.Move(source,Path.Combine(archive,"files"));Atomic(Path.Combine(archive,"config.json"),JsonSerializer.Serialize(target,Json));
    }
   }else Atomic(marker,JsonSerializer.Serialize(new{merged=Array.Empty<string>(),at=DateTime.UtcNow},Json));
  }
  var library=Path.Combine(Root,"engine","builtin-mods");if(Directory.Exists(library))foreach(var file in Directory.GetFiles(library,"*.zip").Where(f=>new[]{"-no-spread-","-no-recoil-","-ballistics-"}.Any(id=>Path.GetFileName(f).Contains(id)))){var archive=Path.Combine(Root,"data","removed-builtins");Directory.CreateDirectory(archive);File.Move(file,Path.Combine(archive,Guid.NewGuid().ToString("N")[..8]+"-"+Path.GetFileName(file)));}
 }
 public static string FindPs7(){var p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PowerShell","7","pwsh.exe");if(File.Exists(p))return p;return "";}
 public void Save(){PrepareModGroupMigration();AdoptDirectModEdits();ModSelectionPolicy.Apply(Config);SaveVersionConfigs();Directory.CreateDirectory(Path.Combine(Root,"data"));if(File.Exists(ConfigPath))File.Copy(ConfigPath,Path.Combine(Root,"data","config.previous.json"),true);Atomic(ConfigPath,JsonSerializer.Serialize(Config,Json));}
 public static void Atomic(string path,string content){Directory.CreateDirectory(Path.GetDirectoryName(path)!);var tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(tmp,content,new System.Text.UTF8Encoding(false));File.Move(tmp,path,true);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
 public ModItem Import(string zip,bool save=true){
  using var archive=ZipFile.OpenRead(zip);if(archive.Entries.Count>1000)throw new Exception("模组包文件数量过多");
  if(archive.Entries.Sum(x=>x.Length)>256L*1024*1024)throw new Exception("模组包超过 256 MB");
  foreach(var e in archive.Entries){var n=e.FullName.Replace('\\','/');if(n.StartsWith('/')||n.Contains(':')||n.Split('/').Any(s=>s=="..")||((e.ExternalAttributes>>16)&0xF000)==0xA000)throw new Exception("模组包包含无效路径");}
  var manifest=archive.Entries.FirstOrDefault(x=>x.FullName=="mod.json")??throw new Exception("压缩包根目录需要有 mod.json");
  using var r=new StreamReader(manifest.Open());var m=JsonSerializer.Deserialize<ModManifest>(r.ReadToEnd(),Json)??throw new Exception("模组说明无法读取");
  ValidateManifest(m);
  Dictionary<string,JsonNode?>? packageConfig=null;
  var key=m.Id+"@"+m.Version;var existing=Config.Mods.FirstOrDefault(x=>x.Key==key);if(existing!=null)return existing;
  var relative=Path.Combine("mods",m.Id,m.Version);var final=Path.Combine(Root,relative);if(Directory.Exists(final))throw new Exception("该版本的文件已存在："+key);
  var staging=Path.Combine(Root,"mods",".import-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
  try{foreach(var e in archive.Entries){var dest=Path.GetFullPath(Path.Combine(staging,e.FullName.Replace('/',Path.DirectorySeparatorChar)));if(!dest.StartsWith(staging+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("模组路径超出目录");if(e.FullName.EndsWith('/')){Directory.CreateDirectory(dest);continue;}Directory.CreateDirectory(Path.GetDirectoryName(dest)!);using var input=e.Open();using var output=File.Create(dest);input.CopyTo(output);}if(m.Adapter=="external"){ExternalModSpec.EntryPath(staging,m.Runtime!);var local=Path.Combine(staging,"config.json");if(File.Exists(local)){packageConfig=JsonSerializer.Deserialize<Dictionary<string,JsonNode?>>(File.ReadAllText(local),Json)??throw new Exception("模组配置内容为空");Validate(packageConfig,m.Fields);}}Directory.CreateDirectory(Path.GetDirectoryName(final)!);Directory.Move(staging,final);}finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
  var item=new ModItem{Key=key,Directory=relative,Manifest=m};foreach(var f in m.Fields)item.Config[f.Id]=f.DefaultValue?.DeepClone();if(packageConfig!=null)foreach(var f in m.Fields)if(packageConfig.TryGetValue(f.Id,out var value))item.Config[f.Id]=value?.DeepClone();Config.Mods.Add(item);if(save)Save();return item;
 }
 public void Remove(ModItem item)=>RemoveModVersion(item);
 public (Dictionary<string,JsonNode?> Values,Dictionary<string,string> Owners) Plan(){
  AdoptDirectModEdits();ModSelectionPolicy.Apply(Config);var values=Config.Server.ToDictionary(x=>x.Key,x=>x.Value?.DeepClone());var owners=new Dictionary<string,string>();var active=Config.Mods.Where(m=>m.Enabled).ToList();
  foreach(var m in active){foreach(var dep in m.Manifest.Dependencies){if(!active.TakeWhile(x=>x!=m).Any(x=>x.Manifest.Id==dep||x.Key==dep))throw new Exception(m.Name+" 需要在它前面启用 "+dep);}
   foreach(var op in m.Manifest.Operations){if(!values.ContainsKey(op.Field))throw new Exception(m.Name+" 使用了未定义的服务端参数："+op.Field);if(op.When!=""&&m.Config.GetValueOrDefault(op.When)?.GetValue<bool>()!=true)continue;var v=op.Value;var s=v is JsonValue j&&j.TryGetValue<string>(out var str)?str:null;if(s?.StartsWith('$')==true)v=m.Config.GetValueOrDefault(s[1..])??throw new Exception("缺少模组参数："+s);values[op.Field]=v?.DeepClone();owners[op.Field]=m.Name+" "+m.Version;}
  }
  Validate(values,Fields);return(values,owners);
 }
 public static void Validate(Dictionary<string,JsonNode?> values,IEnumerable<FieldDef> fields){foreach(var f in fields){if(!values.TryGetValue(f.Id,out var value)||value==null)continue;if(f.Type=="number"){if(!double.TryParse(value.ToString(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var n)||!double.IsFinite(n)||f.Min.HasValue&&n<f.Min||f.Max.HasValue&&n>f.Max)throw new Exception(f.Label+" 的范围是 "+f.Min+"～"+f.Max+f.Unit);}if(f.Type=="select"&&f.Options.Count>0&&!f.Options.Any(o=>JsonNode.DeepEquals(o.Value,value)))throw new Exception(f.Label+" 的选项不正确");}}
}

