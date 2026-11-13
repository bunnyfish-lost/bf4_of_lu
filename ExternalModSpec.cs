using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
namespace BF4Launcher;
public sealed class ExternalRuntimeSpec {
 public int ApiVersion{get;set;}=1;
 public string Type{get;set;}="executable";
 public string EntryPoint{get;set;}="";
 public string Group{get;set;}="";
 public string StartWhen{get;set;}="server";
 public List<string> Arguments{get;set;}=new(){"--context","{context}"};
}
public static class ExternalModSpec {
 public static void Validate(ModManifest manifest){
  if(manifest.Adapter!="external"){if(manifest.Tool?.Actions.Any(a=>a.IsLocal)==true)throw new Exception("本地操作需要独立运行模组。");return;}
  var r=manifest.Runtime??throw new Exception("外部模组缺少 runtime 入口说明。");
  if(r.ApiVersion!=1)throw new Exception("此模组需要其他版本的模组接口。");
  if(!new[]{"executable","powershell","node"}.Contains(r.Type)||!new[]{"launcher","server","client"}.Contains(r.StartWhen))throw new Exception("模组运行方式或启动时机不正确。");
  if(manifest.Kind is not ("runtime" or "service"))throw new Exception("外部模组应使用 runtime 或 service 类型。");
  if(string.IsNullOrWhiteSpace(r.EntryPoint)||Path.IsPathRooted(r.EntryPoint)||r.EntryPoint.Contains(':')||r.EntryPoint.Replace('\\','/').Split('/').Any(p=>p is ".." or "." or ""))throw new Exception("模组入口需要使用包内相对路径。");
  if(r.Group.Length>128||r.Arguments.Count>32||r.Arguments.Any(a=>a.Length>4096||a.Contains('\0')))throw new Exception("模组入口参数过长。");
  if(manifest.Tool is {} tool){if(tool.Actions.Select(a=>a.Id).Distinct().Count()!=tool.Actions.Count||tool.Actions.Any(a=>a.Execution is not ("runtime" or "local")))throw new Exception("模组操作的名称或执行方式不正确。");if(tool.Actions.Any(a=>a.IsLocal)&&tool.LocalRuntime==null)throw new Exception("本地操作缺少 localRuntime 入口。");if(tool.LocalRuntime!=null)Validate(new ModManifest{Adapter="external",Kind="runtime",Runtime=tool.LocalRuntime});}

 }
 public static string EntryPath(string directory,ExternalRuntimeSpec runtime){
  string root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar),path=Path.GetFullPath(Path.Combine(root,runtime.EntryPoint.Replace('/',Path.DirectorySeparatorChar)));
  if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(path))throw new Exception("找不到模组入口："+runtime.EntryPoint);
  for(string? p=path;p!=null&&!string.Equals(p,root,StringComparison.OrdinalIgnoreCase);p=Path.GetDirectoryName(p))if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new Exception("模组入口不能通过链接指向包外文件。");
  string extension=Path.GetExtension(path).ToLowerInvariant();if(runtime.Type=="executable"&&extension!=".exe"||runtime.Type=="powershell"&&extension!=".ps1"||runtime.Type=="node"&&extension is not (".js" or ".mjs" or ".cjs"))throw new Exception("模组入口类型与文件扩展名不一致。");return path;
 }
 public static string Group(ModItem item)=>string.IsNullOrWhiteSpace(item.Manifest.Runtime!.Group)?item.Manifest.Id:item.Manifest.Runtime.Group;
 public static Dictionary<string,JsonNode?> Merge(IEnumerable<ModItem> mods){var values=new Dictionary<string,JsonNode?>();foreach(var mod in mods)foreach(var f in mod.Manifest.Fields)values[f.Id]=(mod.Config.GetValueOrDefault(f.Id)??f.DefaultValue)?.DeepClone();return values;}
}
