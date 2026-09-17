using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TaskDock.Models;

namespace TaskDock.Infrastructure;

public sealed class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public sealed class StatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        WorkStatus.Pending => "待处理",
        WorkStatus.InProgress => "进行中",
        WorkStatus.Completed => "已完成",
        _ => ""
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public sealed class PriorityBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        TaskPriority.Urgent => new SolidColorBrush(Color.FromRgb(226, 85, 85)),
        TaskPriority.High => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        TaskPriority.Low => new SolidColorBrush(Color.FromRgb(72, 170, 120)),
        _ => new SolidColorBrush(Color.FromRgb(79, 107, 237))
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null || string.IsNullOrWhiteSpace(value.ToString()) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
