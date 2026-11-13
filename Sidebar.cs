using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
namespace BF4Launcher;
public partial class MainWindow {
 internal bool SidebarExpanded {get;private set;}
 void Sidebar_MouseEnter(object sender, MouseEventArgs e) => AnimateSidebar(true);
 void Sidebar_MouseLeave(object sender, MouseEventArgs e) => AnimateSidebar(false);
 internal void AnimateSidebar(bool expanded, bool animate=true) {
  SidebarExpanded=expanded;
  double width=expanded?232:68, opacity=expanded?1:0;
  var labels=VisualChildren<TextBlock>(SidebarDrawer).Where(t=>t.Tag?.ToString()=="rail-label").ToArray();
  if(!animate) {
   SidebarDrawer.BeginAnimation(FrameworkElement.WidthProperty,null);
   SidebarDrawer.Width=width;
   foreach(var label in labels){label.BeginAnimation(UIElement.OpacityProperty,null);label.Opacity=opacity;}
   return;
  }
  SidebarDrawer.BeginAnimation(FrameworkElement.WidthProperty,new DoubleAnimation(SidebarDrawer.ActualWidth>0?SidebarDrawer.ActualWidth:68,width,new Duration(TimeSpan.FromMilliseconds(180))){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
  foreach(var label in labels)label.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(opacity,new Duration(TimeSpan.FromMilliseconds(130))));
 }
}
