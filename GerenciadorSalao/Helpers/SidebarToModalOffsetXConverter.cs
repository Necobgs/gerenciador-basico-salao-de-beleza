using System;
using System.Globalization;
using System.Windows.Data;

namespace GerenciadorSalao.Helpers;

/// <summary>
/// Converte o estado de abertura da Sidebar no offset X exato necessário para centralizar 
/// qualquer modal perfeitamente em relação à janela da aplicação como um todo.
/// </summary>
public class SidebarToModalOffsetXConverter : IValueConverter
{
    /// <summary>
    /// Deslocamento horizontal quando a sidebar está aberta (-115px, ou seja, -230px / 2).
    /// </summary>
    public double OffsetWhenOpen { get; set; } = -115.0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSidebarOpen && isSidebarOpen)
        {
            return OffsetWhenOpen;
        }
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
