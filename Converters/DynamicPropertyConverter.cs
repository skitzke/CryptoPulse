using System.Globalization;
using Microsoft.Maui.Controls;

namespace CryptoPulse.Converters;

// quick converter for XAML bindings that need simple tweaks
public class DynamicPropertyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // if XAML passes "Invert" → flip a bool
        if (parameter?.ToString() == "Invert" && value is bool b)
            return !b;

        // otherwise just hand back the value as-is
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        // we don’t use two-way binding here, so no need to implement
        => throw new NotImplementedException();
}
