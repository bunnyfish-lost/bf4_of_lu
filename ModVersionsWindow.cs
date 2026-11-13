using System;using System.Collections.Generic;using System.Linq;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace BF4Launcher;
public sealed class ModVersionsWindow:Window {
 readonly Repository repo;readonly string id;readonly Func<string,string,System.Text.Json.Nodes.JsonObject,Task<System.Text.Json.Nodes.JsonNode?>>? invoke;readonly Func<ModItem,Task>? stop;readonly Action changed;readonly Action<Exception>? report;
 readonly ComboBox versions=new(){MinWidth=180,DisplayMemberPath="Version"};readonly ContentControl body=new();readonly TextBlock note=new(){TextWrapping=TextWrapping.Wrap,Foreground=Bf4Theme.Muted,Margin=new Thickness(0,9,0,0)};readonly Button remove=new(){Content="卸载所选版本",Margin=new Thickness(12,0,0,0)};ModItem selected;ModConfigWindow editor=null!;bool loading,busy;
 internal ModItem SelectedMod=>selected;internal ModConfigWindow CurrentEditor=>editor;internal ComboBox VersionSelector=>versions;
 public ModVersionsWindow(Repository repository,ModItem initial,Func<string,string,System.Text.Json.Nodes.JsonObject,Task<System.Text.Json.Nodes.JsonNode?>>? invokeAction,Action onChange,Func<ModItem,Task>? stopRuntime=null,Action<Exception>? onError=null){
  repo=repository;selected=initial;id=initial.Manifest.Id;invoke=invokeAction;changed=onChange;stop=stopRuntime;report=onError;
  Title=initial.Name+" · 模组设置";Width=710;Height=Math.Min(850,SystemParameters.WorkArea.Height-50);MinWidth=540;MinHeight=540;Background=Bf4Theme.Panel;Foreground=Bf4Theme.Text;FontFamily=new FontFamily("Microsoft YaHei UI");WindowStartupLocation=WindowStartupLocation.CenterOwner;
  var grid=new Grid{Margin=new Thickness(24)};grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new());Content=grid;
  var header=new StackPanel();header.Children.Add(new TextBlock{Text=initial.Name,FontSize=25});var row=new DockPanel{Margin=new Thickness(0,15,0,0)};var label=new TextBlock{Text="当前版本",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)};row.Children.Add(label);DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);row.Children.Add(versions);header.Children.Add(row);header.Children.Add(note);grid.Children.Add(header);Grid.SetRow(body,1);grid.Children.Add(body);
  RefreshVersions();BuildEditor();versions.SelectionChanged+=async(s,e)=>{if(!loading&&versions.SelectedItem is ModItem next&&next.Key!=selected.Key)await SwitchAsync(next);};remove.Click+=async(s,e)=>await RemoveVersionAsync();
  Closed+=(s,e)=>editor.Close();
  Closing+=(s,e)=>{if(busy||editor.IsActionRunning){e.Cancel=true;note.Text="操作完成后可关闭窗口。";}};
 }
 void RefreshVersions(){loading=true;versions.ItemsSource=repo.VersionsOf(id);versions.SelectedItem=selected;loading=false;remove.IsEnabled=true;}
 void BuildEditor(){if(editor!=null)editor.Close();editor=new ModConfigWindow(selected,invoke==null?null:(a,b)=>invoke(selected.Key,a,b),()=>{repo.SaveModVersion(selected);changed();},report,true,this);editor.CompletionRequested+=saved=>{if(saved)DialogResult=true;else Close();};var content=editor.Content;editor.Content=null;body.Content=content;note.Text="已安装 "+repo.VersionsOf(id).Count+" 个版本 · 版本选择立即保存，参数只写入当前版本。";}
 bool ResolveDraft(){if(!editor.HasUnsavedChanges)return true;var answer=MessageBox.Show(this,"保存 "+selected.Version+" 的参数后切换吗？","切换版本",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);if(answer==MessageBoxResult.Cancel)return false;if(answer==MessageBoxResult.Yes)editor.SaveDraft();return true;}
 internal async Task SwitchAsync(ModItem next,bool confirmDraft=true){if(busy||editor.IsActionRunning){RefreshVersions();return;}try{if(confirmDraft&&!ResolveDraft()){RefreshVersions();return;}busy=true;versions.IsEnabled=remove.IsEnabled=false;await Task.Yield();selected=repo.SelectModVersion(id,next.Version);BuildEditor();RefreshVersions();changed();}catch(Exception ex){report?.Invoke(ex);note.Text=ex.Message;RefreshVersions();}finally{busy=false;versions.IsEnabled=remove.IsEnabled=true;}}
 async Task RemoveVersionAsync(){if(busy||editor.IsActionRunning)return;var count=repo.VersionsOf(id).Count;string text="卸载 "+selected.Version+"？该版本的文件和参数会移入本地移除记录，其他版本保留。";if(selected.Enabled)text+="\n此模组会先停用。";if(MessageBox.Show(this,text,"卸载版本",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
  try{busy=true;versions.IsEnabled=remove.IsEnabled=false;repo.SetModEnabled(id,false);if(stop!=null)await stop(selected);repo.RemoveModVersion(selected);changed();if(count<=1){busy=false;Close();return;}selected=repo.ModCards.Single(m=>m.Manifest.Id==id);BuildEditor();RefreshVersions();note.Text="版本已卸载；其他版本及参数保留，模组当前关闭。";}catch(Exception ex){report?.Invoke(ex);note.Text=ex.Message;}finally{busy=false;versions.IsEnabled=remove.IsEnabled=true;}
 }
}
public sealed partial class ExternalModHost {
 public async Task StopGroupAsync(string name){await gate.WaitAsync();try{if(running.Remove(name,out var task))await Stop(task);}finally{gate.Release();}}
}
public partial class Backend {
 public async Task StopModAsync(ModItem item){if(item.Manifest.Adapter=="external"&&externalMods!=null)await externalMods.StopGroupAsync(ExternalModSpec.Group(item));}
}
