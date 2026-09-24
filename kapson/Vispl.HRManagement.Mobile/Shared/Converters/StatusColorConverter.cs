using System.Globalization;

namespace Vispl.HRManagement.Mobile.Shared.Converters;

public sealed class StatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString()?.ToLowerInvariant() switch
        {
            "active" or "shortlisted" or "today" => Color.FromArgb("#39B87F"),
            "draft" or "pending review" or "scheduled" => Color.FromArgb("#F2B52C"),
            "expired" or "rejected" => Color.FromArgb("#E94D55"),
            "interview" => Color.FromArgb("#14BEE4"),
            _ => Color.FromArgb("#75839C")
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

public sealed class AccentColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString()?.ToLowerInvariant() switch
        {
            "cyan" => Color.FromArgb("#14BEE4"),
            "gold" => Color.FromArgb("#F2B52C"),
            _ => Color.FromArgb("#347CF1")
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
