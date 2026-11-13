using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Data;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace BF4Launcher;
public class FieldEditor {
 public FieldDef Field{get;} public FrameworkElement Control{get;} public TextBlock OverrideHint{get;}=new(){FontSize=11,Foreground=new SolidColorBrush(Color.FromRgb(255,177,76)),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0),Visibility=Visibility.Collapsed};
 public void SetOverride(JsonNode? value,string? owner){OverrideHint.Text=owner==null?"":"模组覆盖："+(value?.ToString()??"")+" · "+owner;OverrideHint.Visibility=owner==null?Visibility.Collapsed:Visibility.Visible;}public FieldEditor(FieldDef f,JsonNode? value){Field=f;
 if(f.Type=="boolean"){Control=new CheckBox{IsChecked=value?.ToString().Equals("true",StringComparison.OrdinalIgnoreCase)==true,Content="开启",MinHeight=34,FontSize=13};}
 else if(f.Type=="select"){var c=new ComboBox{ItemsSource=f.Options,SelectedItem=f.Options.FirstOrDefault(o=>JsonNode.DeepEquals(o.Value,value)),MinWidth=130};if(c.SelectedIndex<0&&c.Items.Count>0)c.SelectedIndex=0;Control=c;}
 else {Control=new TextBox{Text=value?.ToString()??"",MinWidth=100,IsReadOnly=f.Locked};}
 Control.IsEnabled=!f.Locked;
 }
 public JsonNode? Read(){if(Control is CheckBox c)return JsonValue.Create(c.IsChecked==true);if(Control is ComboBox combo)return (combo.SelectedItem as Choice)?.Value?.DeepClone();var text=((TextBox)Control).Text;if(Field.Type=="number"){if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var n))throw new Exception(Field.Label+" 需要填写数字");return n==Math.Truncate(n)?JsonValue.Create((long)n):JsonValue.Create(n);}return JsonValue.Create(text);}
 public FrameworkElement View(){var p=new StackPanel{Margin=new Thickness(0,0,0,17)};p.Children.Add(new TextBlock{Text=Field.Label+(Field.Unit!=""?"  ·  "+Field.Unit:""),FontSize=12,Margin=new Thickness(0,0,0,7),Foreground=new SolidColorBrush(Color.FromRgb(183,205,218))});p.Children.Add(Control);p.Children.Add(OverrideHint);if(Field.Hint!="")p.Children.Add(new TextBlock{Text=Field.Hint,FontSize=11,TextWrapping=TextWrapping.Wrap,Foreground=Bf4Theme.Muted,Margin=new Thickness(0,6,0,0)});return p;}
}
public class ModConfigWindow:Window {
 public bool Saved{get;private set;}
 readonly ModItem mod;readonly bool embedded;readonly Window? hostWindow;internal event Action<bool>? CompletionRequested;internal bool IsActionRunning=>actionRunning;internal Window DialogOwner=>hostWindow??this;
 readonly Dictionary<string,FieldEditor> configEditors=new();
 readonly Dictionary<string,FieldEditor> actionEditors=new();
 readonly Dictionary<string,Button> actionButtons=new();
 readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Foreground=Bf4Theme.Muted,FontSize=12,Margin=new Thickness(0,8,0,6)};
 readonly TextBlock actionStatus=new(){TextWrapping=TextWrapping.Wrap,MaxHeight=64,TextTrimming=TextTrimming.CharacterEllipsis,Foreground=Bf4Theme.Muted,Margin=new Thickness(0,10,0,8)};
 readonly StackPanel panel=new();
 readonly StackPanel actionPanel=new();
 TabControl? sections;
 readonly StackPanel footerButtons=new(){Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)};
 readonly ProgressBar actionProgress=new(){Height=3,IsIndeterminate=true,Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,4)};
 readonly Func<string,JsonObject,Task<JsonNode?>>? invoke;
 readonly Action? persist;
 readonly Action<Exception>? logError;
 bool actionRunning;JsonNode? draftBaseline;
 JsonNode? ReadConfigDraft()=>JsonSerializer.SerializeToNode(configEditors.ToDictionary(p=>p.Key,p=>p.Value.Read()),Repository.Json);
 internal IReadOnlyDictionary<string,FieldEditor> ConfigEditors=>configEditors;
 internal IReadOnlyDictionary<string,FieldEditor> ActionEditors=>actionEditors;
 internal IReadOnlyDictionary<string,Button> ActionButtons=>actionButtons;
 internal string ActionMessage=>actionStatus.Text;
 static FieldDef CopyField(FieldDef field)=>JsonSerializer.Deserialize<FieldDef>(JsonSerializer.Serialize(field,Repository.Json),Repository.Json)!;
 public ModConfigWindow(ModItem item,Func<string,JsonObject,Task<JsonNode?>>? invokeAction=null,Action? persistConfiguration=null,Action<Exception>? reportError=null,bool embedded=false,Window? hostWindow=null){
  this.embedded=embedded;this.hostWindow=hostWindow;mod=item;invoke=invokeAction;persist=persistConfiguration;logError=reportError;
  bool operations=mod.Manifest.Tool?.Actions.Count>0;
  Title=item.Name+" · 配置";Width=operations?660:570;Height=Math.Min(operations?800:620,SystemParameters.WorkArea.Height-60);MinWidth=480;MinHeight=440;
  WindowStartupLocation=WindowStartupLocation.CenterOwner;Foreground=Bf4Theme.Text;Background=new SolidColorBrush(Color.FromRgb(10,25,35));FontFamily=new FontFamily("Microsoft YaHei UI");
  var grid=new Grid{Margin=new Thickness(28)};grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new());grid.RowDefinitions.Add(new(){Height=GridLength.Auto});Content=grid;
  var top=new StackPanel();if(!embedded)top.Children.Add(new TextBlock{Text=mod.Name,FontSize=23,FontWeight=FontWeights.Normal});if(!embedded)top.Children.Add(new TextBlock{Text=mod.Tags+"  /  "+mod.Version,Foreground=Bf4Theme.Muted,Margin=new Thickness(0,10,0,10)});
  top.Children.Add(new TextBlock{Text=mod.Description,TextWrapping=TextWrapping.Wrap,Foreground=Bf4Theme.Muted,Margin=new Thickness(0,0,0,18)});grid.Children.Add(top);
  var scroll=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  if(operations){sections=new TabControl{Margin=new Thickness(0,0,0,6),Padding=new Thickness(14,12,14,8),Background=Bf4Theme.Panel,BorderBrush=new SolidColorBrush(Color.FromRgb(61,86,102))};sections.Items.Add(new TabItem{Header="参数设置",Padding=new Thickness(14,7,14,7),Content=scroll});sections.Items.Add(new TabItem{Header="模组操作",Padding=new Thickness(14,7,14,7),Content=new ScrollViewer{Content=actionPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});Grid.SetRow(sections,1);grid.Children.Add(sections);}else{Grid.SetRow(scroll,1);grid.Children.Add(scroll);}
  foreach(var field in mod.Manifest.Fields){var editor=new FieldEditor(CopyField(field),mod.Config.GetValueOrDefault(field.Id)?.DeepClone()??field.DefaultValue?.DeepClone());configEditors.Add(field.Id,editor);panel.Children.Add(editor.View());}
  draftBaseline=ReadConfigDraft();
  if(configEditors.Count==0)panel.Children.Add(new TextBlock{Text="这个模组不需要额外配置。",Foreground=Bf4Theme.Muted,Margin=new Thickness(0,0,0,16)});
  if(operations)BuildActions();
  var bottom=new StackPanel{Margin=new Thickness(0,10,0,0)};Grid.SetRow(bottom,2);grid.Children.Add(bottom);
  status.Text=mod.Manifest.Kind=="config"?"保存参数后，按模组列表顺序覆盖服务端基础值。":"参数独立保存；启用和停用由模组列表开关决定。";bottom.Children.Add(status);bottom.Children.Add(footerButtons);
  var reset=new Button{Content="恢复默认"};reset.Click+=(sender,args)=>ResetDraft();footerButtons.Children.Add(reset);
  var cancel=new Button{Content="取消"};cancel.Click+=(sender,args)=>{if(embedded)CompletionRequested?.Invoke(false);else Close();};footerButtons.Children.Add(cancel);
  var save=new Button{Content=embedded?"保存当前版本配置":"保存配置",Style=(Style)Application.Current.FindResource("Primary")};save.Click+=(sender,args)=>{try{SaveDraft();if(embedded)CompletionRequested?.Invoke(true);else DialogResult=true;}catch(Exception ex){logError?.Invoke(ex);status.Text=UiText.Summary(ex.Message);}};footerButtons.Children.Add(save);
  Closing+=(sender,args)=>{if(actionRunning){args.Cancel=true;actionStatus.Text="正在执行模组操作，完成后可关闭窗口。";}};
 }
 void BuildActions(){
  var tool=mod.Manifest.Tool!;
  actionPanel.Children.Add(new TextBlock{Text="模组操作",FontSize=16,FontWeight=FontWeights.Normal,Margin=new Thickness(0,0,0,10)});
  var state=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Bf4Theme.Muted,Margin=new Thickness(0,0,0,14)};
  state.SetBinding(TextBlock.TextProperty,new Binding("Status"){Source=mod,Converter=new ModStatusConverter()});actionPanel.Children.Add(state);
  foreach(var field in tool.Fields){
   if(configEditors.TryGetValue(field.Id,out var existing)){
    actionEditors[field.Id]=existing;
    var selected=new TextBlock{Foreground=Bf4Theme.Muted,Margin=new Thickness(0,0,0,10),TextWrapping=TextWrapping.Wrap};
    var property=existing.Control is ComboBox?"SelectedItem.Label":existing.Control is CheckBox?"IsChecked":"Text";
    selected.SetBinding(TextBlock.TextProperty,new Binding(property){Source=existing.Control,StringFormat=field.Label+"：{0}"});actionPanel.Children.Add(selected);continue;
   }
   var editor=new FieldEditor(CopyField(field),field.DefaultValue?.DeepClone());actionEditors.Add(field.Id,editor);actionPanel.Children.Add(editor.View());
   if(field.Type=="file"&&editor.Control is TextBox box){var browse=new Button{Content="选择文件",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,-8,0,14)};browse.Click+=(sender,args)=>{var dialog=new Microsoft.Win32.OpenFileDialog{Filter="音频文件|*.wav;*.mp3|所有文件|*.*"};if(dialog.ShowDialog(DialogOwner)==true)box.Text=dialog.FileName;};actionPanel.Children.Add(browse);}
  }
  var row=new WrapPanel();actionPanel.Children.Add(row);
  foreach(var action in tool.Actions){var definition=action;var button=new Button{Content=definition.Label};button.Click+=async(sender,args)=>await ExecuteActionAsync(definition.Id);row.Children.Add(button);actionButtons.Add(definition.Id,button);}
  actionPanel.Children.Add(actionProgress);actionPanel.Children.Add(actionStatus);
  actionStatus.Text=mod.Enabled?"操作使用当前窗口填写的内容；共有参数可在“参数设置”中修改。":"启用此模组后可执行操作，参数可以先保存。";
  if(mod.Manifest.Runtime?.StartWhen=="server"&&mod.Enabled)actionStatus.Text="操作使用当前模组实例；共有参数可在“参数设置”中修改。";
  if(tool.Actions.Any(a=>a.IsLocal))actionStatus.Text=tool.Actions.All(a=>a.IsLocal)?"试听与导入无需启用模组或启动服务端，使用当前窗口填写的内容。":"本地操作可直接使用；对局操作需要启用并运行模组。";
  UpdateActionButtons();
 }
 void UpdateActionButtons(){foreach(var pair in actionButtons){var definition=mod.Manifest.Tool!.Actions.First(a=>a.Id==pair.Key);pair.Value.IsEnabled=!actionRunning&&(mod.Enabled||definition.IsLocal)&&invoke!=null;}}
 internal void ShowActions(){if(sections!=null)sections.SelectedIndex=1;}
 internal void ResetDraft(){foreach(var editor in configEditors.Values){if(editor.Control is CheckBox cb)cb.IsChecked=editor.Field.DefaultValue?.GetValue<bool>()??false;else if(editor.Control is TextBox box)box.Text=editor.Field.DefaultValue?.ToString()??"";else if(editor.Control is ComboBox combo)combo.SelectedItem=editor.Field.Options.FirstOrDefault(o=>JsonNode.DeepEquals(o.Value,editor.Field.DefaultValue));}}
 internal bool HasUnsavedChanges {get{try{return !JsonNode.DeepEquals(draftBaseline,ReadConfigDraft());}catch{return true;}}}
 internal void SaveDraft(){
  var next=mod.Config.ToDictionary(pair=>pair.Key,pair=>pair.Value?.DeepClone());foreach(var pair in configEditors)next[pair.Key]=pair.Value.Read();Repository.Validate(next,mod.Manifest.Fields);
  var before=mod.Config;mod.Config=next;
  try{persist?.Invoke();Saved=true;draftBaseline=ReadConfigDraft();}catch{mod.Config=before;throw;}
 }
 internal async Task ExecuteActionAsync(string actionId){
  if(actionRunning)return;
  var action=mod.Manifest.Tool?.Actions.FirstOrDefault(a=>a.Id==actionId);
  if(action==null||invoke==null){actionStatus.Text="此模组未提供该操作。";return;}
  if(!mod.Enabled&&!action.IsLocal){actionStatus.Text="请先在模组列表启用此版本。";return;}
  if(!string.IsNullOrEmpty(action.Confirm)&&MessageBox.Show(DialogOwner,action.Confirm,action.Label,MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
  actionRunning=true;panel.IsEnabled=actionPanel.IsEnabled=footerButtons.IsEnabled=false;actionProgress.Visibility=Visibility.Visible;actionStatus.Text="正在"+action.Label+"…";UpdateActionButtons();
  try{
   var body=new JsonObject();foreach(var pair in actionEditors)body[pair.Key]=pair.Value.Read();
   var result=await invoke(actionId,body);
   if(result?["options"] is JsonObject options)foreach(var pair in actionEditors){if(pair.Value.Control is not ComboBox combo||options[pair.Key] is not JsonArray choices)continue;var previous=(combo.SelectedItem as Choice)?.Value?.DeepClone();var items=choices.Select(c=>new Choice{Label=c!["label"]!.ToString(),Value=c["value"]?.DeepClone()}).ToList();pair.Value.Field.Options=items;combo.ItemsSource=items;combo.SelectedItem=items.FirstOrDefault(c=>JsonNode.DeepEquals(c.Value,previous))??items.FirstOrDefault();}
   actionStatus.Text=UiText.Summary(result?["message"]?.ToString(),"操作已完成。");
  }catch(Exception ex){logError?.Invoke(ex);actionStatus.Text=UiText.Summary(ex.Message,"模组操作未完成，请查看运行日志。");}
  finally{actionRunning=false;panel.IsEnabled=actionPanel.IsEnabled=footerButtons.IsEnabled=true;actionProgress.Visibility=Visibility.Collapsed;UpdateActionButtons();}
 }
 sealed class ModStatusConverter:System.Windows.Data.IValueConverter {
  public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>UiText.Summary(value?.ToString(),"等待模组运行。");
  public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
 }
}
