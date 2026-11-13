using System.Text.Json.Nodes;
namespace BF4Launcher;
public sealed record ClientLaunchFeedback(string Title,string Detail,bool Ready){
 public static ClientLaunchFeedback From(JsonNode? result){
  string state=result?["state"]?.ToString()??"";
  bool ready=state=="window-ready";
  string title=state switch{
   "window-ready"=>"游戏窗口已打开",
   "starting"=>"客户端正在启动",
   "unresponsive"=>"客户端无响应",
   "no-window"=>"客户端未打开游戏窗口",
   "exited"=>"客户端启动后退出",
   "blocked"=>"客户端启动受阻",
   "launcher-open"=>"请在 BF4 客户端启动窗口继续",
   "error"=>"客户端启动未完成",
   _=>result?["started"]?.GetValue<bool>()==true?"游戏进程已创建，等待窗口":"客户端启动未完成"
  };
  return new(title,result?["message"]?.ToString()??"请查看客户端启动诊断。",ready);
 }
}
