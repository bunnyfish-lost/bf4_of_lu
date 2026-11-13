using System;using System.IO;using System.Text.Json;
namespace BF4Launcher;
public static class LogCleanupTests {
 public static void Run(string root,Action<bool,string> check){
  check(!new LauncherConfig().AutoCleanLogs,"log-cleanup-default-off");
  check(!JsonSerializer.Deserialize<LauncherConfig>("{\"schemaVersion\":1}",Repository.Json)!.AutoCleanLogs,"old-config-without-option-remains-off");
  var config=new LauncherConfig{AutoCleanLogs=true};var json=JsonSerializer.Serialize(config,Repository.Json);check(JsonDocument.Parse(json).RootElement.GetProperty("autoCleanLogs").GetBoolean(),"cleanup-option-json-name-and-save");
  check(JsonSerializer.Deserialize<LauncherConfig>(json,Repository.Json)!.AutoCleanLogs,"cleanup-option-reloads-on");
  config.AutoCleanLogs=false;check(!JsonSerializer.Deserialize<LauncherConfig>(JsonSerializer.Serialize(config,Repository.Json),Repository.Json)!.AutoCleanLogs,"cleanup-option-reloads-off");
 }
}
