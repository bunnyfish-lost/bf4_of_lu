using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace BF4Launcher;
public partial class MainWindow {
 internal async Task<Dictionary<string,bool>> CheckGroupedUiAsync() {
  var checks=new Dictionary<string,bool>();
  ShowActivated=false;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.Manual;Left=-30000;Top=-30000;Width=1440;Height=930;Show();await Dispatcher.Yield(DispatcherPriority.Loaded);
  var content=(FrameworkElement)Content;
  void Layout(){content.Measure(new Size(1440,930));content.Arrange(new Rect(0,0,1440,930));content.UpdateLayout();}
  Rect PageBounds()=>new(PageHost.TransformToAncestor(content).Transform(new Point()),PageHost.RenderSize);
  NavLaunch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  AnimateSidebar(false,false);OperationBanner.Visibility=Visibility.Collapsed;Layout();
  var bounds=PageBounds();
  ShowNotice("操作已完成","反馈固定显示在下方，不改变页面位置。");Layout();
  checks["footer-feedback-does-not-move-or-resize-page"]=PageBounds()==bounds;
  checks["footer-feedback-inside-footer-host"]=ReferenceEquals(OperationBanner.Parent,FooterFeedbackHost)&&OperationBanner.TransformToAncestor(content).Transform(new Point()).Y>PageHost.TransformToAncestor(content).Transform(new Point()).Y+PageHost.ActualHeight;
  checks["footer-feedback-replaces-idle-status"]=StatusText.Visibility==Visibility.Collapsed;
  checks["sidebar-default-icons-only"]=Math.Abs(SidebarDrawer.ActualWidth-68)<0.1&&VisualChildren<TextBlock>(SidebarDrawer).Where(t=>Equals(t.Tag,"rail-label")).All(t=>t.Opacity==0);
  AnimateSidebar(true,false);Layout();
  checks["sidebar-expands-over-content-not-pushing-it"]=Math.Abs(SidebarDrawer.ActualWidth-232)<0.1&&PageBounds()==bounds;
  var labels=VisualChildren<TextBlock>(SidebarDrawer).Where(t=>Equals(t.Tag,"rail-label")).ToArray();
  checks["sidebar-expanded-full-labels"]=labels.Length>=7&&labels.All(t=>t.Opacity==1&&!string.IsNullOrWhiteSpace(t.Text)&&t.FontFamily.Source=="Microsoft YaHei UI");
  checks["sidebar-overlay-z-order"]=Panel.GetZIndex(SidebarDrawer)>0&&Grid.GetColumnSpan(SidebarDrawer)==2;
  Sidebar_MouseEnter(SidebarDrawer,new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount));await Task.Delay(240);Layout();
  checks["sidebar-hover-uses-width-and-label-animation"]=DependencyPropertyHelper.GetValueSource(SidebarDrawer,FrameworkElement.WidthProperty).IsAnimated&&labels.All(t=>DependencyPropertyHelper.GetValueSource(t,UIElement.OpacityProperty).IsAnimated)&&Math.Abs(SidebarDrawer.ActualWidth-232)<0.1&&PageBounds()==bounds;
  Sidebar_MouseLeave(SidebarDrawer,new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount));await Task.Delay(240);Layout();
  checks["sidebar-leave-animates-back-to-icons"]=Math.Abs(SidebarDrawer.ActualWidth-68)<0.1&&labels.All(t=>t.Opacity<0.01)&&PageBounds()==bounds;
  AnimateSidebar(false,false);Layout();
  checks["sidebar-collapse-restores-icons-with-stable-page"]=SidebarDrawer.ActualWidth==68&&PageBounds()==bounds;
  NavMods.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Layout();await Dispatcher.Yield(DispatcherPriority.Loaded);Layout();
  checks["mod-list-one-card-per-id"]=modCards.Count==repo.Config.Mods.Select(m=>m.Manifest.Id).Distinct().Count()&&modCards.Select(m=>m.Manifest.Id).Distinct().Count()==modCards.Count;
  var first=modCards.First();ModsList.ScrollIntoView(first);Layout();
  var row=(ListBoxItem)ModsList.ItemContainerGenerator.ContainerFromItem(first);
  var text=VisualChildren<TextBlock>(row).First(t=>!string.IsNullOrWhiteSpace(t.Text)&&t.Name!="DragHandle");
  BeginCardPress(text,MouseButton.Left,1,new Point());
  checks["mod-card-text-click-toggles-selected-item"]=ReferenceEquals(EndCardPress(text,MouseButton.Left),first);
  bool enabledBefore=first.Enabled;BeginCardPress(text,MouseButton.Left,1,new Point());var mouseUp=new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseUpEvent,Source=text};Mods_MouseUp(ModsList,mouseUp);
  checks["mod-card-handler-persists-toggle"]=first.Enabled!=enabledBefore&&repo.Config.ModGroups!.Single(g=>g.Id==first.Manifest.Id).Enabled==first.Enabled;
  ToggleMod(first,enabledBefore);
  foreach(var button in VisualChildren<System.Windows.Controls.Primitives.ButtonBase>(row)){
   BeginCardPress(button,MouseButton.Left,1,new Point());
   if(EndCardPress(button,MouseButton.Left)!=null)throw new Exception("卡片按钮不应触发开关。");
  }
  checks["mod-card-checkbox-and-buttons-excluded"]=VisualChildren<CheckBox>(row).Any()&&VisualChildren<Button>(row).Count()>=4;
  BeginCardPress(text,MouseButton.Left,1,new Point());TrackCardMotion(new Point(12,0));
  checks["mod-card-drag-does-not-toggle"]=EndCardPress(text,MouseButton.Left)==null;
  BeginCardPress(text,MouseButton.Left,2,new Point());
  checks["mod-card-double-click-does-not-toggle-twice"]=EndCardPress(text,MouseButton.Left)==null;
  BeginCardPress(text,MouseButton.Right,1,new Point());
  checks["mod-card-right-click-does-not-toggle"]=EndCardPress(text,MouseButton.Right)==null;
  var handle=VisualChildren<TextBlock>(row).Single(t=>t.Name=="DragHandle");BeginCardPress(handle,MouseButton.Left,1,new Point());
  checks["mod-card-drag-handle-does-not-toggle"]=EndCardPress(handle,MouseButton.Left)==null;
  BeginCardPress(text,MouseButton.Left,1,new Point());TrackCardMotion(new Point(2,1));
  checks["mod-card-minor-pointer-jitter-still-clicks"]=ReferenceEquals(EndCardPress(text,MouseButton.Left),first);

  // Isolated versions exercise the same editor and repository used by the real settings window.
  var root=Path.Combine(AppContext.BaseDirectory,"ui-version-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  var fixture=new LauncherConfig();
  foreach(var version in new[]{"1.0.0","2.0.0"}){
   var manifest=new ModManifest{Id="ui-version-check",Name="独立版本测试",Version=version,Side="client",Adapter="player-effects",Fields=new(){new FieldDef{Id="value",Label="倍率",Type="number",Min=1,Max=10,DefaultValue=JsonValue.Create(1)}}};
   var item=new ModItem{Key=manifest.Id+"@"+version,Directory=Path.Combine("mods",manifest.Id,version),Manifest=manifest,Enabled=version=="1.0.0",Config=new(){["value"]=JsonValue.Create(version=="1.0.0"?2:7),["unexposed"]=JsonValue.Create(42)}};
   Directory.CreateDirectory(Path.Combine(root,item.Directory));Repository.Atomic(Path.Combine(root,item.Directory,"mod.json"),JsonSerializer.Serialize(manifest,Repository.Json));Repository.Atomic(Path.Combine(root,item.Directory,"config.json"),JsonSerializer.Serialize(item.Config,Repository.Json));fixture.Mods.Add(item);
  }
  Repository.Atomic(Path.Combine(root,"config.json"),JsonSerializer.Serialize(fixture,Repository.Json));
  var versionRepo=new Repository(root);versionRepo.Load();int changes=0;var initial=versionRepo.ModCards.Single();var wrapper=new ModVersionsWindow(versionRepo,initial,null,()=>changes++);
  checks["version-selector-has-all-versions"]=wrapper.VersionSelector.Items.Count==2&&wrapper.SelectedMod.Version=="1.0.0";
  string older=Path.Combine(root,"mods/ui-version-check/1.0.0/config.json"),newer=Path.Combine(root,"mods/ui-version-check/2.0.0/config.json");string newerBefore=File.ReadAllText(newer);
  ((TextBox)wrapper.CurrentEditor.ConfigEditors["value"].Control).Text="4";
  checks["version-editor-tracks-unsaved-draft"]=wrapper.CurrentEditor.HasUnsavedChanges;
  wrapper.CurrentEditor.SaveDraft();
  checks["version-editor-writes-only-current-version"]=JsonNode.Parse(File.ReadAllText(older))!["value"]!.GetValue<int>()==4&&File.ReadAllText(newer)==newerBefore;
  checks["version-editor-preserves-unexposed-values"]=JsonNode.Parse(File.ReadAllText(older))!["unexposed"]!.GetValue<int>()==42;
  await wrapper.SwitchAsync(versionRepo.VersionsOf(initial.Manifest.Id).Single(m=>m.Version=="2.0.0"));
  checks["version-switch-selects-and-persists-without-new-card"]=versionRepo.ModCards.Count==1&&wrapper.SelectedMod.Version=="2.0.0"&&versionRepo.Config.Mods.Count(m=>m.Enabled)==1&&changes>=2;
  checks["version-switch-rebuilds-editor-with-own-values"]=((TextBox)wrapper.CurrentEditor.ConfigEditors["value"].Control).Text=="7"&&!wrapper.CurrentEditor.HasUnsavedChanges;
  string olderBefore=File.ReadAllText(older);((TextBox)wrapper.CurrentEditor.ConfigEditors["value"].Control).Text="9";wrapper.CurrentEditor.SaveDraft();
  checks["version-second-edit-does-not-write-first"]=File.ReadAllText(older)==olderBefore&&JsonNode.Parse(File.ReadAllText(newer))!["value"]!.GetValue<int>()==9;
  await wrapper.SwitchAsync(versionRepo.VersionsOf(initial.Manifest.Id).Single(m=>m.Version=="1.0.0"));
  checks["version-return-restores-own-saved-values"]=((TextBox)wrapper.CurrentEditor.ConfigEditors["value"].Control).Text=="4";
  var sparse=JsonSerializer.Deserialize<ModItem>(JsonSerializer.Serialize(wrapper.SelectedMod,Repository.Json),Repository.Json)!;sparse.Config.Remove("value");var sparseEditor=new ModConfigWindow(sparse);
  checks["version-unchanged-default-display-is-not-dirty"]=!sparseEditor.HasUnsavedChanges;
  ((TextBox)sparseEditor.ConfigEditors["value"].Control).Text="3";checks["version-editing-default-display-is-dirty"]=sparseEditor.HasUnsavedChanges;sparseEditor.Close();
  wrapper.Close();NavLaunch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));OperationBanner.Visibility=Visibility.Collapsed;noticeTimer.Stop();Hide();return checks;
 }
}
