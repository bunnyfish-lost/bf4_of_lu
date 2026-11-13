using System;
using System.Text.Json.Nodes;
namespace BF4Launcher;
public partial class MainWindow {
 void InstallV2Controls(){
  ServiceButtons.Children.Add(MButton("检查运行文件",async()=>{
   await Work("检查运行文件",async()=>{
    SavePaths();
    await System.Threading.Tasks.Task.Run(()=>EngineBootstrap.Ensure(repo.Root));
    var result=await backend.Bridge("check-environment");
    var items=result?["items"]?.AsArray()??new JsonArray();
    if(items.Count==0){ShowNotice("v2 运行文件完整","程序、实例配置和资源链接检查通过。");Log("v2 运行文件检查通过。");}
    else{foreach(var item in items)Log(item?["kind"]+" · "+item?["file"]);ShowNotice("发现 "+items.Count+" 项需要处理","点击“检查并修复运行环境”补齐文件和修正路径，详情见运行记录。",false,true);}
   });
  }));
  ServiceButtons.Children.Add(MButton("复制 Battlelog 地址",()=>{
   System.Windows.Clipboard.SetText("http://127.0.0.1/bf4/");
   ShowNotice("本地 Battlelog 地址已复制","启动服务后，在浏览器地址栏粘贴打开。");
  }));
 }
}
