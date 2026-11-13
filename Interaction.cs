using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
namespace BF4Launcher;
public partial class MainWindow {
 readonly Stopwatch operationClock=new();readonly DispatcherTimer operationTimer=new(){Interval=TimeSpan.FromSeconds(1)};readonly DispatcherTimer noticeTimer=new(){Interval=TimeSpan.FromSeconds(9)};
 OperationKind operationKind;RuntimeReadiness runtimeReadiness=RuntimeReadiness.Stopped;bool awaitingServerReady;string activeOperation="",completionTitle="",completionDetail="";bool completionIsInfo;
 void ConfigureFeedback(){operationTimer.Tick+=(s,e)=>OperationElapsed.Text=operationClock.Elapsed.ToString(@"mm\:ss");noticeTimer.Tick+=(s,e)=>{noticeTimer.Stop();if(!busy)OperationBanner.Visibility=Visibility.Collapsed;};backend.Progress+=p=>Dispatcher.BeginInvoke(()=>{if(busy)SetOperationStep(p.Detail,p.Step,p.Total);});PaintReadiness(RuntimeReadiness.Stopped);}
 static SolidColorBrush Brush(string hex)=>new((Color)ColorConverter.ConvertFromString(hex));
 void PaintReadiness(RuntimeReadiness state,string? detail=null){
  runtimeReadiness=state;
  RuntimeCard.Background=Brush(state==RuntimeReadiness.Ready?"#1D493A":state==RuntimeReadiness.Preparing?"#4A3F28":"#4A2C35");
  RuntimeCard.BorderBrush=Brush(state==RuntimeReadiness.Ready?"#4B9E75":state==RuntimeReadiness.Preparing?"#B59753":"#B65B63");
  RuntimeDot.Fill=Brush(state==RuntimeReadiness.Ready?"#76E7AC":state==RuntimeReadiness.Preparing?"#F0CB72":"#F48A91");
  RuntimeReadinessLabel.Foreground=Brush(state==RuntimeReadiness.Ready?"#C5F2D8":state==RuntimeReadiness.Preparing?"#F7E6BA":"#FFD9DD");
  RuntimeReadinessLabel.Text=detail??(state==RuntimeReadiness.Ready?"服务完全启动":state==RuntimeReadiness.Preparing?"服务准备中":"未启动");
  RuntimeCard.ToolTip="绿色表示登录服务、游戏服、BOT 初始化和对局均已就绪。客户端模组状态见列表。";
  if(state==RuntimeReadiness.Ready&&awaitingServerReady&&!busy){awaitingServerReady=false;ShowNotice("BOT 服务端完全启动","服务端与 BOT 均已就绪，可以启动游戏。",false);}
 }
 void SetInputsEnabled(bool enabled){AdminRestartButton.IsEnabled=enabled;SetManagementEnabled(enabled);PathsCard.IsEnabled=enabled;ServiceButtons.IsEnabled=enabled;AutoCleanLogsCheckBox.IsEnabled=enabled;RotationCard.IsEnabled=enabled;ModsPage.IsEnabled=enabled;ServerFields.IsEnabled=enabled;ServerSaveButtons.IsEnabled=enabled;AdminButton.IsEnabled=enabled;PreferencesButton.IsEnabled=enabled;}
 void BeginOperation(string title,OperationKind kind){
  activeOperation=title;operationKind=kind;completionTitle="";completionDetail="";completionIsInfo=false;noticeTimer.Stop();operationClock.Restart();operationTimer.Start();OperationBanner.Visibility=Visibility.Visible;OperationBanner.Background=Brush("#E2152B3A");OperationBanner.BorderBrush=Brush("#5A8199");OperationTitle.Foreground=Brush("#EFF6FA");OperationDetail.Foreground=Brush("#AFC9D8");OperationSymbol.Foreground=Brush("#FFA942");OperationSymbol.Text="●";OperationTitle.Text="正在"+title;OperationDetail.Text="准备中…";OperationElapsed.Text="00:00";OperationProgressBar.Visibility=Visibility.Visible;DismissOperationButton.Visibility=Visibility.Collapsed;SetInputsEnabled(false);
  if(kind is OperationKind.StartServer or OperationKind.RestartServer){awaitingServerReady=false;PaintReadiness(RuntimeReadiness.Preparing,"正在启动服务");}
  if(kind==OperationKind.StopServer){awaitingServerReady=false;PaintReadiness(RuntimeReadiness.Preparing,"正在保存并停服");}
 }
 void SetOperationStep(string detail,int step=0,int total=0){detail=UiText.Summary(detail);OperationDetail.Text=(total>0?$"{step}/{total} · ":"")+detail;StatusText.Text=detail;}
 void SetCompletion(string title,string detail,bool info=false){completionTitle=title;completionDetail=UiText.Summary(detail);completionIsInfo=info;}
 void FinishOperation(bool success,string? error=null){if(error!=null)error=UiText.Summary(error);
  operationClock.Stop();operationTimer.Stop();OperationElapsed.Text=operationClock.Elapsed.ToString(@"mm\:ss");SetInputsEnabled(true);OperationProgressBar.Visibility=Visibility.Collapsed;DismissOperationButton.Visibility=Visibility.Visible;
  if(success){
   var title=completionTitle!=""?completionTitle:activeOperation+"已完成";var detail=completionDetail!=""?completionDetail:"用时 "+operationClock.Elapsed.ToString(@"mm\:ss")+"。";
   if(operationKind is OperationKind.StartServer or OperationKind.RestartServer){if(runtimeReadiness==RuntimeReadiness.Ready){title="BOT 服务端完全启动";detail="登录服务、游戏服和 BOT 均已就绪。";}else{awaitingServerReady=true;title="启动任务已完成";detail="服务仍在准备，首页状态会继续更新。";completionIsInfo=true;}}
   ShowNotice(title,detail,false,completionIsInfo);
  }else ShowNotice(activeOperation+"未完成",error??"查看运行记录以了解详情。",true);
 }
 void ShowNotice(string title,string detail,bool error=false,bool info=false){detail=UiText.Summary(detail);
  if(busy)return;
  noticeTimer.Stop();OperationBanner.Visibility=Visibility.Visible;OperationProgressBar.Visibility=Visibility.Collapsed;DismissOperationButton.Visibility=Visibility.Visible;
  OperationBanner.Background=Brush(error?"#E43A202A":info?"#E2152B3A":"#E31D3E34");OperationBanner.BorderBrush=Brush(error?"#B96D7B":info?"#5A8199":"#568F75");OperationTitle.Foreground=Brush(error?"#FFD3D7":info?"#DDEEF8":"#CCF2DC");OperationDetail.Foreground=Brush(error?"#F5C1C7":info?"#ADC9DC":"#B6DCC6");OperationSymbol.Foreground=OperationTitle.Foreground;OperationSymbol.Text=error?"!":info?"●":"✓";OperationTitle.Text=title;OperationDetail.Text=detail.Length>220?detail[..220]+"…":detail;OperationDetail.ToolTip=detail;StatusText.Text=title;if(!error)noticeTimer.Start();
 }
 void ObserveProgressLog(string text){
  if(!busy)return;
  if(operationKind==OperationKind.Initialize){if(text.Contains("检查服务端版本"))SetOperationStep("校验原版服务端文件",1,5);else if(text.Contains("准备独立运行文件"))SetOperationStep("复制独立运行文件",2,5);else if(text.Contains("链接大型资源"))SetOperationStep("复用大型游戏资源",3,5);else if(text.Contains("准备客户端与配置"))SetOperationStep("准备独立客户端与配置",4,5);else if(text.Contains("本地环境已准备完成"))SetOperationStep("本地环境已准备完成",5,5);}
  if(operationKind is OperationKind.StartServer or OperationKind.RestartServer){if(text.Contains("Bot startup configuration prepared"))SetOperationStep("BOT 配置已准备，正在启动游戏服",2,4);else if(text.Contains("Loaded weapon selection")||text.Contains("Loaded Bot progress")||text.Contains("Loaded Bot damage"))SetOperationStep("正在加载 BOT 武器、进度与伤害模块",3,4);else if(text.Contains("Bot selection ready"))SetOperationStep("BOT 模块已就绪，正在确认对局状态",4,4);else if(text.Contains("Services started"))SetOperationStep("服务已启动，正在读取对局状态",4,4);}
 }
 void DismissOperation_Click(object sender,RoutedEventArgs e){if(!busy){noticeTimer.Stop();OperationBanner.Visibility=Visibility.Collapsed;}}
 internal async Task<Dictionary<string,bool>> CheckFeedbackAsync(){
  var checks=new Dictionary<string,bool>();NavLaunch.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));LaunchPage.ScrollToHome();await Work("界面反馈检查",async()=>{checks["operation-banner-visible"]=OperationBanner.Visibility==Visibility.Visible&&OperationProgressBar.Visibility==Visibility.Visible;checks["conflicting-controls-disabled"]=!ServiceButtons.IsEnabled&&!ModsPage.IsEnabled;SetOperationStep("测试阶段",2,3);checks["stage-message-visible"]=OperationDetail.Text.Contains("2/3");CapturePreview("operation-in-progress");await Task.Delay(30);});checks["completion-feedback-visible"]=OperationBanner.Visibility==Visibility.Visible&&OperationTitle.Text.Contains("已完成")&&ServiceButtons.IsEnabled;
  await Work("初始化阶段检查",()=>{ObserveProgressLog("初始化：检查服务端版本");checks["initialize-stage-one-visible"]=OperationDetail.Text.Contains("1/5");ObserveProgressLog("初始化：本地环境已准备完成");checks["initialize-final-stage-visible"]=OperationDetail.Text.Contains("5/5");return Task.CompletedTask;},OperationKind.Initialize);
  await Work("错误反馈检查",()=>Task.FromException(new Exception("测试任务返回的错误详情")));checks["failure-feedback-visible"]=OperationTitle.Text.Contains("未完成")&&OperationDetail.Text.Contains("错误详情")&&ServiceButtons.IsEnabled;
  PaintReadiness(RuntimeReadiness.Stopped);checks["stopped-card-red"]=((SolidColorBrush)RuntimeCard.Background).Color==(Color)ColorConverter.ConvertFromString("#4A2C35");PaintReadiness(RuntimeReadiness.Ready);checks["ready-card-green"]=((SolidColorBrush)RuntimeCard.Background).Color==(Color)ColorConverter.ConvertFromString("#1D493A");PaintReadiness(RuntimeReadiness.Preparing);checks["preparing-card-amber"]=((SolidColorBrush)RuntimeCard.Background).Color==(Color)ColorConverter.ConvertFromString("#4A3F28");PaintReadiness(RuntimeReadiness.Stopped);
  NavMods.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));ShowNotice("枪械模组已合并","无散布、无后座、无下坠和高速子弹可在同一个配置窗口中分别调整。");CapturePreview("mods-combined");
  var combined=repo.Config.Mods.FirstOrDefault(m=>m.Manifest.Id=="weapon-tuning");if(combined!=null){var configWindow=new ModConfigWindow(combined);CaptureElement((FrameworkElement)configWindow.Content,"weapon-config",554,581);checks["combined-config-four-switches"]=VisualChildren<System.Windows.Controls.CheckBox>((FrameworkElement)configWindow.Content).Count()==4;}
  NavLaunch.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));PaintReadiness(RuntimeReadiness.Ready);ShowNotice("BOT 服务端完全启动","登录服务、游戏服和 BOT 均已就绪。");CapturePreview("ready-card");PaintReadiness(RuntimeReadiness.Stopped);return checks;
 }
}

