using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace BF4Launcher;
public partial class MainWindow {
 internal async Task<Dictionary<string,bool>> CheckExternalViewsAsync(){
  var result=new Dictionary<string,bool>();NavMods.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));OperationBanner.Visibility=Visibility.Collapsed;var content=(FrameworkElement)Content;content.Measure(new Size(1280,822));content.Arrange(new Rect(0,0,1280,822));content.UpdateLayout();result["external-directory-refresh-button-present"]=VisualChildren<Button>(content).Any(b=>b.Content?.ToString()=="刷新目录");
  var item=repo.ModCards.FirstOrDefault(m=>m.Manifest.Adapter=="external");if(item!=null){ModsList.ScrollIntoView(item);await Dispatcher.Yield(DispatcherPriority.Loaded);content.UpdateLayout();CapturePreview("external-mod-library");var window=new ModConfigWindow(item);CaptureElement((FrameworkElement)window.Content,"external-mod-config",554,581);result["external-folder-module-visible-in-library"]=ModsList.Items.Contains(item);result["external-config-controls-generated-from-module-manifest"]=VisualChildren<TextBox>((FrameworkElement)window.Content).Count()+VisualChildren<CheckBox>((FrameworkElement)window.Content).Count()+VisualChildren<ComboBox>((FrameworkElement)window.Content).Count()>=item.Manifest.Fields.Count;}
  return result;
 }
}
