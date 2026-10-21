using System.Windows.Controls;
using System.Windows.Input;
using GerenciadorSalao.ViewModels;

namespace GerenciadorSalao.Views;

public partial class ServicosView : UserControl
{
    public ServicosView()
    {
        InitializeComponent();
    }

    private void FecharModalServico_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ServicosViewModel vm)
        {
            vm.FecharModalServicoCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FecharModalExcluir_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ServicosViewModel vm)
        {
            vm.FecharModalExcluirCommand.Execute(null);
            e.Handled = true;
        }
    }
}
