using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
namespace BF4Launcher;
public static class ServerPresetLogic {
 public static readonly HashSet<string> ControlledFields=new(){"friendlyFire","autoBalance","vehicles","regen","miniMap","hud","spotting","killCam","respawn","health","damage","startPlayers","commander","tickets"};
 public static string ReadFile(string path){
  if(!File.Exists(path))return "CUSTOM";
  var matches=Regex.Matches(File.ReadAllText(path),@"^[\t ]*vars\.preset[\t ]+(\S+)",RegexOptions.Multiline|RegexOptions.IgnoreCase);
  return matches.Count>0?matches[^1].Groups[1].Value.ToUpperInvariant():"CUSTOM";
 }
 public static string Label(string preset)=>preset switch{"NORMAL"=>"普通","HARDCORE"=>"硬核",_=>"自定义"};
}
