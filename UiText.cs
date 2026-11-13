using System;using System.Linq;using System.Text.Json.Nodes;
namespace BF4Launcher;
public static class UiText {
 public static string Summary(string? raw,string fallback="操作未完成，请查看运行日志。",int limit=150){
  if(string.IsNullOrWhiteSpace(raw))return fallback;
  string text=raw.Trim();
  if(text.StartsWith("{"))try{var node=JsonNode.Parse(text);var value=node?["message"]??node?["error"];if(value!=null)text=value.ToString();}catch{}
  string[] technical={"Command failed:","-NoProfile","-NonInteractive","-Command ","System.Management.Automation","Traceback ("," at BF4Launcher.","ForEach-Object","Get-Process -Name","[Console]::OutputEncoding"};
  if(technical.Any(s=>text.Contains(s,StringComparison.OrdinalIgnoreCase)))return fallback;
  text=string.Join(" ",text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()));
  return text.Length<=limit?text:text[..Math.Max(1,limit-1)]+"…";
 }
}
