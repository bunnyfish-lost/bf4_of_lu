using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
namespace BF4Launcher;
public partial class MainWindow {
 internal async Task<Dictionary<string,bool>> CheckManagementViewsAsync(){
  var checks=new Dictionary<string,bool>();checks["client-restart-button-present"]=VisualChildren<Button>(ServiceButtons).Any(b=>b.Content?.ToString()=="重启客户端");checks["client-diagnostics-button-present"]=VisualChildren<Button>(ServiceButtons).Any(b=>b.Content?.ToString()=="打开启动诊断");
  checks["management-navigation-four-pages"]=navPlayers!=null&&playersPage!=null;checks["no-play-tools-navigation"]=!VisualChildren<Button>((System.Windows.Controls.StackPanel)NavLaunch.Parent).Any(button=>button.Content?.ToString()?.Contains("游玩工具")==true);
  NavServer.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  var unlock=fieldEditors.Where(e=>e.Field.Id=="allUnlocks").ToList();
  checks["weapon-unlock-single-server-switch"]=unlock.Count==1&&unlock[0].Field.Label=="武器全解"&&unlock[0].Control is CheckBox;
  checks["weapon-unlock-explains-restart-and-profile"]=unlock[0].Field.Hint.Contains("重启")&&unlock[0].Field.Hint.Contains("档案");
  var unlockBox=(CheckBox)unlock[0].Control;bool originalUnlock=unlockBox.IsChecked==true;var botsBefore=repo.Config.Server["initialBots"]?.DeepClone();unlockBox.IsChecked=false;SaveServer();
  checks["weapon-unlock-save-persists-false"]=JsonNode.Parse(System.IO.File.ReadAllText(repo.ConfigPath))!["server"]!["allUnlocks"]!.GetValue<bool>()==false;
  checks["server-save-preserves-home-bot-count"]=JsonNode.DeepEquals(repo.Config.Server["initialBots"],botsBefore);unlockBox.IsChecked=originalUnlock;SaveServer();

  checks["initial-bot-count-only-on-home-card"]=repo.Fields.Single(f=>f.Id=="initialBots").Hidden&&fixedBots!=null;
  checks["management-loadout-and-damage-cards-created"]=loadoutStatus!=null&&damageStatus!=null&&botDamageBox.Text=="100";
  NavLaunch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  fillMode.SelectedIndex=0;RefreshFillInputs();checks["bot-fixed-mode-inputs"]=fixedBots.IsEnabled&&!fillTarget.IsEnabled&&!fillTeam1.IsEnabled;
  fillMode.SelectedIndex=1;RefreshFillInputs();checks["bot-auto-mode-inputs"]=!fixedBots.IsEnabled&&fillTarget.IsEnabled&&fillReserve.IsEnabled&&!fillTeam1.IsEnabled;
  fillMode.SelectedIndex=2;RefreshFillInputs();checks["bot-team-mode-inputs"]=fillTeam1.IsEnabled&&fillTeam2.IsEnabled&&!fillTarget.IsEnabled;
  var launchPanel=(StackPanel)botControlCard.Parent;int cardIndex=launchPanel.Children.IndexOf(botControlCard);launchPanel.Children.Remove(botControlCard);CaptureElement(botControlCard,"bot-control-card",1180,360);launchPanel.Children.Insert(cardIndex,botControlCard);
  fillMode.SelectedIndex=0;fillDirty=false;RefreshFillInputs();
  checks["live-map-controls-separate-from-rotation-draft"]=liveMapCombo!=null&&VisualChildren<Button>(RotationCard).Any(b=>b.Content?.ToString()=="下一回合");
  navPlayers.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Dispatcher.Yield(DispatcherPriority.Loaded);
  checks["players-navigation-and-readonly-grid"]=playersPage.Visibility==Visibility.Visible&&playersTable.IsReadOnly&&profilesTable.IsReadOnly&&PageTitle.Text=="玩家管理";
  var playerPanel=(FrameworkElement)XamlReader.Parse(XamlWriter.Save((StackPanel)playersPage.Content));CaptureElement(playerPanel,"players-management",1180,770);
  var list=onlinePlayers.ToArray();onlinePlayers.Clear();onlinePlayers.Add(new(){Name="Alpha",Team=1});onlinePlayers.Add(new(){Name="Beta",Team=2});playerSearch.Text="Alpha";FilterPlayers();checks["players-name-filter"]=playersTable.Items.Count==1;playerSearch.Text="";onlinePlayers.Clear();onlinePlayers.AddRange(list);FilterPlayers();
  var profileWindow=new ProfileLevelWindow("测试玩家",new JsonObject{["rank"]=1,["experience"]=100},new JsonArray(0,100,300));
  var boxes=VisualChildren<TextBox>((FrameworkElement)profileWindow.Content).ToList();boxes[0].Text="2";checks["profile-rank-selects-threshold-xp"]=boxes[1].Text=="300";boxes[1].Text="175";checks["profile-manual-xp-keeps-exact-value"]=boxes[0].Text=="1"&&boxes[1].Text=="175";
  SetManagementEnabled(false);checks["management-disabled-during-task"]=!playersPage.IsEnabled&&!botControlCard.IsEnabled;SetManagementEnabled(true);
  NavServer.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));OperationBanner.Visibility=Visibility.Collapsed;
  var unlockView=(FrameworkElement)unlock[0].Control.Parent;var unlockPreview=(FrameworkElement)XamlReader.Parse(XamlWriter.Save(unlockView));CaptureElement(unlockPreview,"weapon-unlock-setting",720,165);CapturePreview("server-unlocks-and-rules");return checks;
 }
}
