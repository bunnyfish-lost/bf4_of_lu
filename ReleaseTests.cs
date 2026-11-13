using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using BF4Launcher;
namespace BF4Launcher;
public static class ReleaseTests {
 public static void Run(string root,Action<bool,string> check){
  var row=new RotationEntry{Map="MP_Siege",Mode="ConquestLarge0",Rounds=3};row.Map=null!;row.Mode="";row.Map=" ";check(row.Map=="MP_Siege"&&row.Mode=="ConquestLarge0"&&row.Rounds==3,"rotation-ignores-transient-null-selection");
  var converter=new KeepSelectionConverter();check(ReferenceEquals(converter.ConvertBack(null!,typeof(string),null!,CultureInfo.InvariantCulture),System.Windows.Data.Binding.DoNothing),"selection-converter-blocks-null");check((string)converter.ConvertBack("MP_Prison",typeof(string),null!,CultureInfo.InvariantCulture)=="MP_Prison","selection-converter-preserves-real-map");
  check(RuntimeStatusLogic.Evaluate(false,false,"",false)==RuntimeReadiness.Stopped,"readiness-stopped");
  check(RuntimeStatusLogic.Evaluate(false,true,"",false)==RuntimeReadiness.Preparing,"readiness-blaze-only");
  check(RuntimeStatusLogic.Evaluate(true,true,"PRE_GAME",true)==RuntimeReadiness.Preparing,"readiness-pre-game-not-green");
  check(RuntimeStatusLogic.Evaluate(true,true,"IN_GAME",false)==RuntimeReadiness.Preparing,"readiness-missing-bots-not-green");
  check(RuntimeStatusLogic.Evaluate(true,false,"IN_GAME",true)==RuntimeReadiness.Preparing,"readiness-missing-blaze-not-green");
  check(RuntimeStatusLogic.Evaluate(true,true,"IN_GAME",true)==RuntimeReadiness.Ready,"readiness-all-components-ready");
  var templatePath=Path.Combine(AppContext.BaseDirectory,"engine","weapon-tuning-manifest.json");
  string templateText;
  if(File.Exists(templatePath))templateText=File.ReadAllText(templatePath);else {using var payload=Assembly.GetExecutingAssembly().GetManifestResourceStream("BF4Launcher.EnginePayload")!;using var zip=new ZipArchive(payload,ZipArchiveMode.Read);using var reader=new StreamReader(zip.GetEntry("engine/weapon-tuning-manifest.json")!.Open());templateText=reader.ReadToEnd();}
  var template=JsonSerializer.Deserialize<ModManifest>(templateText,Repository.Json)!;
  check(template.Fields.Select(f=>f.Id).SequenceEqual(new[]{"noSpread","noRecoil","noBulletDrop","fastBullets"}),"combined-mod-four-independent-fields");
  check(template.Fields.All(f=>f.DefaultValue!.GetValue<bool>()==false),"combined-mod-fresh-defaults-off");
  ModItem Old(string id,bool enabled,params (string key,bool value)[] flags){var m=new ModItem{Key=id+"@1.0.0",Directory="mods/"+id+"/1.0.0",Enabled=enabled,Manifest=new(){Id=id,Name=id,Author="本地模组库",Adapter="player-effects",Kind="runtime",Version="1.0.0"}};foreach(var (key,value) in flags){m.Manifest.Fields.Add(new(){Id=key,Type="boolean",DefaultValue=JsonValue.Create(false)});m.Config[key]=JsonValue.Create(value);}return m;}
  ModItem Other(string id)=>new(){Key=id+"@2",Directory="mods/"+id+"/2",Enabled=true,Manifest=new(){Id=id,Name=id,Author="用户",Adapter="vision",Version="2"},Config=new(){["custom"]=JsonValue.Create(73)}};
  string Fixture(string name,IEnumerable<ModItem> mods,bool haveTemplate=true){var dir=Path.Combine(root,name);Directory.CreateDirectory(Path.Combine(dir,"engine"));if(haveTemplate)File.WriteAllText(Path.Combine(dir,"engine","weapon-tuning-manifest.json"),templateText);
   var cfg=new LauncherConfig{PlayerId="KeepPlayer",SourceServer="source-server",GameDirectory="source-game",PowerShell="7",PowerShell7Path="keep-pwsh",Server=new(){["respawn"]=JsonValue.Create(17)},Rotation=new(){new(){Map="MP_Siege",Rounds=3},new(){Map="MP_Prison",Rounds=2}},Mods=new ObservableCollection<ModItem>(mods)};
   foreach(var m in cfg.Mods){Directory.CreateDirectory(Path.Combine(dir,m.Directory));Repository.Atomic(Path.Combine(dir,m.Directory,"mod.json"),JsonSerializer.Serialize(m.Manifest,Repository.Json));}
   Repository.Atomic(Path.Combine(dir,"config.json"),JsonSerializer.Serialize(cfg,Repository.Json));return dir;
  }
  var fixture=Fixture("weapon-merge",new[]{Other("before"),Old("no-spread",true,("noSpread",true)),Other("between"),Old("no-recoil",false,("noRecoil",true)),Old("ballistics",true,("noBulletDrop",true),("fastBullets",false)),Other("after")});
  var repo=new Repository(fixture);repo.Load();var combined=repo.Config.Mods.Single(m=>m.Manifest.Id=="weapon-tuning");
  check(repo.Config.Mods.Select(m=>m.Manifest.Id).SequenceEqual(new[]{"before","between","weapon-tuning","after"}),"merge-keeps-priority-of-unrelated-mods");
  check(combined.Enabled&&combined.Config["noSpread"]!.GetValue<bool>()&&combined.Config["noBulletDrop"]!.GetValue<bool>(),"merge-preserves-active-effects");
  check(!combined.Config["noRecoil"]!.GetValue<bool>()&&!combined.Config["fastBullets"]!.GetValue<bool>(),"merge-does-not-enable-disabled-effects");
  check(repo.Config.Mods.Where(m=>m!=combined).All(m=>m.Enabled&&m.Config["custom"]!.GetValue<int>()==73),"merge-preserves-unrelated-settings");
  check(repo.Config.PlayerId=="KeepPlayer"&&repo.Config.PowerShell=="7"&&repo.Config.PowerShell7Path=="keep-pwsh"&&repo.Config.SourceServer=="source-server"&&repo.Config.GameDirectory=="source-game","merge-preserves-player-paths-shell");
  check(repo.Config.Server["respawn"]!.GetValue<int>()==17&&repo.Config.Rotation!.Select(m=>m.Map).SequenceEqual(new[]{"MP_Siege","MP_Prison"})&&repo.Config.Rotation![0].Rounds==3,"merge-preserves-server-and-rotation");
  check(Directory.GetDirectories(Path.Combine(fixture,"data","removed-mods")).Length==3,"merge-archives-three-old-mods");
  check(File.Exists(Path.Combine(fixture,"data","migrations","config-before-weapon-merge-0.1.3.json")),"merge-full-config-backup");
  combined.Config["fastBullets"]=JsonValue.Create(true);repo.Save();var reload=new Repository(fixture);reload.Load();check(reload.Config.Mods.Count==4&&reload.Config.Mods.Single(m=>m.Manifest.Id=="weapon-tuning").Config["fastBullets"]!.GetValue<bool>(),"merge-runs-once-and-preserves-later-edits");
  var manuallyImported=Old("no-spread",false,("noSpread",true));reload.Config.Mods.Add(manuallyImported);reload.Save();var reimport=new Repository(fixture);reimport.Load();check(reimport.Config.Mods.Any(m=>m.Manifest.Id=="no-spread"),"merge-allows-later-manual-legacy-import");
  var off=new Repository(Fixture("weapon-all-disabled",new[]{Old("no-spread",false,("noSpread",true)),Old("no-recoil",false,("noRecoil",true)),Old("ballistics",false,("noBulletDrop",true),("fastBullets",true))}));off.Load();var disabled=off.Config.Mods.Single();check(!disabled.Enabled&&disabled.Config.Values.All(v=>v!.GetValue<bool>()==false),"merge-all-disabled-remains-disabled");
  var user=Old("no-spread",true,("noSpread",true));user.Manifest.Author="自定义作者";var userRepo=new Repository(Fixture("weapon-custom",new[]{user}));userRepo.Load();check(userRepo.Config.Mods.Single().Manifest.Author=="自定义作者","merge-does-not-remove-third-party-mod");
  var explicitCombined=new ModItem{Key="weapon-tuning@1.0.0",Directory="mods/weapon-tuning/1.0.0",Manifest=template,Enabled=false,Config=new(){["noRecoil"]=JsonValue.Create(true)}};
  var existingRepo=new Repository(Fixture("weapon-existing-combined",new[]{Old("no-spread",true,("noSpread",true)),explicitCombined}));existingRepo.Load();check(existingRepo.Config.Mods.Count==1&&!existingRepo.Config.Mods[0].Enabled&&existingRepo.Config.Mods[0].Config.Count==1&&existingRepo.Config.Mods[0].Config["noRecoil"]!.GetValue<bool>(),"merge-preserves-explicit-combined-entry");
  var missing=Fixture("weapon-missing-template",new[]{Old("no-spread",true,("noSpread",true)),Old("no-recoil",true,("noRecoil",true))},false);bool rejected=false;try{new Repository(missing).Load();}catch(FileNotFoundException){rejected=true;}
  var saved=JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Path.Combine(missing,"config.json")),Repository.Json)!;check(rejected&&saved.Mods.Count==2&&saved.Mods.All(m=>Directory.Exists(Path.Combine(missing,m.Directory))),"merge-missing-template-does-not-remove-old-mods");
  var module=JsonNode.Parse("{\"state\":\"applied\",\"feedback\":{\"config\":{\"noSpread\":true,\"noRecoil\":false,\"noBulletDrop\":false,\"fastBullets\":false},\"status\":{\"noSpread\":true,\"noRecoil\":false}}}")!;
  var spread=Old("no-spread",true,("noSpread",true));check(EffectFeedbackLogic.Describe(spread,module)=="已应用到本机玩家","feedback-recognizes-own-effect");
  var recoil=Old("no-recoil",true,("noRecoil",false));check(EffectFeedbackLogic.Describe(recoil,module).Contains("默认值"),"feedback-default-not-credited-to-other-active-effect");
  recoil.Config["noRecoil"]=JsonValue.Create(true);check(EffectFeedbackLogic.Describe(recoil,module).Contains("覆盖"),"feedback-shows-later-priority-override");
  module["feedback"]!["config"]!["noRecoil"]=true;check(EffectFeedbackLogic.Describe(recoil,module).Contains("等待适用"),"feedback-waits-for-specific-native-effect");
  module["feedback"]!["status"]!["noRecoil"]=true;check(EffectFeedbackLogic.Describe(recoil,module)=="已应用到本机玩家","feedback-specific-native-effect-confirmed");
  module["state"]="error";module["message"]="fixture error";check(EffectFeedbackLogic.Describe(recoil,module)=="fixture error","feedback-shows-module-error");
 }
}
