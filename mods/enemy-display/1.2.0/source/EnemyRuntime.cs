using System;using System.IO;using System.Diagnostics;using System.Drawing;using System.Windows.Forms;using System.Runtime.InteropServices;using System.Linq;
public static class WindowApi {
 [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();[DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);[DllImport("user32.dll")]public static extern bool GetClientRect(IntPtr hwnd,out RECT r);[DllImport("user32.dll")]public static extern bool ClientToScreen(IntPtr hwnd,ref POINT p);[DllImport("user32.dll")]public static extern bool IsIconic(IntPtr hwnd);[DllImport("shcore.dll")]public static extern int SetProcessDpiAwareness(int value);
 [StructLayout(LayoutKind.Sequential)]public struct RECT{public int Left,Top,Right,Bottom;}[StructLayout(LayoutKind.Sequential)]public struct POINT{public int X,Y;}
}
public sealed class EnemyForm:Form {
 readonly string inputFile;readonly Timer timer=new Timer{Interval=30};readonly EspOverlay overlay=new EspOverlay();readonly Stopwatch clock=Stopwatch.StartNew();FeatureInput input;AimReader reader;string token,phase="read-input",lastError="";long nextInput,nextStatus,nextDiagnostic;int samples,errors;long lastSuccess;
 public EnemyForm(string file){inputFile=file;ShowInTaskbar=false;timer.Tick+=(s,e)=>Tick();timer.Start();FormClosed+=(s,e)=>{timer.Stop();overlay.Dispose();if(reader!=null){reader.M.Process.Dispose();reader.Dispose();reader=null;}};}
 protected override void SetVisibleCore(bool value){base.SetVisibleCore(false);}
 void Publish(string state,string message,Scene scene=null){if(input==null||clock.ElapsedMilliseconds<nextStatus)return;nextStatus=clock.ElapsedMilliseconds+400;var record=new{apiVersion=1,clientBuild=Mem.ClientBuild,instanceId=token,at=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),pid=Process.GetCurrentProcess().Id,clientPid=input.client==null?0:input.client.pid,state=state,message=message,phase=phase,samples=samples,errors=errors,lastSuccessAt=lastSuccess,markers=scene==null?0:scene.Markers.Count,enemies=scene==null?0:scene.Enemies,bots=scene==null?0:scene.MatchedBots,rendered=overlay.Visible?overlay.Count:0,updates=overlay.Updates,overlayVisible=overlay.Visible,overlayTopmost=overlay.IsTopmost,overlayInputTransparent=overlay.InputTransparent,overlayHwnd=overlay.WindowHandle,lastPresentCode=overlay.LastPresentCode,presentSkipped=overlay.PresentSkipped};ModIO.Put(input.statusFile,record);if(clock.ElapsedMilliseconds>=nextDiagnostic){nextDiagnostic=clock.ElapsedMilliseconds+2000;ModIO.Event(input.stateDirectory,"enemy-display-sample",record);}}
 void ResetReader(){overlay.Hide();if(reader!=null){reader.M.Process.Dispose();reader.Dispose();reader=null;}}
 void Tick(){try{
  if(clock.ElapsedMilliseconds>=nextInput){nextInput=clock.ElapsedMilliseconds+150;phase="read-input";input=ModIO.Read<FeatureInput>(inputFile);if(input==null||!input.enabled||DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-input.at>5000){Close();return;}if(token==null)token=input.instanceId;if(token!=input.instanceId){Close();return;}}
  if(input==null||input.client==null)return;
  if(reader!=null&&(reader.M.Process.HasExited||reader.M.Process.Id!=input.client.pid))ResetReader();
  if(reader==null){phase="verify-client";var process=Process.GetProcessById(input.client.pid);ProcessIdentity.Verify(process,Path.Combine(Path.GetFullPath(input.launcherRoot),"host/mods/client/bf4_no_drm.exe"),input.client.startTicks);phase="open-memory";try{reader=new AimReader(process);}catch{process.Dispose();throw;}}
  uint foreground;var window=WindowApi.GetForegroundWindow();WindowApi.GetWindowThreadProcessId(window,out foreground);bool focused=foreground==reader.M.Process.Id;
  if(!focused||WindowApi.IsIconic(window)){overlay.Hide();phase="foreground";Publish("paused","游戏不在前台，已暂停显示");return;}
  WindowApi.RECT rect;WindowApi.POINT origin=new WindowApi.POINT();if(!WindowApi.GetClientRect(window,out rect)||rect.Right<32||rect.Bottom<32||!WindowApi.ClientToScreen(window,ref origin)){overlay.Hide();Publish("waiting","等待游戏画面尺寸");return;}
  phase="read-scene";int distance=Values.Number(input.config,"distance",100);if(distance!=0&&(distance<10||distance>300))throw new Exception("显示距离为10～300米或0（不限距离）");var scene=reader.Read(false,rect.Right,rect.Bottom,input.roster,true,distance,Values.Flag(input.config,"excludeHumans",true));samples++;
  if(!scene.Ready){overlay.Hide();Publish("waiting",scene.Status??"等待本机步兵部署",scene);return;}lastSuccess=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  phase="draw-overlay";bool boxes=Values.Flag(input.config,"boxes",true),lines=Values.Flag(input.config,"lines",true);overlay.UpdateDisplay(new Rectangle(origin.X,origin.Y,rect.Right,rect.Bottom),scene.Markers,true,boxes,lines);
  bool topmost=overlay.IsTopmost;
  string state=overlay.Count>0&&overlay.Visible&&topmost?"applied":scene.Markers.Count>0?"waiting-display":"waiting-target";
  string message=!boxes&&!lines?"方框和指示线均已关闭":overlay.Count>0?(topmost?"显示窗已置顶，已提交 "+overlay.Count+" 个敌方步兵标记":"正在恢复显示窗口层级"):scene.Markers.Count>0?"正在提交游戏标记":"场景读取正常，屏幕内暂无有效目标（敌方 "+scene.Enemies+"）";Publish(state,message,scene);lastError="";
 }catch(Exception error){errors++;overlay.Hide();var e=error.GetBaseException();if(input!=null&&(lastError!=e.ToString()||clock.ElapsedMilliseconds>=nextDiagnostic)){lastError=e.ToString();nextDiagnostic=clock.ElapsedMilliseconds+2000;ModIO.Event(input.stateDirectory,"enemy-display-error",new{phase=phase,error=e.ToString(),clientPid=input.client==null?0:input.client.pid});}
  bool exited=reader!=null&&reader.M.Process.HasExited;Publish(exited?"waiting":"error",exited?"客户端已退出":e.Message);if(phase=="open-memory"||phase=="verify-client"||exited)ResetReader();
 }}
}
public static class EnemyProgram {
 [STAThread]public static int Main(string[] args){if(args.Length==1&&args[0]=="--self-test"){try{EspOverlay.Smoke();return 0;}catch(Exception error){Console.Error.WriteLine(error.ToString());return 1;}}if(args.Length!=2||args[0]!="--input")return 2;try{WindowApi.SetProcessDpiAwareness(2);}catch{}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new EnemyForm(Path.GetFullPath(args[1])));return 0;}
}
