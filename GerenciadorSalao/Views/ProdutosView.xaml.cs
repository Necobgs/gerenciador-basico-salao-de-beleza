using System.Windows.Controls;
using System.Windows.Input;
using GerenciadorSalao.ViewModels;

namespace GerenciadorSalao.Views;

public partial class ProdutosView : UserControl
{
    public ProdutosView()
    {
        InitializeComponent();
    }

    private void FecharModalProduto_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ProdutosViewModel vm)
        {
            vm.FecharModalProdutoCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FecharModalExcluir_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ProdutosViewModel vm)
        {
            vm.FecharModalExcluirCommand.Execute(null);
            e.Handled = true;
        }
    }
}
