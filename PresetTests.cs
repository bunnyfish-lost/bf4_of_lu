using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace BF4Launcher;
public static class PresetTests {
 public static void Run(string root,Action<bool,string> check){
  var directory=Path.Combine(root,"preset");Directory.CreateDirectory(Path.Combine(directory,"engine"));
  var field=new FieldDef{Id="serverPreset",Type="select",DefaultValue=JsonValue.Create("CUSTOM"),Options=new(){new(){Label="自定义",Value=JsonValue.Create("CUSTOM")},new(){Label="普通",Value=JsonValue.Create("NORMAL")},new(){Label="硬核",Value=JsonValue.Create("HARDCORE")}}};
  Repository.Atomic(Path.Combine(directory,"engine","server-fields.json"),JsonSerializer.Serialize(new[]{field},Repository.Json));
  var repo=new Repository(directory);repo.Load();check(repo.Config.Server["serverPreset"]!.GetValue<string>()=="CUSTOM","preset-new-install-default-custom");
  repo.Config.Server["serverPreset"]=JsonValue.Create("HARDCORE");repo.Plan();repo.Save();var reload=new Repository(directory);reload.Load();check(reload.Config.Server["serverPreset"]!.GetValue<string>()=="HARDCORE","preset-string-selection-save-reload");
  repo.Config.Server["serverPreset"]=JsonValue.Create("INVALID");bool invalid=false;try{repo.Plan();}catch{invalid=true;}check(invalid,"preset-invalid-choice-rejected");
  var startup=Path.Combine(directory,"host","runtime","GameServer","Instance","Admin","Startup.txt");Directory.CreateDirectory(Path.GetDirectoryName(startup)!);Repository.Atomic(startup,"#vars.preset NORMAL true\r\nvars.preset CUSTOM false\r\nvars.preset HARDCORE true\r\n");
  check(ServerPresetLogic.ReadFile(startup)=="HARDCORE","preset-parser-last-command-crlf");
  reload.Config.Server.Remove("serverPreset");reload.Save();var migrated=new Repository(directory);migrated.Load();check(migrated.Config.Server["serverPreset"]!.GetValue<string>()=="HARDCORE","preset-upgrade-imports-existing-managed-file");
  migrated.Config.Server["serverPreset"]=JsonValue.Create("NORMAL");migrated.Save();var preserved=new Repository(directory);preserved.Load();check(preserved.Config.Server["serverPreset"]!.GetValue<string>()=="NORMAL","preset-saved-choice-not-overwritten-by-stale-file");
  check(ServerPresetLogic.ReadFile(Path.Combine(directory,"missing.txt"))=="CUSTOM","preset-missing-file-default-custom");
  check(ServerPresetLogic.ControlledFields.Contains("health")&&!ServerPresetLogic.ControlledFields.Contains("ammoMode")&&!ServerPresetLogic.ControlledFields.Contains("initialBots"),"preset-only-controls-related-rule-editors");
 }
}

