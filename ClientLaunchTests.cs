using System;using System.Text.Json.Nodes;
namespace BF4Launcher;
public static class ClientLaunchTests {
 public static void Run(Action<bool,string> check){
  foreach(var pair in new[]{("window-ready","游戏窗口已打开"),("starting","客户端正在启动"),("unresponsive","客户端无响应"),("no-window","客户端未打开游戏窗口"),("exited","客户端启动后退出"),("launcher-open","请在 BF4 客户端启动窗口继续")}){
   var result=ClientLaunchFeedback.From(new JsonObject{["state"]=pair.Item1,["started"]=pair.Item1=="window-ready",["message"]="客户端状态"});
   check(result.Title==pair.Item2&&result.Detail=="客户端状态"&&result.Ready==(pair.Item1=="window-ready"),"client-feedback-"+pair.Item1);
  }
  var legacy=ClientLaunchFeedback.From(new JsonObject{["started"]=true});check(!legacy.Ready&&!legacy.Title.Contains("已打开"),"client-process-created-is-not-window-ready");
  var missing=ClientLaunchFeedback.From(null);check(!missing.Ready&&missing.Title=="客户端启动未完成","client-null-result-not-success");
 }
}
