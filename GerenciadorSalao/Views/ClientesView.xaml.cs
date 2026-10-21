using System.Windows.Controls;
using System.Windows.Input;
using GerenciadorSalao.ViewModels;

namespace GerenciadorSalao.Views;

public partial class ClientesView : UserControl
{
    public ClientesView()
    {
        InitializeComponent();
    }

    private void FecharModalCliente_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ClientesViewModel vm)
        {
            vm.FecharModalClienteCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FecharHistorico_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ClientesViewModel vm)
        {
            vm.FecharHistoricoCommand.Execute(null);
            e.Handled = true;
        }
    }
}
