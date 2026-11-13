using System;using System.Linq;
namespace BF4Launcher;
public static class UiTextTests {
 public static void Run(Action<bool,string> check){
  check(UiText.Summary("等待游戏启动")=="等待游戏启动","ui-normal-state-kept");
  check(UiText.Summary("Command failed: C:\\Windows\\powershell.exe -Command Get-Process", "客户端检测未完成。")=="客户端检测未完成。","ui-command-hidden");
  check(UiText.Summary("{\"error\":\"请选择地图\"}")=="请选择地图","ui-json-error-unwrapped");
  check(UiText.Summary("第一行\n第二行")=="第一行 第二行","ui-newlines-collapsed");
  check(UiText.Summary(new string('长',400)).Length<=150,"ui-length-bounded");
  check(UiText.Summary("   ","等待更新")=="等待更新","ui-empty-fallback");
 }
}
