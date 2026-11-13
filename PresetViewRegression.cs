using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace BF4Launcher;
public partial class MainWindow {
 internal async Task<Dictionary<string,bool>> CheckPresetEditorsAsync(){
  var checks=new Dictionary<string,bool>();var editor=fieldEditors.Single(e=>e.Field.Id=="serverPreset");var combo=(ComboBox)editor.Control;var original=combo.SelectedItem;var before=fieldEditors.ToDictionary(e=>e.Field.Id,e=>e.Read()?.DeepClone());
  NavServer.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));OperationBanner.Visibility=Visibility.Collapsed;StatusText.Text="服务端预设配置界面检查";
  checks["preset-dropdown-three-string-options"]=combo.Items.Cast<Choice>().Select(c=>c.Value!.GetValue<string>()).SequenceEqual(new[]{"CUSTOM","NORMAL","HARDCORE"});
  combo.SelectedItem=combo.Items.Cast<Choice>().Single(c=>c.Value!.GetValue<string>()=="HARDCORE");await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.DataBind);
  checks["preset-hardcore-disables-controlled-rules"]=fieldEditors.Where(e=>ServerPresetLogic.ControlledFields.Contains(e.Field.Id)).All(e=>!e.Control.IsEnabled&&e.OverrideHint.Text.Contains("硬核"));
  checks["preset-bot-and-ammo-remain-editable"]=fixedBots.IsEnabled&&fieldEditors.Single(e=>e.Field.Id=="ammoMode").Control.IsEnabled;
  checks["preset-changing-selection-retains-custom-values"]=fieldEditors.Where(e=>e.Field.Id!="serverPreset").All(e=>JsonNode.DeepEquals(e.Read(),before[e.Field.Id]));
  CapturePreview("server-preset-hardcore");
  combo.SelectedItem=combo.Items.Cast<Choice>().Single(c=>c.Value!.GetValue<string>()=="CUSTOM");
  checks["preset-custom-restores-rule-editors"]=fieldEditors.Where(e=>ServerPresetLogic.ControlledFields.Contains(e.Field.Id)).All(e=>e.Control.IsEnabled);
  combo.SelectedItem=original;RefreshLabels();return checks;
 }
}

