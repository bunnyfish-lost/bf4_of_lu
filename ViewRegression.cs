using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace BF4Launcher;
public partial class MainWindow {
 static IEnumerable<T> VisualChildren<T>(DependencyObject parent) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T typed)yield return typed;foreach(var nested in VisualChildren<T>(child))yield return nested;}}
 internal void CapturePreview(string name){CaptureElement((FrameworkElement)Content,name,1280,822);}
 internal static void CaptureElement(FrameworkElement content,string name,int width,int height){content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(Window.GetWindow(content)?.Background??Brushes.White,null,new Rect(0,0,width,height));image.Render(background);image.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));var directory=Path.Combine(AppContext.BaseDirectory,"test-previews");Directory.CreateDirectory(directory);using var stream=File.Create(Path.Combine(directory,name+".png"));encoder.Save(stream);}
 internal async Task<Dictionary<string,bool>> CheckRotationBindingsAsync(){
  var result=new Dictionary<string,bool>();var originals=rotationRows.ToArray();var before=originals.ToDictionary(r=>r,r=>(r.Map,r.Mode,r.Rounds));
  if(originals.Length<2)throw new Exception("轮换检查至少需要两项。");
  var content=(FrameworkElement)Content;content.Measure(new Size(1280,860));content.Arrange(new Rect(0,0,1280,860));content.UpdateLayout();RotationGrid.ApplyTemplate();RotationGrid.UpdateLayout();
  MoveRotation(originals[0],1);await Dispatcher.Yield(DispatcherPriority.DataBind);content.UpdateLayout();
  result["rotation-first-move-reorders-rows"]=rotationRows.IndexOf(originals[0])==1&&rotationRows[0]==originals[1];
  result["rotation-move-keeps-map-and-mode"]=originals.All(r=>before[r]==(r.Map,r.Mode,r.Rounds));
  MoveRotation(originals[^1],0);await Dispatcher.Yield(DispatcherPriority.Loaded);content.UpdateLayout();
  result["rotation-repeated-move-keeps-values"]=originals.All(r=>before[r]==(r.Map,r.Mode,r.Rounds));
  for(int i=0;i<20;i++){MoveRotation(originals[i%originals.Length],(i+2)%originals.Length);await Dispatcher.Yield(DispatcherPriority.DataBind);content.UpdateLayout();}
  result["rotation-twenty-reorders-keep-values"]=originals.All(r=>before[r]==(r.Map,r.Mode,r.Rounds));
  var combos=VisualChildren<ComboBox>(RotationGrid).Where(c=>c.DataContext is RotationEntry).ToArray();
  result["rotation-selection-controls-generated"]=combos.Length>=originals.Length*2;
  result["rotation-selection-controls-not-empty"]=combos.Length>0&&combos.All(c=>c.SelectedValue is string value&&!string.IsNullOrWhiteSpace(value));
  result["rotation-controls-match-row-data"]=combos.Length>0&&combos.All(c=>{var row=(RotationEntry)c.DataContext;var field=c.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty)?.ParentBinding.Path.Path;return Equals(c.SelectedValue,field=="Map"?row.Map:row.Mode);});
  loadingRotation=true;rotationRows.Clear();foreach(var row in originals)rotationRows.Add(row);loadingRotation=false;RefreshRotationHeaders();return result;
 }
}
