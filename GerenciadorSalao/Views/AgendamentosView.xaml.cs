using System.Windows.Controls;
using System.Windows.Input;
using GerenciadorSalao.ViewModels;

namespace GerenciadorSalao.Views;

public partial class AgendamentosView : UserControl
{
    public AgendamentosView()
    {
        InitializeComponent();
    }

    private void FecharModalNovoAgendamento_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AgendamentosViewModel vm)
        {
            vm.FecharModalNovoAgendamentoCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FecharModalExcluir_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AgendamentosViewModel vm)
        {
            vm.FecharModalExcluirCommand.Execute(null);
            e.Handled = true;
        }
    }
}
