using System;using System.IO;using System.Linq;using System.Text.Json;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Threading;
namespace BF4Launcher;
public partial class App {
 async void CaptureBf4Preview(MainWindow win){try{await win.RenderBf4PreviewAsync();File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"bf4-ui-preview-result.json"),JsonSerializer.Serialize(new{success=true,theme="bf4",width=1440,height=930,library=win.PreviewLibraryState}));win.FinishBf4Preview();Shutdown(0);}catch(Exception e){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"bf4-ui-preview-result.json"),e.ToString());Shutdown(1);}}
}
public partial class MainWindow {
 internal object PreviewLibraryState=>new{modCount=repo.ModCards.Count,versionCount=repo.Config.Mods.Count(m=>m.HasFiles),enabledCount=repo.Config.Mods.Count(m=>m.Enabled),versions=repo.ModCards.ToDictionary(m=>m.Manifest.Id,m=>m.Version)};
 internal async Task RenderBf4PreviewAsync(){
  Width=1440;Height=930;await Dispatcher.Yield(DispatcherPriority.Loaded);
  async Task Capture(string name){await Dispatcher.Yield(DispatcherPriority.DataBind);CaptureElement((FrameworkElement)Content,name,1440,930);}
  NavLaunch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Capture("bf4-launch");
  BeginOperation("启动 BOT 服务端",OperationKind.StartServer);SetOperationStep("正在等待游戏服与 BOT 完成初始化",2,4);await Capture("bf4-footer-progress");operationTimer.Stop();operationClock.Stop();SetInputsEnabled(true);PaintReadiness(RuntimeReadiness.Stopped);OperationBanner.Visibility=Visibility.Collapsed;StatusText.Text="就绪";
  NavMods.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Capture("bf4-mods");
  AnimateSidebar(true,false);await Capture("bf4-sidebar-expanded");AnimateSidebar(false,false);
  ShowNotice("模组顺序已保存","新的顺序将用于实际加载和参数覆盖。");await Capture("bf4-footer-feedback");OperationBanner.Visibility=Visibility.Collapsed;
  NavServer.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Capture("bf4-server");
  navPlayers.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(200);await Capture("bf4-players");
  NavRotation.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Dispatcher.Yield(DispatcherPriority.Loaded);ScrollBf4RotationToTop();CaptureElement((FrameworkElement)Content,"bf4-rotation",1440,930);
  var mod=repo.ModCards.FirstOrDefault(m=>m.Manifest.Id=="auto-aim")??repo.ModCards.FirstOrDefault(m=>m.Manifest.Fields.Count>0);if(mod!=null){var dialog=new ModVersionsWindow(repo,mod,null,()=>{});CaptureElement((FrameworkElement)dialog.Content,"bf4-mod-config",662,720);dialog.Close();}
  NavLaunch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));LaunchPage.ScrollToTop();OperationBanner.Visibility=Visibility.Collapsed;RefreshBf4Navigation("launch");CaptureElement((FrameworkElement)Content,"bf4-launch-compact",1120,740);
 }
 internal void FinishBf4Preview(){allowClose=true;timer.Stop();Close();}
}
