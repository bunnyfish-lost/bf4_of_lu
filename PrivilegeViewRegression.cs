using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace BF4Launcher;
public partial class MainWindow {
 internal async Task<Dictionary<string,bool>> CheckPrivilegeNoticeAsync() {
  var checks=new Dictionary<string,bool>();var content=(FrameworkElement)Content;
  void Layout(){content.Measure(new Size(1440,930));content.Arrange(new Rect(0,0,1440,930));content.UpdateLayout();}
  try {
   UpdatePrivilegeNotice(false);Layout();
   checks["privilege-non-admin-has-persistent-large-yellow-banner"]=AdminRequiredBanner.Visibility==Visibility.Visible&&AdminRequiredBanner.ActualHeight>=58&&((SolidColorBrush)AdminRequiredBanner.BorderBrush).Color==Color.FromRgb(255,209,102);
   checks["privilege-restart-action-visible"]=AdminRestartButton.Visibility==Visibility.Visible&&AdminRestartButton.Content.ToString()!.Contains("管理员");
   var before=PageHost.RenderSize;ShowNotice("配置已保存","操作反馈与管理员提示同时显示。");Layout();
   checks["privilege-warning-and-operation-feedback-coexist"]=AdminRequiredBanner.Visibility==Visibility.Visible&&OperationBanner.Visibility==Visibility.Visible&&Grid.GetRow(AdminRequiredBanner)<Grid.GetRow(OperationBanner);
   checks["privilege-feedback-does-not-change-page-size"]=PageHost.RenderSize==before;
   checks["privilege-both-footer-panels-fit"]=AdminRequiredBanner.ActualHeight+AdminRequiredBanner.Margin.Top+AdminRequiredBanner.Margin.Bottom+OperationBanner.ActualHeight<=FooterFeedbackHost.ActualHeight+1;
   SetInputsEnabled(false);checks["privilege-restart-disabled-during-operation"]=!AdminRestartButton.IsEnabled;SetInputsEnabled(true);
   CaptureElement(content,"privilege-yellow-footer",1440,930);
   UpdatePrivilegeNotice(true);Layout();
   checks["privilege-admin-hides-warning-and-keeps-compact-footer"]=AdminRequiredBanner.Visibility==Visibility.Collapsed&&FooterRow.Height.Value==84&&AdminButton.Visibility==Visibility.Collapsed;
   await Task.CompletedTask;return checks;
  }finally{UpdatePrivilegeNotice(Backend.IsAdmin);OperationBanner.Visibility=Visibility.Collapsed;noticeTimer.Stop();}
 }
}
