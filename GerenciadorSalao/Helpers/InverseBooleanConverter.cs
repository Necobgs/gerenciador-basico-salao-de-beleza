using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GerenciadorSalao.Helpers;

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (targetType == typeof(Visibility))
        {
            if (value is bool bv)
            {
                return bv ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        if (value is bool b)
        {
            return !b;
        }
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility vis)
        {
            return vis != Visibility.Visible;
        }

        if (value is bool b)
        {
            return !b;
        }
        return false;
    }
}
