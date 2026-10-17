using System.Windows.Controls;

namespace GerenciadorSalao.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void ChartArea_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement fe && DataContext is ViewModels.DashboardViewModel vm)
        {
            var pos = e.GetPosition(fe);
            double width = fe.ActualWidth;
            if (width <= 0) return;

            double colWidth = width / 12.0;
            int monthIndex = (int)(pos.X / colWidth);
            if (monthIndex < 0) monthIndex = 0;
            if (monthIndex > 11) monthIndex = 11;

            vm.AtualizarHoverMes(monthIndex, width);
        }
    }

    private void ChartArea_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (DataContext is ViewModels.DashboardViewModel vm)
        {
            vm.LimparHover();
        }
    }
}
