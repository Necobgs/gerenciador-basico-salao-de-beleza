using System.Windows;
using System.Windows.Controls;
using GerenciadorSalao.ViewModels;

namespace GerenciadorSalao.Views;

public partial class RelatoriosView : UserControl
{
    public RelatoriosView()
    {
        InitializeComponent();
    }

    private void ChartArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is RelatoriosViewModel vm && e.NewSize.Width > 50)
        {
            vm.RedesenharGrafico(e.NewSize.Width);
        }
    }
}
