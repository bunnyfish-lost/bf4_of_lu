using System;using System.Windows;using System.Diagnostics;using System.IO;using System.Text.Json.Nodes;
namespace BF4Launcher;
public partial class MainWindow {
 void InstallClientDiagnostics(){ServiceButtons.Children.Add(MButton("重启客户端",async()=>{if(MessageBox.Show(this,"关闭本启动器目录下的游戏客户端并重新启动？BOT 服务端保持运行。","重启客户端",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;await Work("重启客户端",async()=>{SavePaths();var result=await backend.Launch(true);var feedback=ClientLaunchFeedback.From(result);Log(feedback.Detail);SetCompletion(feedback.Title,feedback.Detail,!feedback.Ready);await RefreshState(false);},OperationKind.LaunchGame);}));ServiceButtons.Children.Add(MButton("打开启动诊断",()=>{var directory=Path.Combine(repo.Root,"data","client-diagnostics");Directory.CreateDirectory(directory);Process.Start(new ProcessStartInfo("explorer.exe",directory){UseShellExecute=true});}));}
}
