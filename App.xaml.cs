using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Text.Json;
namespace BF4Launcher;
public partial class App : Application {
 private Mutex? mutex;
 public static bool UiPreview {get;private set;}
 protected override void OnStartup(StartupEventArgs e) {
  base.OnStartup(e);
  EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,new RoutedEventHandler((s,a)=>{if(s is Window w)Bf4Theme.DarkCaption(w);}));
  UiPreview=Array.IndexOf(e.Args,"--bf4-ui-capture")>=0||Array.IndexOf(e.Args,"--desktop-self-test")>=0;
  bool modelTest=Array.IndexOf(e.Args,"--self-test")>=0;
  bool desktopTest=Array.IndexOf(e.Args,"--desktop-self-test")>=0;
  if(modelTest){try{File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"self-test-result.json"),SelfTests.Run());Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"self-test-result.json"),ex.ToString());Shutdown(1);}return;}
  int wait=Array.IndexOf(e.Args,"--wait-for-exit");
  if(wait>=0&&e.Args.Length>wait+1&&int.TryParse(e.Args[wait+1],out int parent)){try{using var previous=Process.GetProcessById(parent);if(!previous.WaitForExit(20000))throw new Exception("请先关闭旧的启动器窗口。");}catch(ArgumentException){}}
  var key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(AppContext.BaseDirectory.ToLowerInvariant())))[..20];
  mutex=new Mutex(true,"Local\\BF4Launcher-"+key,out bool first);
  if(!first){MessageBox.Show("这个目录的启动器已打开。","BF4 启动器");Shutdown();return;}
  DispatcherUnhandledException+=(s,x)=>{try{File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"launcher-error.log"),DateTime.Now+" "+x.Exception+Environment.NewLine);}catch{}x.Handled=true;if(desktopTest){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"desktop-self-test-result.json"),x.Exception.ToString());Shutdown(1);}else{MessageBox.Show(x.Exception.Message,"BF4 启动器");if(MainWindow==null)Shutdown(1);}};
  EngineBootstrap.Ensure(AppContext.BaseDirectory);
  var win=new MainWindow();MainWindow=win;
  if(desktopTest){ShutdownMode=ShutdownMode.OnExplicitShutdown;RunDesktopChecks(win);return;}
  if(UiPreview){CaptureBf4Preview(win);return;}
  win.Show();
 }
 async void RunDesktopChecks(MainWindow win){
  try{
   var checks=new System.Collections.Generic.Dictionary<string,bool>();checks["three-pages-created"]=win.FindName("LaunchPage")!=null&&win.FindName("ModsPage")!=null&&win.FindName("ServerPage")!=null;checks["server-editors-created"]=((StackPanel)win.FindName("ServerFields")).Children.Count>0;
   var grid=(DataGrid)win.FindName("RotationGrid");int originalCount=grid.Items.Count;checks["rotation-card-and-catalog-bound"]=originalCount>0&&((ComboBox)win.FindName("AddMapCombo")).Items.Count==12;
   ((Button)win.FindName("NavRotation")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));checks["rotation-shortcut-navigation"]=((TextBlock)win.FindName("PageTitle")).Text=="地图轮换";
   var add=typeof(MainWindow).GetMethod("AddRotation_Click",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);add!.Invoke(win,new object[]{win,new RoutedEventArgs()});checks["rotation-add-draft-row"]=grid.Items.Count==originalCount+1;
   foreach(var pair in await win.CheckRotationBindingsAsync())checks[pair.Key]=pair.Value;
   foreach(var pair in await win.CheckFeedbackAsync())checks[pair.Key]=pair.Value;
   foreach(var pair in await win.CheckPresetEditorsAsync())checks[pair.Key]=pair.Value;
   foreach(var pair in await win.CheckExternalViewsAsync())checks[pair.Key]=pair.Value;
   foreach(var pair in await win.CheckLogCleanupViewsAsync())checks[pair.Key]=pair.Value;
   foreach(var pair in await win.CheckManagementViewsAsync())checks[pair.Key]=pair.Value;
   foreach(var pair in await win.CheckModSettingsViewsAsync())checks[pair.Key]=pair.Value;
   foreach(var pair in await win.CheckV2ViewsAsync())checks[pair.Key]=pair.Value;
   foreach(var pair in await win.CheckGroupedUiAsync())checks[pair.Key]=pair.Value;
   foreach(var pair in await win.CheckPrivilegeNoticeAsync())checks[pair.Key]=pair.Value;
   foreach(var pair in await win.CheckAudioOfflineActionsAsync())checks[pair.Key]=pair.Value;
   win.Closed+=(s,e)=>{checks["unsaved-rotation-draft-not-committed"]=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"config.json"))).RootElement.GetProperty("rotation").GetArrayLength()==originalCount;checks["hidden-window-close-completes"]=true;checks["config-saved-on-close"]=File.Exists(Path.Combine(AppContext.BaseDirectory,"config.json"));File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"desktop-self-test-result.json"),JsonSerializer.Serialize(new{passed=System.Linq.Enumerable.Count(checks,c=>c.Value),checks}));Shutdown(System.Linq.Enumerable.All(checks,c=>c.Value)?0:1);};
   var timeout=new DispatcherTimer{Interval=TimeSpan.FromSeconds(20)};timeout.Tick+=(s,e)=>{timeout.Stop();File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"desktop-self-test-result.json"),"Window close timed out");Shutdown(1);};timeout.Start();win.Close();
  }catch(Exception ex){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"desktop-self-test-result.json"),ex.ToString());Shutdown(1);}
 }
 protected override void OnExit(ExitEventArgs e){mutex?.Dispose();base.OnExit(e);}
}
