using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
namespace BF4Launcher;
public partial class MainWindow {
 internal async Task<Dictionary<string,bool>> CheckAudioOfflineActionsAsync(){
  var checks=new Dictionary<string,bool>();var audio=repo.ModCards.Single(m=>m.Manifest.Id=="battle-audio");bool enabled=audio.Enabled;
  repo.SetModEnabled(audio.Manifest.Id,false);string config=File.ReadAllText(repo.ConfigPath),version=File.ReadAllText(Path.Combine(repo.Root,audio.Directory,"config.json"));
  checks["audio-preview-fresh-server-not-initialized"]=!backend.Initialized&&!File.Exists(Path.Combine(repo.Root,"host","runtime","private.json"));
  var editor=new ModConfigWindow(audio,(action,body)=>backend.ModActionAsync(audio.Key,action,body));
  try{
   checks["audio-preview-buttons-enabled-while-mod-off"]=editor.ActionButtons.Values.All(b=>b.IsEnabled);
   ((System.Windows.Controls.TextBox)editor.ActionEditors["volume"].Control).Text="0";
   await editor.ExecuteActionAsync("preview-kill");bool kill=editor.ActionMessage.Contains("已播放击杀");
   await editor.ExecuteActionAsync("preview-death");bool death=editor.ActionMessage.Contains("已播放死亡");
   checks["audio-native-kill-and-death-preview-complete-offline"]=kill&&death;
   checks["audio-preview-does-not-enable-or-save-parameters"]=!audio.Enabled&&File.ReadAllText(repo.ConfigPath)==config&&File.ReadAllText(Path.Combine(repo.Root,audio.Directory,"config.json"))==version;
   checks["audio-preview-does-not-create-server-initialization"]=!backend.Initialized&&!File.Exists(Path.Combine(repo.Root,"host","runtime","private.json"))&&!File.Exists(Path.Combine(repo.Root,"data","environment.json"));
   editor.ShowActions();CaptureElement((FrameworkElement)editor.Content,"audio-disabled-offline-preview",644,760);return checks;
  }finally{editor.Close();repo.SetModEnabled(audio.Manifest.Id,enabled);}
 }
}
