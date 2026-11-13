using System.Windows.Markup;
using System.Linq;
using System;using System.Collections.Generic;using System.IO;using System.Text.Json;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Threading;
namespace BF4Launcher;
public partial class MainWindow {
 internal async Task<Dictionary<string,bool>> CheckLogCleanupViewsAsync(){
  var result=new Dictionary<string,bool>();bool original=repo.Config.AutoCleanLogs;NavLaunch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Dispatcher.Yield(DispatcherPriority.Loaded);
  result["cleanup-switch-in-service-card"]=VisualChildren<CheckBox>(ServiceControlCard).Contains(AutoCleanLogsCheckBox);result["cleanup-label-matches-request"]=AutoCleanLogsCheckBox.Content?.ToString()=="自动清理日志文件";
  repo.Config.AutoCleanLogs=false;AutoCleanLogsCheckBox.IsChecked=false;repo.Save();result["cleanup-fresh-switch-unchecked"]=AutoCleanLogsCheckBox.IsChecked==false;
  AutoCleanLogsCheckBox.IsChecked=true;AutoCleanLogs_Click(AutoCleanLogsCheckBox,new RoutedEventArgs());result["cleanup-click-saves-on"]=JsonDocument.Parse(File.ReadAllText(repo.ConfigPath)).RootElement.GetProperty("autoCleanLogs").GetBoolean();
  SetInputsEnabled(false);result["cleanup-switch-disabled-during-operation"]=!AutoCleanLogsCheckBox.IsEnabled;SetInputsEnabled(true);
  var cardPreview=(FrameworkElement)XamlReader.Parse(XamlWriter.Save(ServiceControlCard));CaptureElement(cardPreview,"auto-clean-logs-card",1180,190);
  AutoCleanLogsCheckBox.IsChecked=false;AutoCleanLogs_Click(AutoCleanLogsCheckBox,new RoutedEventArgs());result["cleanup-click-saves-off"]=!JsonDocument.Parse(File.ReadAllText(repo.ConfigPath)).RootElement.GetProperty("autoCleanLogs").GetBoolean();
  repo.Config.AutoCleanLogs=original;AutoCleanLogsCheckBox.IsChecked=original;repo.Save();return result;
 }
}
