using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
namespace BF4Launcher;
public sealed class KeepSelectionConverter:IValueConverter {
 public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value??Binding.DoNothing;
 public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>value is string s&&!string.IsNullOrWhiteSpace(s)?s:Binding.DoNothing;
}
