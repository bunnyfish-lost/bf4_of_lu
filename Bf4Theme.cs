using System;using System.Runtime.InteropServices;using System.Windows.Interop;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Input;
namespace BF4Launcher;
public static class Bf4Theme {
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 public static void DarkCaption(Window window){try{var hwnd=new WindowInteropHelper(window).Handle;if(hwnd==IntPtr.Zero)return;int on=1;if(DwmSetWindowAttribute(hwnd,20,ref on,4)!=0)DwmSetWindowAttribute(hwnd,19,ref on,4);}catch{}}

 public static readonly DependencyProperty IsActiveProperty=DependencyProperty.RegisterAttached("IsActive",typeof(bool),typeof(Bf4Theme),new FrameworkPropertyMetadata(false));
 public static bool GetIsActive(DependencyObject o)=>(bool)o.GetValue(IsActiveProperty);public static void SetIsActive(DependencyObject o,bool value)=>o.SetValue(IsActiveProperty,value);
 public static SolidColorBrush Muted {get;}=new(Color.FromRgb(169,192,205));public static SolidColorBrush Text {get;}=new(Color.FromRgb(237,244,248));public static SolidColorBrush Panel {get;}=new(Color.FromRgb(10,25,35));
 public static void SetActive(Button b,bool active){b.ClearValue(Control.BackgroundProperty);b.ClearValue(Control.ForegroundProperty);SetIsActive(b,active);}
}
public partial class MainWindow {
 void FitBf4Window(){if(App.UiPreview)return;Width=Math.Min(Width,SystemParameters.WorkArea.Width-24);Height=Math.Min(Height,SystemParameters.WorkArea.Height-24);}
 void RefreshBf4Navigation(string page){foreach(var top in new[]{NavLaunch,NavMods,NavServer,navPlayers}){if(top!=null)Bf4Theme.SetActive(top,top.Tag?.ToString()==page||(page=="rotation"&&top==NavLaunch));}foreach(var b in VisualChildren<Button>((DependencyObject)Content)){if(b.Style!=FindResource("BattleTab")&&b.Style!=FindResource("BattleRail"))continue;string tag=b.Tag?.ToString()??"";Bf4Theme.SetActive(b,tag==page||(page=="rotation"&&tag=="launch"&&b.Style==FindResource("BattleTab")));}Bf4Theme.SetActive(NavRotation,page=="rotation");}
 void ScrollBf4RotationToTop(){if(LaunchPage.Content is FrameworkElement stack){var top=RotationCard.TransformToAncestor(stack).Transform(new Point(0,0)).Y;LaunchPage.ScrollToVerticalOffset(top);}}
 void Bf4Minimize_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
 void Bf4Maximize_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
 void Bf4Close_Click(object sender,RoutedEventArgs e)=>Close();
 void Bf4Drag_MouseDown(object sender,MouseButtonEventArgs e){if(e.ChangedButton!=MouseButton.Left)return;if(e.ClickCount==2)Bf4Maximize_Click(sender,new RoutedEventArgs());else try{DragMove();}catch(InvalidOperationException){}e.Handled=true;}
}
