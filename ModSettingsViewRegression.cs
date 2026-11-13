using System;using System.Windows.Markup;using System.Collections.Generic;using System.Linq;using System.Text.Json;using System.Text.Json.Nodes;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;
namespace BF4Launcher;
public partial class MainWindow {
 internal async Task<Dictionary<string,bool>> CheckModSettingsViewsAsync(){
  var checks=new Dictionary<string,bool>();
  ModItem Fixture(string id){var original=repo.ModCards.Single(m=>m.Manifest.Id==id);var item=JsonSerializer.Deserialize<ModItem>(JsonSerializer.Serialize(original,Repository.Json),Repository.Json)!;item.Enabled=true;return item;}
  var audio=Fixture("battle-audio");var before=JsonSerializer.Serialize(audio.Config,Repository.Json);int calls=0,saves=0;JsonObject? lastBody=null;
  var audioWindow=new ModConfigWindow(audio,async(action,body)=>{calls++;lastBody=(JsonObject)body.DeepClone();await Task.Delay(30);return new JsonObject{["message"]="操作已完成"};},()=>saves++);
  checks["audio-all-actions-in-own-settings"]=new[]{"preview-kill","preview-death","import-kill","import-death"}.All(id=>audioWindow.ActionButtons.ContainsKey(id));
  checks["audio-pack-controls-reused-not-duplicated"]=ReferenceEquals(audioWindow.ConfigEditors["killPack"],audioWindow.ActionEditors["killPack"])&&ReferenceEquals(audioWindow.ConfigEditors["deathPack"],audioWindow.ActionEditors["deathPack"]);
  var pack=(ComboBox)audioWindow.ConfigEditors["killPack"].Control;pack.SelectedItem=audioWindow.ConfigEditors["killPack"].Field.Options.Single(c=>c.Value?.ToString()=="cf");
  ((TextBox)audioWindow.ActionEditors["file"].Control).Text="C:\\Temp\\test.wav";
  var first=audioWindow.ExecuteActionAsync("preview-kill");await audioWindow.ExecuteActionAsync("preview-kill");await first;
  checks["mod-action-double-click-starts-once"]=calls==1;
  checks["mod-action-uses-current-draft-pack"]=lastBody?["killPack"]?.ToString()=="cf";
  checks["mod-actions-do-not-auto-save-draft"]=saves==0&&JsonSerializer.Serialize(audio.Config,Repository.Json)==before;
  audioWindow.SaveDraft();checks["mod-save-persists-once"]=saves==1&&audio.Config["killPack"]?.ToString()=="cf";
  checks["action-only-fields-not-in-mod-config"]=!audio.Config.ContainsKey("file")&&!audio.Config.ContainsKey("slot")&&!audio.Config.ContainsKey("volume");
  CaptureElement((FrameworkElement)audioWindow.Content,"audio-mod-settings",644,760);audioWindow.ShowActions();CaptureElement((FrameworkElement)audioWindow.Content,"audio-mod-actions",644,760);
  var vehicle=Fixture("vehicle-summon");string originalManifest=JsonSerializer.Serialize(vehicle.Manifest,Repository.Json);string originalConfig=JsonSerializer.Serialize(vehicle.Config,Repository.Json);int vehicleCalls=0;
  var vehicleWindow=new ModConfigWindow(vehicle,(action,body)=>{vehicleCalls++;return Task.FromResult<JsonNode?>(new JsonObject{["message"]="已读取当前地图载具",["options"]=new JsonObject{["model"]=new JsonArray(new JsonObject{["label"]="M1",["value"]="Vehicles/M1"})}});});
  checks["vehicle-actions-in-own-settings"]=new[]{"refresh","spawn","clear"}.All(id=>vehicleWindow.ActionButtons.ContainsKey(id));
  await vehicleWindow.ExecuteActionAsync("refresh");
  checks["vehicle-choices-update-in-mod-window"]=((Choice)((ComboBox)vehicleWindow.ActionEditors["model"].Control).SelectedItem).Value?.ToString()=="Vehicles/M1";
  checks["dynamic-choices-do-not-change-manifest"]=JsonSerializer.Serialize(vehicle.Manifest,Repository.Json)==originalManifest;
  checks["runtime-actions-do-not-change-persistent-config"]=JsonSerializer.Serialize(vehicle.Config,Repository.Json)==originalConfig;
  vehicleWindow.ShowActions();CaptureElement((FrameworkElement)vehicleWindow.Content,"vehicle-mod-settings",644,760);
  var disabled=Fixture("battle-audio");disabled.Enabled=false;int disabledCalls=0;var disabledWindow=new ModConfigWindow(disabled,(a,body)=>{disabledCalls++;return Task.FromResult<JsonNode?>(null);});await disabledWindow.ExecuteActionAsync("preview-kill");
  checks["disabled-audio-preview-runs-without-enabling-mod"]=disabledCalls==1&&!disabled.Enabled&&disabledWindow.ActionButtons.Values.All(button=>button.IsEnabled);
  var disabledVehicle=Fixture("vehicle-summon");disabledVehicle.Enabled=false;int vehicleDisabledCalls=0;var disabledVehicleWindow=new ModConfigWindow(disabledVehicle,(a,b)=>{vehicleDisabledCalls++;return Task.FromResult<JsonNode?>(null);});await disabledVehicleWindow.ExecuteActionAsync("spawn");checks["disabled-runtime-actions-still-require-enabled-mod"]=vehicleDisabledCalls==0&&disabledVehicleWindow.ActionButtons.Values.All(button=>!button.IsEnabled);
  string? logged=null;var failWindow=new ModConfigWindow(Fixture("battle-audio"),(a,body)=>throw new Exception("Command failed: powershell.exe -NoProfile -Command Get-Process"),reportError:e=>logged=e.Message);await failWindow.ExecuteActionAsync("preview-kill");
  checks["mod-action-error-is-short"]=!failWindow.ActionMessage.Contains("Command")&&failWindow.ActionMessage.Length<100;
  checks["mod-action-error-details-kept-for-log"]=logged?.Contains("Get-Process")==true;
  var normal=Fixture("auto-aim");var normalWindow=new ModConfigWindow(normal);checks["aim-settings-still-generated-from-mod"]=normalWindow.ConfigEditors.Count==normal.Manifest.Fields.Count&&normalWindow.ActionButtons.Count==0;
  var other=Fixture("enemy-display");var otherWindow=new ModConfigWindow(other);checks["enemy-settings-still-independent"]=otherWindow.ConfigEditors.ContainsKey("distance")&&!otherWindow.ConfigEditors.ContainsKey("strength");
  var originalStatus=FeatureStatus.Text;FeatureStatus.Text=UiText.Summary("Command failed: powershell.exe -NoProfile -Command Get-Process", "客户端状态读取失败，请查看运行日志。");
  checks["home-feature-status-short-and-height-bounded"]=!FeatureStatus.Text.Contains("Command")&&FeatureStatus.MaxHeight<=42;
  NavLaunch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CapturePreview("clean-startup-status");var card=(FrameworkElement)XamlReader.Parse(XamlWriter.Save(ServiceControlCard));CaptureElement(card,"clean-service-card",1180,240);FeatureStatus.Text=originalStatus;
  return checks;
 }
}
