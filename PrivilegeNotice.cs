using System.Windows;
namespace BF4Launcher;
public partial class MainWindow {
 void UpdatePrivilegeNotice(bool administrator) {
  AdminButton.Visibility=administrator?Visibility.Collapsed:Visibility.Visible;
  AdminRequiredBanner.Visibility=administrator?Visibility.Collapsed:Visibility.Visible;
  FooterRow.Height=new GridLength(administrator?84:156);
 }
}
