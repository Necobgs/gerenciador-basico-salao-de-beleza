using CommunityToolkit.Mvvm.ComponentModel;
using GerenciadorSalao.Services;

namespace GerenciadorSalao.ViewModels;

public partial class RelatoriosViewModel : ObservableObject
{
    private readonly IRelatorioService _relatorioService;

    [ObservableProperty]
    private string _titulo = "Relatórios e Faturamento";

    public RelatoriosViewModel(IRelatorioService relatorioService)
    {
        _relatorioService = relatorioService;
    }
}
